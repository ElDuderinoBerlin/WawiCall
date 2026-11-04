using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;
using MySqlConnector;
using Npgsql;

namespace WawiCallPro.Database;

public interface IDatabaseMigrationLease : IAsyncDisposable
{
    Task EnsureHeldAsync(CancellationToken cancellationToken = default);
}

public interface IDatabaseMigrationLock
{
    Task<IDatabaseMigrationLease> AcquireAsync(DatabaseTarget target, TimeSpan timeout, CancellationToken cancellationToken = default);
}

/// <summary>Exclusive cooperating-process gate: persistent SQLite sidecar or a dedicated, non-pooled server session.</summary>
public sealed class DatabaseMigrationLock : IDatabaseMigrationLock
{
    private const long PostgresKey = 0x5743504D49475241;
    private const string Resource = "WawiCallPro.Migrations";

    public async Task<IDatabaseMigrationLease> AcquireAsync(DatabaseTarget target, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromMinutes(5)) throw new DatabaseOperationException(DatabaseErrorCode.InvalidTarget);
        var timer = Stopwatch.StartNew();
        DbConnection? connection = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (target.Type != DatabaseType.Sqlite)
            {
                connection = CreateSession(target);
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }
            var resource = target.Type == DatabaseType.MySql ?
                "wcp.migrate:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(target.DatabaseName.ToLowerInvariant())))[..52] : Resource;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (target.Type == DatabaseType.Sqlite)
                {
                    if (!File.Exists(target.SqlitePath)) throw new DatabaseOperationException(DatabaseErrorCode.MissingDatabase);
                    try
                    {
                        var file = new FileStream(target.SqlitePath + ".migration.lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                        return new FileLease(file);
                    }
                    catch (IOException exception) when ((exception.HResult & 0xFFFF) is 32 or 33) { }
                }
                else
                {
                    var sql = target.Type switch
                    {
                        DatabaseType.SqlServer => "DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=@resource,@LockMode='Exclusive',@LockOwner='Session',@LockTimeout=0,@DbPrincipal='public'; SELECT @r;",
                        DatabaseType.MySql => "SELECT GET_LOCK(@resource,0)",
                        _ => "SELECT pg_try_advisory_lock(" + PostgresKey + ")"
                    };
                    await using var command = DatabaseFactory.Command(connection!, target, sql);
                    Add(command, "@resource", resource);
                    var answer = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                    var acquired = target.Type == DatabaseType.SqlServer ? Convert.ToInt32(answer) >= 0 :
                        answer is not null && answer is not DBNull && Convert.ToInt32(answer) == 1;
                    if (target.Type == DatabaseType.SqlServer && Convert.ToInt32(answer) < -1)
                        throw new DatabaseOperationException(DatabaseErrorCode.MigrationLockLost);
                    if (acquired)
                    {
                        long sessionId = 0;
                        if (target.Type == DatabaseType.PostgreSql)
                        {
                            await using var session = DatabaseFactory.Command(connection!, target, "SELECT pg_backend_pid()");
                            sessionId = Convert.ToInt64(await session.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
                        }
                        var lease = new ServerLease(connection!, target, resource, sessionId);
                        connection = null;
                        return lease;
                    }
                }
                var remaining = timeout - timer.Elapsed;
                if (remaining <= TimeSpan.Zero) throw new DatabaseOperationException(DatabaseErrorCode.MigrationLockTimeout);
                await Task.Delay(remaining < TimeSpan.FromMilliseconds(50) ? remaining : TimeSpan.FromMilliseconds(50), cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception) when (exception is DbException or IOException or UnauthorizedAccessException)
        {
            throw new DatabaseOperationException(DatabaseOperationException.Classify(exception));
        }
        finally { if (connection is not null) await connection.DisposeAsync().ConfigureAwait(false); }
    }

    private static DbConnection CreateSession(DatabaseTarget target) => target.Type switch
    {
        DatabaseType.SqlServer => new SqlConnection(new SqlConnectionStringBuilder(target.ConnectionString) { Pooling = false, ConnectRetryCount = 0 }.ConnectionString),
        DatabaseType.MySql => new MySqlConnection(new MySqlConnectionStringBuilder(target.ConnectionString) { Pooling = false }.ConnectionString),
        DatabaseType.PostgreSql => new NpgsqlConnection(new NpgsqlConnectionStringBuilder(target.ConnectionString) { Pooling = false, Multiplexing = false }.ConnectionString),
        _ => throw new DatabaseOperationException(DatabaseErrorCode.InvalidTarget)
    };

    private static void Add(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter(); parameter.ParameterName = name; parameter.Value = value; command.Parameters.Add(parameter);
    }

    private sealed class FileLease(FileStream file) : IDatabaseMigrationLease
    {
        public Task EnsureHeldAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!file.CanWrite) throw new DatabaseOperationException(DatabaseErrorCode.MigrationLockLost);
            return Task.CompletedTask;
        }
        // The sidecar stays in place: unlinking it permits a second lock inode/handle during an acquisition race.
        public ValueTask DisposeAsync() => file.DisposeAsync();
    }

    private sealed class ServerLease(DbConnection connection, DatabaseTarget target, string resource, long sessionId) : IDatabaseMigrationLease
    {
        public async Task EnsureHeldAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (connection.State != ConnectionState.Open) throw new DatabaseOperationException(DatabaseErrorCode.MigrationLockLost);
                var sql = target.Type switch
                {
                    DatabaseType.SqlServer => "SELECT CASE WHEN APPLOCK_MODE('public',@resource,'Session')='Exclusive' THEN 1 ELSE 0 END",
                    DatabaseType.MySql => "SELECT CASE WHEN IS_USED_LOCK(@resource)=CONNECTION_ID() THEN 1 ELSE 0 END",
                    _ => "SELECT pg_backend_pid()"
                };
                await using var command = DatabaseFactory.Command(connection, target, sql);
                Add(command, "@resource", resource);
                var answer = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
                if (answer != (target.Type == DatabaseType.PostgreSql ? sessionId : 1))
                    throw new DatabaseOperationException(DatabaseErrorCode.MigrationLockLost);
            }
            catch (DbException) { throw new DatabaseOperationException(DatabaseErrorCode.MigrationLockLost); }
        }

        public async ValueTask DisposeAsync()
        {
            // Closing a non-pooled session releases its native locks even after a failed connection/command.
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }
}

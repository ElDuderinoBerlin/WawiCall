using System.Data.Common;

namespace WawiCallPro.Database;

/// <summary>A receipt proves that this exact target object was explicitly created during this process.</summary>
public sealed class DatabaseCreation
{
    private readonly DatabaseTarget _target;
    internal DatabaseCreation(DatabaseTarget target) => _target = target;
    public DatabaseType Type => _target.Type;
    internal bool Matches(DatabaseTarget target) => ReferenceEquals(target, _target);
    public override string ToString() => nameof(DatabaseCreation);
}

/// <summary>Explicit create-only provisioning; no schema migrations, adoption, replacement or deletion.</summary>
public sealed class DatabaseProvisioner(DatabaseFactory factory)
{
    public Task<DatabaseCreation> CreateAsync(DatabaseTarget target, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        return target.Type == DatabaseType.Sqlite
            ? Task.Run(() => CreateCoreAsync(target, cancellationToken), cancellationToken)
            : CreateCoreAsync(target, cancellationToken);
    }

    private async Task<DatabaseCreation> CreateCoreAsync(DatabaseTarget target, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        try
        {
            if (target.Type == DatabaseType.Sqlite)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target.SqlitePath!)!);
                token.ThrowIfCancellationRequested();
                // CreateNew closes the existence-check race and never truncates an existing file.
                await using (new FileStream(target.SqlitePath!, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
                await using var connection = factory.CreateConnection(target);
                await connection.OpenAsync(token).ConfigureAwait(false);
                // Materialize a valid empty SQLite header, without claiming a schema/version or enabling WAL.
                await using var initialize = DatabaseFactory.Command(connection, target, "PRAGMA user_version = 0");
                await initialize.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
            else
            {
                await using var connection = factory.CreateMaintenanceConnection(target);
                await connection.OpenAsync(token).ConfigureAwait(false);
                await using var create = DatabaseFactory.Command(connection, target, DatabaseSql.CreateDatabase(target));
                await create.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
            token.ThrowIfCancellationRequested();
            return new DatabaseCreation(target);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception exception) when (exception is DbException or IOException or UnauthorizedAccessException)
        {
            var exists = exception switch
            {
                IOException io when (io.HResult & 0xFFFF) is 80 or 183 => true,
                Microsoft.Data.SqlClient.SqlException { Number: 1801 } => true,
                MySqlConnector.MySqlException { Number: 1007 } => true,
                Npgsql.PostgresException { SqlState: "42P04" } => true,
                _ => false
            };
            var code = DatabaseOperationException.Classify(exception);
            throw new DatabaseOperationException(exists ? DatabaseErrorCode.AlreadyExists
                : code == DatabaseErrorCode.AccessDenied ? code : DatabaseErrorCode.ProvisioningFailed);
        }
    }
}

using System.Data.Common;
using Microsoft.Data.Sqlite;

namespace WawiCallPro.Database;

public interface ISqliteDatabaseBackup
{
    Task CreateAsync(DatabaseTarget source, string destinationFile, CancellationToken cancellationToken = default);
    Task RestoreNewAsync(string backupFile, DatabaseTarget destination, CancellationToken cancellationToken = default);
}

/// <summary>Verified standalone snapshots via SQLite's online backup API. Existing destination files are never replaced.</summary>
public sealed class SqliteDatabaseBackup(DatabaseFactory factory, IDatabaseMigrationEngineFactory engines, IDatabaseMigrationLock gate) : ISqliteDatabaseBackup
{
    public Task CreateAsync(DatabaseTarget source, string destinationFile, CancellationToken cancellationToken = default)
    {
        RequireSqlite(source);
        var destination = DatabaseTarget.Sqlite(destinationFile, source.CommandTimeoutSeconds);
        return Task.Run(() => CopyAsync(source, destination, cancellationToken), cancellationToken);
    }

    public Task RestoreNewAsync(string backupFile, DatabaseTarget destination, CancellationToken cancellationToken = default)
    {
        RequireSqlite(destination);
        var source = DatabaseTarget.Sqlite(backupFile, destination.CommandTimeoutSeconds);
        return Task.Run(() => CopyAsync(source, destination, cancellationToken), cancellationToken);
    }

    private async Task CopyAsync(DatabaseTarget source, DatabaseTarget destination, CancellationToken token)
    {
        string? temporary = null;
        try
        {
            token.ThrowIfCancellationRequested();
            if (DestinationExists(destination)) throw new DatabaseOperationException(DatabaseErrorCode.AlreadyExists);
            await using var lease = await gate.AcquireAsync(source, TimeSpan.FromSeconds(30), token).ConfigureAwait(false);
            var status = new DatabaseStatusService(factory, engines.GetEngine(DatabaseType.Sqlite).Catalog);
            await VerifySchemaAsync(status, source, token).ConfigureAwait(false);
            Directory.CreateDirectory(Path.GetDirectoryName(destination.SqlitePath!)!);
            temporary = destination.SqlitePath + "." + Guid.NewGuid().ToString("N") + ".partial";
            await using (new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
            var staged = DatabaseTarget.Sqlite(temporary, destination.CommandTimeoutSeconds);
            await using (var input = (SqliteConnection)factory.CreateConnection(source, readOnly: true))
            await using (var output = (SqliteConnection)factory.CreateConnection(staged))
            {
                await input.OpenAsync(token).ConfigureAwait(false); await output.OpenAsync(token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                // Synchronous native operation: finish it on the worker before observing cancellation and publishing the file.
                input.BackupDatabase(output);
                await using var journal = output.CreateCommand(); journal.CommandText = "PRAGMA journal_mode=DELETE";
                await journal.ExecuteScalarAsync(token).ConfigureAwait(false);
                await using var integrity = output.CreateCommand(); integrity.CommandText = "PRAGMA integrity_check";
                if (!Equals(await integrity.ExecuteScalarAsync(token).ConfigureAwait(false), "ok")) throw new DatabaseOperationException(DatabaseErrorCode.InvalidSchema);
                await using var foreignKeys = output.CreateCommand(); foreignKeys.CommandText = "PRAGMA foreign_key_check";
                await using var reader = await foreignKeys.ExecuteReaderAsync(token).ConfigureAwait(false);
                if (await reader.ReadAsync(token).ConfigureAwait(false)) throw new DatabaseOperationException(DatabaseErrorCode.InvalidSchema);
            }
            await VerifySchemaAsync(status, staged, token).ConfigureAwait(false);
            await lease.EnsureHeldAsync(token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if (DestinationExists(destination)) throw new DatabaseOperationException(DatabaseErrorCode.AlreadyExists);
            File.Move(temporary, destination.SqlitePath!, overwrite: false);
            temporary = null;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (DatabaseOperationException) { throw; }
        catch (Exception exception) when (exception is DbException or IOException or UnauthorizedAccessException)
        {
            if (DestinationExists(destination)) throw new DatabaseOperationException(DatabaseErrorCode.AlreadyExists);
            throw new DatabaseOperationException(DatabaseOperationException.Classify(exception) == DatabaseErrorCode.AccessDenied ? DatabaseErrorCode.AccessDenied : DatabaseErrorCode.BackupFailed);
        }
        finally
        {
            // Only this operation's unpredictable temporary files are owned by this cleanup.
            if (temporary is not null)
                foreach (var file in new[] { temporary, temporary + "-wal", temporary + "-shm", temporary + "-journal" })
                    try { File.Delete(file); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    private static async Task VerifySchemaAsync(DatabaseStatusService service, DatabaseTarget target, CancellationToken token)
    {
        var status = await service.GetStatusAsync(target, token).ConfigureAwait(false);
        if (status.State is not (DatabaseSchemaState.Current or DatabaseSchemaState.MigrationRequired)) throw new DatabaseOperationException(status.Error ?? DatabaseErrorCode.InvalidSchema);
    }
    private static void RequireSqlite(DatabaseTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target.Type != DatabaseType.Sqlite) throw new DatabaseOperationException(DatabaseErrorCode.InvalidTarget);
    }

    private static bool DestinationExists(DatabaseTarget target) => Directory.Exists(target.SqlitePath) ||
        new[] { "", "-wal", "-shm", "-journal" }.Any(suffix => File.Exists(target.SqlitePath + suffix));
}

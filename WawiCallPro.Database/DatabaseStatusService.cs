using System.Collections.Immutable;
using System.Data.Common;
using WawiCallPro.Database.Contracts;

namespace WawiCallPro.Database;

/// <summary>Reads metadata directly, without provisioning or resolving a migration runner/version loader.</summary>
public sealed class DatabaseStatusService(DatabaseFactory factory, DatabaseMigrationCatalog catalog)
{
    public Task<DatabaseMigrationStatus> GetStatusAsync(DatabaseTarget target, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        // Microsoft.Data.Sqlite executes its async methods synchronously; keep that work off a caller's UI thread.
        return target.Type == DatabaseType.Sqlite
            ? Task.Run(() => ReadAsync(target, cancellationToken), cancellationToken)
            : ReadAsync(target, cancellationToken);
    }

    private async Task<DatabaseMigrationStatus> ReadAsync(DatabaseTarget target, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        try
        {
            if (target.Type == DatabaseType.Sqlite)
            {
                try
                {
                    using var probe = new FileStream(target.SqlitePath!, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                }
                catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
                {
                    return Result(target, DatabaseSchemaState.MissingDatabase, error: DatabaseErrorCode.MissingDatabase);
                }
            }
            else
            {
                await using var maintenance = factory.CreateMaintenanceConnection(target);
                await maintenance.OpenAsync(token).ConfigureAwait(false);
                await using var check = DatabaseFactory.Command(maintenance, target, DatabaseSql.DatabaseExists(target.Type));
                var name = check.CreateParameter();
                name.ParameterName = "@name";
                name.Value = target.DatabaseName;
                check.Parameters.Add(name);
                if (await check.ExecuteScalarAsync(token).ConfigureAwait(false) is null)
                {
                    // SQL Server/MySQL catalog visibility depends on permissions. Do not report a proven absence.
                    return target.Type == DatabaseType.PostgreSql
                        ? Result(target, DatabaseSchemaState.MissingDatabase, error: DatabaseErrorCode.MissingDatabase)
                        : Result(target, DatabaseSchemaState.MissingOrInaccessible, error: DatabaseErrorCode.MissingOrInaccessible);
                }
            }

            await using var connection = factory.CreateConnection(target, readOnly: true);
            await connection.OpenAsync(token).ConfigureAwait(false);
            return await ReadMetadataAsync(connection, target, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (DatabaseOperationException exception)
        {
            return Result(target, StateFor(exception.Code), error: exception.Code);
        }
        catch (Exception exception) when (exception is DbException or IOException or UnauthorizedAccessException)
        {
            var code = DatabaseOperationException.Classify(exception);
            return Result(target, StateFor(code), error: code);
        }
    }

    private async Task<DatabaseMigrationStatus> ReadMetadataAsync(DbConnection connection, DatabaseTarget target, CancellationToken token)
    {
        var objects = new HashSet<(string Schema, string Name)>();
        await using (var command = DatabaseFactory.Command(connection, target, DatabaseSql.Objects(target.Type)))
        await using (var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(token).ConfigureAwait(false))
            {
                if (objects.Count >= 2048) throw Invalid();
                objects.Add((reader.GetString(0), reader.GetString(1)));
            }
        }

        var schema = DatabaseSql.Schema(target);
        if (objects.Any(item => !string.Equals(item.Schema, schema, StringComparison.Ordinal)))
            return Result(target, DatabaseSchemaState.ForeignDatabase, error: DatabaseErrorCode.ForeignDatabase);
        var hasOwner = objects.Contains((schema, CommunicationDatabaseIdentity.TableName));
        var hasVersions = objects.Contains((schema, "versioninfo"));
        if (!hasOwner)
        {
            if (objects.Count == 0 || objects.Count == 1 && hasVersions && (await ReadVersionsAsync(connection, target, token).ConfigureAwait(false)).IsEmpty)
                return Result(target, DatabaseSchemaState.EmptyDatabase, pending: catalog.Versions);
            return Result(target, DatabaseSchemaState.ForeignDatabase, error: DatabaseErrorCode.ForeignDatabase);
        }

        try
        {
            await using var owner = DatabaseFactory.Command(connection, target,
                "SELECT applicationid, formatversion, createdatutc FROM " + DatabaseSql.Table(target.Type, CommunicationDatabaseIdentity.TableName));
            await using var reader = await owner.ExecuteReaderAsync(token).ConfigureAwait(false);
            if (!await reader.ReadAsync(token).ConfigureAwait(false)) throw Invalid();
            if (!string.Equals(reader.GetString(0), CommunicationDatabaseIdentity.ApplicationId, StringComparison.Ordinal))
                return Result(target, DatabaseSchemaState.ForeignDatabase, error: DatabaseErrorCode.ForeignDatabase);
            var format = Integer(reader.GetValue(1));
            _ = DatabaseUtc.Decode(Integer(reader.GetValue(2)));
            if (await reader.ReadAsync(token).ConfigureAwait(false) || format < 1) throw Invalid();
            if (format > CommunicationDatabaseIdentity.FormatVersion)
                return Result(target, DatabaseSchemaState.NewerSchema, error: DatabaseErrorCode.NewerSchema);
        }
        catch (Exception exception) when (exception is InvalidCastException or ArgumentOutOfRangeException)
        {
            throw Invalid();
        }
        catch (DbException exception) when (IsMetadataShapeError(exception)) { throw Invalid(); }

        var applied = hasVersions ? await ReadVersionsAsync(connection, target, token).ConfigureAwait(false) : [];
        if (catalog.LatestVersion is null)
            return Result(target, DatabaseSchemaState.MigrationsUnavailable, applied, error: DatabaseErrorCode.MigrationDefinitionsMissing);
        if (applied.Any(version => version > catalog.LatestVersion))
            return Result(target, DatabaseSchemaState.NewerSchema, applied, error: DatabaseErrorCode.NewerSchema);
        if (!applied.SequenceEqual(catalog.Versions.Take(applied.Length))) throw Invalid();
        var pending = catalog.Versions.Skip(applied.Length).ToImmutableArray();
        return Result(target, pending.IsEmpty ? DatabaseSchemaState.Current : DatabaseSchemaState.MigrationRequired, applied, pending);
    }

    private static async Task<ImmutableArray<long>> ReadVersionsAsync(DbConnection connection, DatabaseTarget target, CancellationToken token)
    {
        try
        {
            var versions = ImmutableArray.CreateBuilder<long>();
            await using var command = DatabaseFactory.Command(connection, target, "SELECT version FROM " + DatabaseSql.Table(target.Type, "versioninfo") + " ORDER BY version");
            await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            while (await reader.ReadAsync(token).ConfigureAwait(false))
            {
                var value = Integer(reader.GetValue(0));
                if (versions.Count >= 1024 || value <= 0 || versions.Count > 0 && value <= versions[^1]) throw Invalid();
                versions.Add(value);
            }
            return versions.ToImmutable();
        }
        catch (DbException exception) when (IsMetadataShapeError(exception)) { throw Invalid(); }
    }

    private static long Integer(object value) => value switch { long number => number, int number => number, _ => throw Invalid() };

    private static bool IsMetadataShapeError(DbException exception) => exception switch
    {
        Microsoft.Data.Sqlite.SqliteException { SqliteErrorCode: 1 } => true,
        Microsoft.Data.SqlClient.SqlException { Number: 207 or 208 } => true,
        Npgsql.PostgresException { SqlState: "42703" or "42P01" } => true,
        MySqlConnector.MySqlException { Number: 1054 or 1146 } => true,
        _ => false
    };

    private DatabaseMigrationStatus Result(DatabaseTarget target, DatabaseSchemaState state, ImmutableArray<long> applied = default,
        ImmutableArray<long> pending = default, DatabaseErrorCode? error = null) => new(target.Type, state,
        applied.IsDefault ? [] : applied, pending.IsDefault ? [] : pending, catalog.LatestVersion, error);

    private static DatabaseOperationException Invalid() => new(DatabaseErrorCode.InvalidSchema);
    private static DatabaseSchemaState StateFor(DatabaseErrorCode code) => code switch
    {
        DatabaseErrorCode.ForeignDatabase => DatabaseSchemaState.ForeignDatabase,
        DatabaseErrorCode.InvalidSchema => DatabaseSchemaState.InvalidSchema,
        _ => DatabaseSchemaState.ConnectionFailed
    };
}

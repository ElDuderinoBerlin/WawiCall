using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using WawiCallPro.Database;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Operations;
using WawiCallPro.Utilities.Security;

namespace WawiCallPro.Utilities.Persistence;

/// <summary>Detached editor values. The connection string is intentionally excluded from diagnostic serialization.</summary>
public sealed class DatabaseSetupDraft
{
    public DatabaseType Type { get; init; }
    public string? SqlitePath { get; init; }
    [JsonIgnore] public string? ConnectionString { get; init; }
    public override string ToString() => nameof(DatabaseSetupDraft);
}

public sealed record DatabaseConnectionTestResult(bool Success, string Message);

public interface ICommunicationDatabaseFactory
{
    ICommunicationDatabase Create(DatabaseTarget target);
}

/// <summary>Creates a facade for one explicit candidate target, independently of the host's active startup target.</summary>
public sealed class CommunicationDatabaseFactory(DatabaseFactory connections, IDatabaseMigrationEngineFactory engines,
    IDatabaseMigrationLock gate, IDatabaseMigrationPermissions permissions, IWorkstationIdentity workstation,
    ISqliteDatabaseBackup backup, ILogger<DatabaseMigrator> logger) : ICommunicationDatabaseFactory
{
    public ICommunicationDatabase Create(DatabaseTarget target) => new CommunicationDatabase(new FixedTarget(target), connections,
        engines, gate, permissions, workstation, backup, logger);
    private sealed class FixedTarget(DatabaseTarget target) : IDatabaseTargetSource { public DatabaseTarget GetTarget() => target; }
}

public interface IDatabaseSetupService
{
    Task<ConfigurationSnapshot> LoadAsync(CancellationToken cancellationToken = default);
    DatabaseSetupDraft ReadDraft(ConfigurationSnapshot snapshot);
    Task<DatabaseConnectionTestResult> TestAsync(DatabaseSetupDraft draft, CancellationToken cancellationToken = default);
    Task<DatabaseMigrationStatus> InspectAsync(DatabaseSetupDraft draft, CancellationToken cancellationToken = default);
    Task<DatabaseMigrationStatus> InitializeAsync(DatabaseSetupDraft draft, bool approveEmpty,
        IProgress<DatabaseMigrationProgress>? progress = null, CancellationToken cancellationToken = default);
    Task SaveAsync(DatabaseSetupDraft draft, ConfigurationSnapshot expected, CancellationToken cancellationToken = default);
}

/// <summary>Wizard workflow using existing ownership checks and migrations. No schema definition or SQL lives in the UI.</summary>
public sealed class DatabaseSetupService(ConfigurationStore settings, ApplicationPaths paths, ISecretProtector secrets,
    DatabaseFactory connections, ICommunicationDatabaseFactory databases, IWorkstationIdentity workstation) : IDatabaseSetupService
{
    public Task<ConfigurationSnapshot> LoadAsync(CancellationToken cancellationToken = default) => settings.LoadAsync(cancellationToken);
    public DatabaseSetupDraft ReadDraft(ConfigurationSnapshot snapshot)
    {
        var database = snapshot.Options.Database;
        if (database.Provider == CommunicationDatabaseProvider.Sqlite)
            return new() { Type = DatabaseType.Sqlite, SqlitePath = Path.GetFullPath(Path.Combine(paths.RootDirectory, database.SqliteFileName ?? "communications.db")) };
        using var secret = secrets.Unprotect(database.ConnectionString!, CommunicationDatabaseSettings.ConnectionStringPurpose);
        return new() { Type = (DatabaseType)database.Provider, ConnectionString = secret.Reveal() };
    }
    private static DatabaseTarget Target(DatabaseSetupDraft draft) => draft.Type == DatabaseType.Sqlite
        ? DatabaseTarget.Sqlite(draft.SqlitePath!) : DatabaseTarget.Server(draft.Type, draft.ConnectionString!);

    public async Task<DatabaseConnectionTestResult> TestAsync(DatabaseSetupDraft draft, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var target = Target(draft);
        if (draft.Type == DatabaseType.Sqlite && !File.Exists(draft.SqlitePath))
            return new(true, "Der Dateipfad ist gültig. Die Datei wird erst beim Einrichten angelegt.");
        try
        {
            await using var connection = connections.CreateProbeConnection(target);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return new(true, "Verbindung erfolgreich. Das Datenbankschema wird beim Einrichten geprüft.");
        }
        catch (OperationCanceledException) { throw; }
        catch { return new(false, "Die Verbindung konnte nicht hergestellt werden. Server, Zugangsdaten und Verschlüsselung prüfen."); }
    }
    public Task<DatabaseMigrationStatus> InspectAsync(DatabaseSetupDraft draft, CancellationToken cancellationToken = default) =>
        databases.Create(Target(draft)).GetStatusAsync(cancellationToken);

    public async Task<DatabaseMigrationStatus> InitializeAsync(DatabaseSetupDraft draft, bool approveEmpty,
        IProgress<DatabaseMigrationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var database = databases.Create(Target(draft));
        var status = await database.GetStatusAsync(cancellationToken).ConfigureAwait(false);
        if (status.State == DatabaseSchemaState.EmptyDatabase && !approveEmpty)
            throw new DatabaseOperationException(DatabaseErrorCode.OwnershipUnverified);
        status = status.State switch
        {
            DatabaseSchemaState.MissingDatabase or DatabaseSchemaState.MissingOrInaccessible =>
                await database.CreateAndMigrateAsync(progress, cancellationToken).ConfigureAwait(false),
            _ => await database.MigrateAsync(approveEmpty, progress, cancellationToken).ConfigureAwait(false)
        };
        if (status.State == DatabaseSchemaState.Current) await workstation.InitializeAsync(cancellationToken).ConfigureAwait(false);
        return status;
    }
    public Task SaveAsync(DatabaseSetupDraft draft, ConfigurationSnapshot expected, CancellationToken cancellationToken = default)
    {
        _ = Target(draft);
        var database = draft.Type == DatabaseType.Sqlite
            ? new CommunicationDatabaseSettings { Provider = CommunicationDatabaseProvider.Sqlite, SqliteFileName = Path.GetFullPath(draft.SqlitePath!) }
            : new CommunicationDatabaseSettings { Provider = (CommunicationDatabaseProvider)draft.Type,
                ConnectionString = secrets.Protect(draft.ConnectionString!, CommunicationDatabaseSettings.ConnectionStringPurpose) };
        return settings.SaveAsync(expected.Options with { Database = database }, expected, cancellationToken);
    }
}

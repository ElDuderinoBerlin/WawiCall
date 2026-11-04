using System.Collections.Immutable;

namespace WawiCallPro.Database;

public enum DatabaseSchemaState
{
    MissingDatabase,
    MissingOrInaccessible,
    EmptyDatabase,
    MigrationsUnavailable,
    MigrationRequired,
    Current,
    NewerSchema,
    ForeignDatabase,
    InvalidSchema,
    ConnectionFailed
}

/// <summary>Allowlisted metadata only. No database name, path, connection string or provider message.</summary>
public sealed record DatabaseMigrationStatus(DatabaseType Type, DatabaseSchemaState State,
    ImmutableArray<long> AppliedVersions, ImmutableArray<long> PendingVersions, long? LatestAvailableVersion,
    DatabaseErrorCode? Error = null);

public enum DatabaseMigrationPhase
{
    Inspecting,
    Applying,
    Verifying,
    Completed,
    Failed,
    Cancelled
}

/// <summary>Progress reports fixed phases and numeric versions rather than raw migration/provider messages.</summary>
public sealed record DatabaseMigrationProgress(DatabaseMigrationPhase Phase, long? Version, int CompletedSteps, int TotalSteps);

/// <summary>Version-one ownership metadata to be created by the first owned schema migration.</summary>
public static class CommunicationDatabaseIdentity
{
    public const string TableName = "wawicallpro_metadata";
    public const string ApplicationId = "WawiCallPro.Communications";
    public const int FormatVersion = 1;
}

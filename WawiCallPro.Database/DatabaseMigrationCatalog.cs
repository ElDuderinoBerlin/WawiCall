using System.Collections.Immutable;

namespace WawiCallPro.Database;

/// <summary>The actual available migration versions, not the versions merely planned in documentation.</summary>
public sealed class DatabaseMigrationCatalog
{
    public DatabaseMigrationCatalog(IEnumerable<long> versions)
    {
        ArgumentNullException.ThrowIfNull(versions);
        Versions = versions.Order().ToImmutableArray();
        if (Versions.Any(version => version <= 0) || Versions.Distinct().Count() != Versions.Length)
            throw new ArgumentException("Migration versions must be positive and unique.", nameof(versions));
    }

    public ImmutableArray<long> Versions { get; }
    public long? LatestVersion => Versions.IsEmpty ? null : Versions[^1];
}

/// <summary>
/// Adapter boundary for the standard FluentMigrator runner. One synchronous call must finish before returning.
/// The future adapter must defer runner creation until MigrateUp, after the ownership/status preflight.
/// </summary>
public interface IDatabaseMigrationEngine
{
    DatabaseMigrationCatalog Catalog { get; }
    void MigrateUp(DatabaseTarget target, long version);
}

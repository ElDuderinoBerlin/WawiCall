using System.Collections.Concurrent;

namespace WawiCallPro.Database;

public interface IDatabaseTargetSource { DatabaseTarget GetTarget(); }
public interface IDatabaseMigrationEngineFactory { IDatabaseMigrationEngine GetEngine(DatabaseType type); }

/// <summary>Caches immutable provider catalogs; resolving an engine does not create a runner or open a connection.</summary>
public sealed class DatabaseMigrationEngineFactory : IDatabaseMigrationEngineFactory
{
    private readonly ConcurrentDictionary<DatabaseType, IDatabaseMigrationEngine> _engines = new();
    public IDatabaseMigrationEngine GetEngine(DatabaseType type) => _engines.GetOrAdd(type, value => new FluentMigratorEngine(value));
}

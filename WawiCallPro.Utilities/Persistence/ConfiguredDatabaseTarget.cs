using WawiCallPro.Database;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Operations;
using WawiCallPro.Utilities.Security;

namespace WawiCallPro.Utilities.Persistence;

/// <summary>Resolves only after configuration startup. Constructing the DI graph never opens a connection.</summary>
public sealed class ConfiguredDatabaseTarget(ApplicationConfigurationService configuration, ApplicationPaths paths, ISecretProtector secrets) : IDatabaseTargetSource
{
    private DatabaseTarget? _target;
    private readonly object _gate = new();
    public DatabaseTarget GetTarget()
    {
        lock (_gate)
        {
            if (_target is not null) return _target;
            var settings = configuration.Current.Options.Database;
            if (settings.Provider == CommunicationDatabaseProvider.Sqlite)
                return _target = DatabaseTarget.Sqlite(Path.Combine(paths.RootDirectory, settings.SqliteFileName ?? "communications.db"));
            if (settings.ConnectionString is null) throw new DatabaseOperationException(DatabaseErrorCode.InvalidTarget);
            using var connection = secrets.Unprotect(settings.ConnectionString, CommunicationDatabaseSettings.ConnectionStringPurpose);
            var type = settings.Provider switch
            {
                CommunicationDatabaseProvider.SqlServer => DatabaseType.SqlServer,
                CommunicationDatabaseProvider.MySql => DatabaseType.MySql,
                CommunicationDatabaseProvider.PostgreSql => DatabaseType.PostgreSql,
                _ => throw new DatabaseOperationException(DatabaseErrorCode.InvalidTarget)
            };
            return _target = DatabaseTarget.Server(type, connection.Reveal());
        }
    }
}

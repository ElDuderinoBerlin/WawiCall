using System.Collections.Immutable;
using System.Data.Common;
using System.Reflection;
using FluentMigrator;
using FluentMigrator.Runner;
using FluentMigrator.Runner.Infrastructure;
using FluentMigrator.Runner.Initialization;
using FluentMigrator.Runner.Processors.MySql;
using FluentMigrator.Runner.Processors.SQLite;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;

namespace WawiCallPro.Database;

/// <summary>
/// Standard FluentMigrator backend for DatabaseMigrator, which owns the status/ownership preflight.
/// Catalog discovery neither constructs migrations nor creates a runner or database connection.
/// </summary>
public sealed class FluentMigratorEngine : IDatabaseMigrationEngine
{
    private readonly ImmutableArray<Type> _migrationTypes;
    private readonly ILogger _logger;
    private readonly string _tag;

    public FluentMigratorEngine(DatabaseType type, ILoggerFactory? loggerFactory = null)
        : this(type, typeof(FluentMigratorEngine).Assembly, "WawiCallPro.Database.Migrations", loggerFactory) { }

    // Test migrations live in their own assembly/namespace and cannot enter the product catalog.
    internal FluentMigratorEngine(DatabaseType type, Assembly assembly, string migrationNamespace, ILoggerFactory? loggerFactory = null)
    {
        Type = type;
        _tag = type switch
        {
            DatabaseType.Sqlite => "SQLite",
            DatabaseType.SqlServer => "SqlServer",
            DatabaseType.MySql => "MySQL",
            DatabaseType.PostgreSql => "PostgreSQL",
            _ => throw new DatabaseOperationException(DatabaseErrorCode.InvalidTarget)
        };
        _logger = loggerFactory?.CreateLogger<FluentMigratorEngine>() ?? NullLogger<FluentMigratorEngine>.Instance;
        var conventions = DefaultMigrationRunnerConventions.Instance;
        _migrationTypes = assembly.ExportedTypes
            .Where(candidate => !candidate.IsAbstract && !candidate.ContainsGenericParameters
                && candidate.Namespace == migrationNamespace && conventions.TypeIsMigration(candidate)
                && conventions.HasRequestedTags(candidate, [_tag], includeUntagged: true))
            .ToImmutableArray();
        Catalog = new DatabaseMigrationCatalog(_migrationTypes.Select(candidate => candidate.GetCustomAttribute<MigrationAttribute>()!.Version));
    }

    public DatabaseType Type { get; }
    public DatabaseMigrationCatalog Catalog { get; }

    /// <summary>Runs through a catalog version synchronously; call through DatabaseMigrator after preflight.</summary>
    public void MigrateUp(DatabaseTarget target, long version)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target.Type != Type) throw new DatabaseOperationException(DatabaseErrorCode.InvalidTarget);
        if (Catalog.Versions.IsEmpty) throw new DatabaseOperationException(DatabaseErrorCode.MigrationDefinitionsMissing);
        if (!Catalog.Versions.Contains(version)) throw new DatabaseOperationException(DatabaseErrorCode.InvalidSchema);
        try
        {
            using var provider = BuildServices(target);
            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<IMigrationRunner>().MigrateUp(version);
        }
        catch (Exception exception)
        {
            // Runner exceptions can contain SQL, connection strings and migration-supplied messages.
            throw new DatabaseOperationException(DatabaseOperationException.MigrationCode(exception));
        }
    }

    internal ServiceProvider BuildServices(DatabaseTarget target)
    {
        if (target.Type != Type) throw new DatabaseOperationException(DatabaseErrorCode.InvalidTarget);
        var services = new ServiceCollection();
        services.AddSingleton(typeof(DatabaseType), Type);
        services.AddFluentMigratorCore().ConfigureRunner(builder =>
        {
            switch (Type)
            {
                case DatabaseType.Sqlite:
                    builder.AddSQLite();
                    services.AddScoped<SQLiteDbFactory, MicrosoftSqliteDbFactory>();
                    break;
                case DatabaseType.SqlServer:
                    builder.AddSqlServer2016();
                    break;
                case DatabaseType.MySql:
                    builder.AddMySql8();
                    services.AddScoped<MySqlDbFactory, ConnectorMySqlDbFactory>();
                    break;
                case DatabaseType.PostgreSql:
                    builder.AddPostgres15_0();
                    break;
                default:
                    throw new DatabaseOperationException(DatabaseErrorCode.InvalidTarget);
            }
            builder.WithGlobalConnectionString(target.ConnectionString)
                .WithGlobalCommandTimeout(TimeSpan.FromSeconds(target.CommandTimeoutSeconds))
                .WithRunnerConventions(DefaultMigrationRunnerConventions.Instance)
                .WithVersionTable(new CommunicationVersionTable(Type));
        });
        // Supply only the selected concrete types. No assembly scan can pick up plugin migrations,
        // profiles, maintenance migrations or foreign version-table metadata.
        services.AddSingleton<IMigrationSourceItem>(new MigrationTypes(_migrationTypes));
        services.Configure<RunnerOptions>(options =>
        {
            options.Tags = [_tag];
            options.IncludeUntaggedMigrations = true;
        });
        services.AddLogging(logging => logging.ClearProviders().AddProvider(new SafeRunnerLoggerProvider(_logger)));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    private sealed class MigrationTypes(ImmutableArray<Type> types) : IMigrationSourceItem
    {
        public IEnumerable<Type> MigrationTypeCandidates { get; } = types;
    }

    private sealed class MicrosoftSqliteDbFactory(IServiceProvider services) : SQLiteDbFactory(services)
    {
        protected override DbProviderFactory CreateFactory() => SqliteFactory.Instance;
    }

    private sealed class ConnectorMySqlDbFactory(IServiceProvider services) : MySqlDbFactory(services)
    {
        protected override DbProviderFactory CreateFactory() => MySqlConnectorFactory.Instance;
    }
}

using System.Data;
using System.Security.Cryptography;
using FluentMigrator;
using FluentMigrator.Expressions;
using FluentMigrator.Model;
using FluentMigrator.Runner;
using FluentMigrator.Runner.Processors;
using FluentMigrator.Runner.Processors.MySql;
using FluentMigrator.Runner.Processors.Postgres;
using FluentMigrator.Runner.Processors.SQLite;
using FluentMigrator.Runner.VersionTableInfo;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MySqlConnector;
using Npgsql;

namespace WawiCallPro.Database.Tests;

[TestClass]
public sealed class FluentMigratorEngineTests
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WawiCallPro-runner-" + Guid.NewGuid().ToString("N"));
    private readonly DatabaseFactory _factory = new();
    private const string FixtureNamespace = "WawiCallPro.Database.Tests.RunnerFixtures";
    private const string Secret = "private-runner-sentinel";

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [TestMethod]
    public async Task ProductCatalogContainsOnlyProductVersionsAndNeverCreatesADatabase()
    {
        var target = Target();
        var engine = new FluentMigratorEngine(DatabaseType.Sqlite);
        Assert.AreSame(engine.Catalog, engine.Catalog);
        CollectionAssert.AreEqual(new long[] { 1, 2, 3, 4, 5 }, engine.Catalog.Versions.ToArray());
        var status = await new DatabaseStatusService(_factory, engine.Catalog).GetStatusAsync(target);
        Assert.AreEqual(DatabaseSchemaState.MissingDatabase, status.State);
        var failure = await Assert.ThrowsExactlyAsync<DatabaseOperationException>(() => new DatabaseMigrator(_factory, engine).MigrateAsync(target));
        Assert.AreEqual(DatabaseErrorCode.MissingDatabase, failure.Code);
        Assert.IsFalse(Directory.Exists(_root));
    }

    [TestMethod]
    [DataRow(DatabaseType.Sqlite, "SQLiteGenerator", "")]
    [DataRow(DatabaseType.SqlServer, "SqlServer2016Generator", "dbo")]
    [DataRow(DatabaseType.MySql, "MySql8Generator", "")]
    [DataRow(DatabaseType.PostgreSql, "Postgres15_0Generator", "public")]
    public void EachProviderResolvesItsRunnerGeneratorMetadataAndOptionsWithoutOpeningADatabase(DatabaseType type, string generatorName, string schema)
    {
        var target = type switch
        {
            DatabaseType.Sqlite => Target(),
            DatabaseType.SqlServer => DatabaseTarget.Server(type, "Server=localhost,1;Database=runner_fixture;User ID=test;Password=" + Secret, 1, 7),
            DatabaseType.MySql => DatabaseTarget.Server(type, "Server=localhost;Port=1;Database=runner_fixture;User ID=test;Password=" + Secret, 1, 7),
            _ => DatabaseTarget.Server(type, "Host=localhost;Port=1;Database=runner_fixture;Username=test;Password=" + Secret, 1, 7)
        };
        var engine = Engine(type);
        using var provider = engine.BuildServices(target);
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;
        var generator = services.GetRequiredService<IMigrationGenerator>();
        Assert.AreEqual(generatorName, generator.GetType().Name);
        var processor = services.GetRequiredService<IMigrationProcessor>();
        Assert.AreEqual(generatorName.Replace("Generator", "Processor", StringComparison.Ordinal), processor.GetType().Name);
        var expression = new CreateTableExpression { TableName = "fixture_table", SchemaName = schema };
        expression.Columns.Add(new ColumnDefinition { Name = "id", Type = DbType.Int64, IsNullable = false });
        var sql = generator.Generate(expression);
        Assert.Contains("fixture_table", sql);
        Assert.Contains("id", sql);
        var metadata = services.GetRequiredService<IVersionTableMetaData>();
        Assert.AreEqual(schema, metadata.SchemaName);
        Assert.AreEqual("versioninfo", metadata.TableName);
        Assert.AreEqual("version", metadata.ColumnName);
        Assert.AreEqual("appliedon", metadata.AppliedOnColumnName);
        Assert.AreEqual("description", metadata.DescriptionColumnName);
        Assert.IsFalse(metadata.OwnsSchema);
        var options = services.GetRequiredService<IOptionsSnapshot<ProcessorOptions>>().Value;
        Assert.AreEqual(TimeSpan.FromSeconds(target.CommandTimeoutSeconds), options.Timeout);
        Assert.IsFalse(options.PreviewOnly);
        var migrations = services.GetRequiredService<IMigrationInformationLoader>().LoadMigrations();
        CollectionAssert.AreEqual(engine.Catalog.Versions.ToArray(), migrations.Keys.ToArray());
        Assert.AreEqual(type is DatabaseType.Sqlite or DatabaseType.PostgreSql ? 2 : 1, migrations.Count);
        if (type != DatabaseType.SqlServer)
        {
            var dbFactory = type switch
            {
                DatabaseType.Sqlite => (DbFactoryBase)services.GetRequiredService<SQLiteDbFactory>(),
                DatabaseType.MySql => services.GetRequiredService<MySqlDbFactory>(),
                _ => services.GetRequiredService<PostgresDbFactory>()
            };
            using var connection = dbFactory.Factory.CreateConnection()!;
            connection.ConnectionString = target.ConnectionString;
            Assert.AreEqual(type switch
            {
                DatabaseType.Sqlite => typeof(SqliteConnection),
                DatabaseType.MySql => typeof(MySqlConnection),
                _ => typeof(NpgsqlConnection)
            }, connection.GetType());
            Assert.AreEqual(ConnectionState.Closed, connection.State);
        }
        Assert.IsFalse(Directory.Exists(_root));
    }

    [TestMethod]
    public async Task RealSqliteRunnerPersistsLowercaseVersionsUnicodeAndIsIdempotent()
    {
        var target = Target();
        await new DatabaseProvisioner(_factory).CreateAsync(target);
        var engine = Engine(DatabaseType.Sqlite);
        var statusService = new DatabaseStatusService(_factory, engine.Catalog);
        var before = Hash(target);
        Assert.AreEqual(DatabaseSchemaState.EmptyDatabase, (await statusService.GetStatusAsync(target)).State);
        Assert.AreEqual(before, Hash(target));
        var migrator = new DatabaseMigrator(_factory, engine);
        var status = await migrator.MigrateAsync(target);
        Assert.AreEqual(DatabaseSchemaState.Current, status.State);
        CollectionAssert.AreEqual(new long[] { 1, 2 }, status.AppliedVersions.ToArray());
        Assert.AreEqual("Grüße 电话", Scalar(target, "SELECT value FROM fixture_data WHERE id=1"));
        Assert.AreEqual(2L, Scalar(target, "SELECT COUNT(*) FROM fixture_data"));
        Assert.AreEqual("version,appliedon,description", Scalar(target, "SELECT group_concat(name,',') FROM pragma_table_info('versioninfo')"));
        Assert.AreEqual(2L, Scalar(target, "SELECT COUNT(*) FROM versioninfo WHERE appliedon IS NOT NULL AND description IS NOT NULL"));
        Assert.AreEqual(1L, Scalar(target, "SELECT COUNT(*) FROM pragma_index_list('versioninfo') WHERE name='uc_versioninfo_version' AND \"unique\"=1"));
        Assert.AreEqual(0L, Scalar(target, "SELECT COUNT(*) FROM sqlite_schema WHERE name='VersionInfo'"));
        before = Hash(target);
        Assert.AreEqual(DatabaseSchemaState.Current, (await migrator.MigrateAsync(target)).State);
        Assert.AreEqual(before, Hash(target), "An up-to-date database must not instantiate a writing runner.");
        using var exclusive = File.Open(target.SqlitePath!, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    [TestMethod]
    public async Task RealRunnerFailureRollsBackTheStepKeepsConfirmedVersionsAndRedactsSqlAndErrors()
    {
        var target = Target();
        await new DatabaseProvisioner(_factory).CreateAsync(target);
        var log = new LogProbe();
        using var logging = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Trace).AddProvider(log));
        var engine = new FluentMigratorEngine(DatabaseType.Sqlite, GetType().Assembly,
            "WawiCallPro.Database.Tests.FailingRunnerFixtures", logging);
        var migrator = new DatabaseMigrator(_factory, engine, logging.CreateLogger<DatabaseMigrator>());
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var failure = await Assert.ThrowsExactlyAsync<DatabaseOperationException>(() => migrator.MigrateAsync(target));
            Assert.AreEqual(DatabaseErrorCode.MigrationFailed, failure.Code);
            Assert.IsNull(failure.InnerException);
            Assert.DoesNotContain(Secret, failure.ToString());
            var status = await new DatabaseStatusService(_factory, engine.Catalog).GetStatusAsync(target);
            Assert.AreEqual(DatabaseSchemaState.MigrationRequired, status.State);
            CollectionAssert.AreEqual(new long[] { 1 }, status.AppliedVersions.ToArray());
            Assert.AreEqual(0L, Scalar(target, "SELECT COUNT(*) FROM sqlite_schema WHERE name='fixture_unfinished'"));
        }
        Assert.IsTrue(log.Messages.Any(message => message.Contains("Database migration runner reported", StringComparison.Ordinal)));
        foreach (var message in log.Messages)
        {
            Assert.DoesNotContain(Secret, message);
            Assert.DoesNotContain("nonexistent_fixture_table", message);
            Assert.DoesNotContain(_root, message);
            Assert.DoesNotContain("CREATE TABLE", message);
        }
        using var exclusive = File.Open(target.SqlitePath!, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    [TestMethod]
    public async Task RealRunnerIsNeverConstructedForForeignOrNewerTargets()
    {
        var target = Target();
        await new DatabaseProvisioner(_factory).CreateAsync(target);
        var engine = Engine(DatabaseType.Sqlite);
        await new DatabaseMigrator(_factory, engine).MigrateAsync(target);
        Execute(target, "INSERT INTO versioninfo(version) VALUES (999)");
        var before = Hash(target);
        var failure = await Assert.ThrowsExactlyAsync<DatabaseOperationException>(() => new DatabaseMigrator(_factory, engine).MigrateAsync(target));
        Assert.AreEqual(DatabaseErrorCode.NewerSchema, failure.Code);
        Assert.AreEqual(before, Hash(target));
        Execute(target, "DROP TABLE wawicallpro_metadata");
        before = Hash(target);
        failure = await Assert.ThrowsExactlyAsync<DatabaseOperationException>(() => new DatabaseMigrator(_factory, engine).MigrateAsync(target));
        Assert.AreEqual(DatabaseErrorCode.ForeignDatabase, failure.Code);
        Assert.AreEqual(before, Hash(target));
    }

    [TestMethod]
    public void CatalogDiscoveryDoesNotConstructSelectedMigrationTypes()
    {
        var engine = new FluentMigratorEngine(DatabaseType.Sqlite, GetType().Assembly,
            "WawiCallPro.Database.Tests.ConstructorRunnerFixtures");
        CollectionAssert.AreEqual(new long[] { 41 }, engine.Catalog.Versions.ToArray());
        Assert.IsFalse(Directory.Exists(_root));
    }

    [TestMethod]
    public async Task FailedFirstMigrationLeavesOnlyEmptyRunnerMetadataAndCanBeRetried()
    {
        var target = Target();
        await new DatabaseProvisioner(_factory).CreateAsync(target);
        var engine = new FluentMigratorEngine(DatabaseType.Sqlite, GetType().Assembly,
            "WawiCallPro.Database.Tests.FirstFailureRunnerFixtures");
        var migrator = new DatabaseMigrator(_factory, engine);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var failure = await Assert.ThrowsExactlyAsync<DatabaseOperationException>(() => migrator.MigrateAsync(target));
            Assert.AreEqual(DatabaseErrorCode.MigrationFailed, failure.Code);
            var status = await new DatabaseStatusService(_factory, engine.Catalog).GetStatusAsync(target);
            Assert.AreEqual(DatabaseSchemaState.EmptyDatabase, status.State);
            Assert.IsTrue(status.AppliedVersions.IsEmpty);
            Assert.AreEqual(0L, Scalar(target, "SELECT COUNT(*) FROM sqlite_schema WHERE name IN ('wawicallpro_metadata','fixture_data')"));
        }
    }

    [TestMethod]
    public void InvalidProviderVersionAndRawLogFormatterCannotOpenTargetsOrLeakContent()
    {
        var target = Target();
        var engine = Engine(DatabaseType.Sqlite);
        Assert.ThrowsExactly<DatabaseOperationException>(() => new FluentMigratorEngine((DatabaseType)999));
        Assert.ThrowsExactly<DatabaseOperationException>(() => engine.MigrateUp(DatabaseTarget.Server(DatabaseType.MySql, "Server=localhost;Database=test"), 1));
        Assert.ThrowsExactly<DatabaseOperationException>(() => engine.MigrateUp(target, 999));
        var log = new LogProbe();
        using var provider = new SafeRunnerLoggerProvider(log);
        var logger = provider.CreateLogger(Secret);
        using (logger.BeginScope(Secret))
            logger.Log(LogLevel.Error, new EventId(99), Secret, new Exception(Secret), (_, _) => throw new InvalidOperationException("Formatter must not run"));
        Assert.AreEqual(1, log.Messages.Count);
        Assert.DoesNotContain(Secret, log.Messages[0]);
        Assert.IsFalse(Directory.Exists(_root));
    }

    private static FluentMigratorEngine Engine(DatabaseType type) => new(type, typeof(FluentMigratorEngineTests).Assembly, FixtureNamespace);
    private DatabaseTarget Target() => DatabaseTarget.Sqlite(Path.Combine(_root, "fixture.db"), 7);
    private static string Hash(DatabaseTarget target) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(target.SqlitePath!)));

    private object? Scalar(DatabaseTarget target, string sql)
    {
        using var connection = _factory.CreateConnection(target, readOnly: true);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    private void Execute(DatabaseTarget target, string sql)
    {
        using var connection = _factory.CreateConnection(target);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private sealed class LogProbe : ILogger, ILoggerProvider
    {
        internal List<string> Messages { get; } = [];
        public ILogger CreateLogger(string categoryName) => this;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception) + exception);
        public void Dispose() { }
    }
}

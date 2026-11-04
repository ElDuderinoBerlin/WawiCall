using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Npgsql;

namespace WawiCallPro.Database.Tests;

[TestClass]
public sealed class DatabaseServicesTests
{
    private const string Secret = "private-provider-sentinel";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WawiCallPro-database-services-tests-" + Guid.NewGuid().ToString("N"));
    private readonly DatabaseFactory _factory = new();
    private DatabaseTarget Target() => DatabaseTarget.Sqlite(Path.Combine(_root, "communications.db"), commandTimeoutSeconds: 1);

    [TestCleanup]
    public void Cleanup()
    {
        var resolved = Path.GetFullPath(_root);
        var expectedPrefix = Path.Combine(Path.GetFullPath(Path.GetTempPath()), "WawiCallPro-database-services-tests-");
        Assert.IsTrue(resolved.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase));
        if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
    }

    [TestMethod]
    public void FactoryCreatesAllFourClosedDriversAndTargetsDoNotSerializeCredentials()
    {
        DatabaseTarget[] targets =
        [
            Target(),
            DatabaseTarget.Server(DatabaseType.SqlServer, $"Server={Secret}.invalid;Initial Catalog=wawicallpro_tests;User ID=test;Password={Secret}", 7, 9),
            DatabaseTarget.Server(DatabaseType.MySql, $"Server={Secret}.invalid;Database=wawicallpro_tests;User ID=test;Password={Secret}", 7, 9),
            DatabaseTarget.Server(DatabaseType.PostgreSql, $"Host={Secret}.invalid;Database=wawicallpro_tests;Username=test;Password={Secret}", 7, 9)
        ];
        Type[] types = [typeof(SqliteConnection), typeof(SqlConnection), typeof(MySqlConnection), typeof(NpgsqlConnection)];
        for (var index = 0; index < targets.Length; index++)
        {
            using var connection = _factory.CreateConnection(targets[index]);
            Assert.AreEqual(types[index], connection.GetType());
            Assert.AreEqual(ConnectionState.Closed, connection.State);
            Assert.DoesNotContain(Secret, targets[index].ToString());
            Assert.DoesNotContain(Secret, JsonSerializer.Serialize(targets[index]));
            if (index > 0) Assert.AreEqual(7, connection.ConnectionTimeout);
        }
        using var sqlite = (SqliteConnection)_factory.CreateConnection(targets[0], readOnly: true);
        var settings = new SqliteConnectionStringBuilder(sqlite.ConnectionString);
        Assert.AreEqual(SqliteOpenMode.ReadOnly, settings.Mode);
        Assert.IsFalse(settings.Pooling);
        Assert.IsTrue(settings.ForeignKeys);
        Assert.AreEqual(1, settings.DefaultTimeout);
        Assert.IsFalse(Directory.Exists(_root));
    }

    [TestMethod]
    public void InvalidAndSystemTargetsFailWithFixedMessagesBeforeAnyIo()
    {
        foreach (var type in new[] { DatabaseType.SqlServer, DatabaseType.MySql, DatabaseType.PostgreSql })
        {
            var prefix = type == DatabaseType.PostgreSql ? "Host=localhost;" : "Server=localhost;";
            foreach (var name in new[] { "master", "mysql", "postgres", "template1", "", new string('x', 129) })
            {
                var failure = Assert.ThrowsExactly<DatabaseOperationException>(() => DatabaseTarget.Server(type, prefix + "Database=" + name));
                Assert.AreEqual(DatabaseErrorCode.InvalidTarget, failure.Code);
                Assert.IsNull(failure.InnerException);
            }
            var error = Assert.ThrowsExactly<DatabaseOperationException>(() => DatabaseTarget.Server(type, "not-a-connection-string-" + Secret));
            Assert.DoesNotContain(Secret, error.ToString());
        }
        Assert.ThrowsExactly<DatabaseOperationException>(() => DatabaseTarget.Sqlite(":memory:"));
        Assert.ThrowsExactly<DatabaseOperationException>(() => DatabaseTarget.Sqlite(Path.GetPathRoot(_root)!));
        Assert.ThrowsExactly<DatabaseOperationException>(() => DatabaseTarget.Sqlite(Path.Combine(_root, "db:stream")));
        Assert.ThrowsExactly<DatabaseOperationException>(() => DatabaseTarget.Sqlite(Path.Combine(_root, "db"), 0));
        Assert.ThrowsExactly<DatabaseOperationException>(() => DatabaseTarget.Server(DatabaseType.SqlServer, "Server=localhost;Database=app;AttachDbFilename=private.mdf"));
        Assert.ThrowsExactly<DatabaseOperationException>(() => DatabaseTarget.Server(DatabaseType.PostgreSql, "Host=localhost;Database=" + new string('ä', 32)));
        Assert.IsFalse(Directory.Exists(_root));
    }

    [TestMethod]
    public async Task MissingStatusAndCancelledRequestsNeverCreateFilesOrParents()
    {
        var target = Target();
        var status = await new DatabaseStatusService(_factory, new([1, 2])).GetStatusAsync(target);
        Assert.AreEqual(DatabaseSchemaState.MissingDatabase, status.State);
        Assert.IsFalse(Directory.Exists(_root));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => new DatabaseStatusService(_factory, new([])).GetStatusAsync(target, cancellation.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(() => new DatabaseProvisioner(_factory).CreateAsync(target, cancellation.Token));
        Assert.IsFalse(Directory.Exists(_root));
    }

    [TestMethod]
    public async Task ProvisioningCreatesOnlyAnEmptyDatabaseAndNeverReplacesAnExistingFile()
    {
        var target = Target();
        var receipt = await new DatabaseProvisioner(_factory).CreateAsync(target);
        Assert.AreEqual(DatabaseType.Sqlite, receipt.Type);
        Assert.IsTrue(new FileInfo(FilePath()).Length >= 100);
        var original = Hash();
        var status = await new DatabaseStatusService(_factory, new([1])).GetStatusAsync(target);
        Assert.AreEqual(DatabaseSchemaState.EmptyDatabase, status.State);
        Assert.AreEqual(original, Hash());
        Assert.AreEqual(0L, await Scalar(target, "SELECT COUNT(*) FROM sqlite_schema WHERE type='table'"));
        var failure = await Assert.ThrowsExactlyAsync<DatabaseOperationException>(() => new DatabaseProvisioner(_factory).CreateAsync(target));
        Assert.AreEqual(DatabaseErrorCode.AlreadyExists, failure.Code);
        Assert.AreEqual(original, Hash());
        Assert.HasCount(1, Directory.GetFiles(_root));
    }

    [TestMethod]
    public async Task OrdinaryReadAndWriteConnectionsCannotCreateAMissingSqliteDatabase()
    {
        Directory.CreateDirectory(_root);
        foreach (var readOnly in new[] { false, true })
        {
            await using var connection = _factory.CreateConnection(Target(), readOnly);
            await Assert.ThrowsExactlyAsync<SqliteException>(() => connection.OpenAsync());
            Assert.IsFalse(File.Exists(FilePath()));
        }
    }

    [TestMethod]
    [DataRow("pending", DatabaseSchemaState.MigrationRequired)]
    [DataRow("current", DatabaseSchemaState.Current)]
    [DataRow("newer", DatabaseSchemaState.NewerSchema)]
    [DataRow("gap", DatabaseSchemaState.InvalidSchema)]
    [DataRow("duplicate", DatabaseSchemaState.InvalidSchema)]
    [DataRow("noninteger", DatabaseSchemaState.InvalidSchema)]
    [DataRow("owner-format", DatabaseSchemaState.NewerSchema)]
    [DataRow("owner-foreign", DatabaseSchemaState.ForeignDatabase)]
    [DataRow("owner-empty", DatabaseSchemaState.InvalidSchema)]
    public async Task StatusClassifiesStoredMetadataWithoutWriting(string fixture, DatabaseSchemaState expected)
    {
        var target = Target();
        await new DatabaseProvisioner(_factory).CreateAsync(target);
        await FixtureSchema(target);
        var sql = fixture switch
        {
            "pending" => "INSERT INTO versioninfo VALUES (1)",
            "current" => "INSERT INTO versioninfo VALUES (1),(2)",
            "newer" => "INSERT INTO versioninfo VALUES (1),(2),(9)",
            "gap" => "INSERT INTO versioninfo VALUES (2)",
            "duplicate" => "INSERT INTO versioninfo VALUES (1),(1)",
            "noninteger" => "INSERT INTO versioninfo VALUES ('bad')",
            "owner-format" => "UPDATE wawicallpro_metadata SET formatversion=2",
            "owner-foreign" => "UPDATE wawicallpro_metadata SET applicationid='Other.Application'",
            _ => "DELETE FROM wawicallpro_metadata"
        };
        await Execute(target, sql);
        var original = Hash();
        var status = await new DatabaseStatusService(_factory, new([1, 2])).GetStatusAsync(target);
        Assert.AreEqual(expected, status.State);
        Assert.AreEqual(original, Hash());
        Assert.HasCount(1, Directory.GetFiles(_root));
    }

    [TestMethod]
    public async Task EmptyMigrationCatalogIsNeverReportedAsCurrent()
    {
        var target = Target();
        await new DatabaseProvisioner(_factory).CreateAsync(target);
        await FixtureSchema(target);
        var status = await new DatabaseStatusService(_factory, new([])).GetStatusAsync(target);
        Assert.AreEqual(DatabaseSchemaState.MigrationsUnavailable, status.State);
        Assert.IsNull(status.LatestAvailableVersion);
        var engine = new FixtureEngine(_factory, []);
        var error = await Assert.ThrowsExactlyAsync<DatabaseOperationException>(() => new DatabaseMigrator(_factory, engine).MigrateAsync(target));
        Assert.AreEqual(DatabaseErrorCode.MigrationDefinitionsMissing, error.Code);
        Assert.HasCount(0, engine.Calls);
    }

    [TestMethod]
    public async Task ForeignDatabaseIsUntouchedAndNeverPassedToTheMigrationEngine()
    {
        var target = Target();
        await new DatabaseProvisioner(_factory).CreateAsync(target);
        await Execute(target, "CREATE TABLE source_customers (id INTEGER); INSERT INTO source_customers VALUES (7)");
        var original = Hash();
        var engine = new FixtureEngine(_factory, [1]);
        var status = await new DatabaseStatusService(_factory, engine.Catalog).GetStatusAsync(target);
        Assert.AreEqual(DatabaseSchemaState.ForeignDatabase, status.State);
        var failure = await Assert.ThrowsExactlyAsync<DatabaseOperationException>(() => new DatabaseMigrator(_factory, engine).MigrateAsync(target));
        Assert.AreEqual(DatabaseErrorCode.ForeignDatabase, failure.Code);
        Assert.HasCount(0, engine.Calls);
        Assert.AreEqual(7L, await Scalar(target, "SELECT id FROM source_customers"));
        Assert.AreEqual(original, Hash());
        Assert.AreEqual(0L, await Scalar(target, "SELECT COUNT(*) FROM sqlite_schema WHERE name='versioninfo'"));
    }

    [TestMethod]
    public async Task UserObjectsWithSimilarNamesAreNotMistakenForSystemObjects()
    {
        var target = Target();
        await new DatabaseProvisioner(_factory).CreateAsync(target);
        await Execute(target, "CREATE TABLE sqlitecustomer (id INTEGER)");
        var status = await new DatabaseStatusService(_factory, new([1])).GetStatusAsync(target);
        Assert.AreEqual(DatabaseSchemaState.ForeignDatabase, status.State);
    }

    [TestMethod]
    public async Task CorruptAndLockedFilesReturnSafeDistinctStatusInsteadOfMissingOrSuccess()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(FilePath(), "not a database " + Secret);
        var status = await new DatabaseStatusService(_factory, new([1])).GetStatusAsync(Target());
        Assert.AreEqual(DatabaseSchemaState.InvalidSchema, status.State);
        Assert.DoesNotContain(Secret, JsonSerializer.Serialize(status));
        using var locked = new FileStream(FilePath(), FileMode.Open, FileAccess.Read, FileShare.None);
        status = await new DatabaseStatusService(_factory, new([1])).GetStatusAsync(Target());
        Assert.AreEqual(DatabaseSchemaState.ConnectionFailed, status.State);
        Assert.DoesNotContain(Secret, JsonSerializer.Serialize(status));
    }

    [TestMethod]
    public async Task SuccessfulMigrationVerifiesEachStepAndRepeatedRunDoesNotReapplyIt()
    {
        var target = Target();
        await new DatabaseProvisioner(_factory).CreateAsync(target);
        var engine = new FixtureEngine(_factory, [1, 2]);
        var progress = new ProgressProbe();
        var migrator = new DatabaseMigrator(_factory, engine);
        var status = await migrator.MigrateAsync(target, progress: progress);
        Assert.AreEqual(DatabaseSchemaState.Current, status.State);
        CollectionAssert.AreEqual(new long[] { 1, 2 }, engine.Calls.ToArray());
        CollectionAssert.AreEqual(new[] { DatabaseMigrationPhase.Inspecting, DatabaseMigrationPhase.Applying,
            DatabaseMigrationPhase.Verifying, DatabaseMigrationPhase.Applying, DatabaseMigrationPhase.Verifying,
            DatabaseMigrationPhase.Completed }, progress.Items.Select(item => item.Phase).ToArray());
        Assert.AreEqual(2, progress.Items[^1].CompletedSteps);
        Assert.AreEqual(2, progress.Items[^1].TotalSteps);
        var before = Hash();
        await migrator.MigrateAsync(target);
        Assert.HasCount(2, engine.Calls);
        Assert.AreEqual(before, Hash());
    }

    [TestMethod]
    public async Task FailedStepKeepsTheCommittedPrefixAndNeverReportsCompletionOrLeaksProviderText()
    {
        var target = Target();
        await new DatabaseProvisioner(_factory).CreateAsync(target);
        var engine = new FixtureEngine(_factory, [1, 2]) { FailVersion = 2 };
        var progress = new ProgressProbe();
        var logger = new LogProbe();
        var failure = await Assert.ThrowsExactlyAsync<DatabaseOperationException>(() => new DatabaseMigrator(_factory, engine, logger).MigrateAsync(target, progress: progress));
        Assert.AreEqual(DatabaseErrorCode.MigrationFailed, failure.Code);
        Assert.IsNull(failure.InnerException);
        Assert.DoesNotContain(Secret, failure.ToString());
        Assert.DoesNotContain(Secret, string.Join("\n", logger.Messages));
        Assert.AreEqual(DatabaseMigrationPhase.Failed, progress.Items[^1].Phase);
        Assert.IsFalse(progress.Items.Any(item => item.Phase == DatabaseMigrationPhase.Completed));
        var status = await new DatabaseStatusService(_factory, engine.Catalog).GetStatusAsync(target);
        CollectionAssert.AreEqual(new long[] { 1 }, status.AppliedVersions.ToArray());
        Assert.AreEqual(0L, await Scalar(target, "SELECT COUNT(*) FROM sqlite_schema WHERE name='fixture_step_2'"));
        engine.FailVersion = null;
        await new DatabaseMigrator(_factory, engine).MigrateAsync(target);
        CollectionAssert.AreEqual(new long[] { 1, 2, 2 }, engine.Calls.ToArray());
    }

    [TestMethod]
    public async Task CancellationWaitsForTheRunningSynchronousStepAndStopsBeforeTheNextOne()
    {
        var target = Target();
        await new DatabaseProvisioner(_factory).CreateAsync(target);
        using var cancellation = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var engine = new FixtureEngine(_factory, [1, 2]) { BeforeStep = () => { entered.TrySetResult(); release.Task.GetAwaiter().GetResult(); } };
        var progress = new ProgressProbe();
        var migrator = new DatabaseMigrator(_factory, engine);
        var run = migrator.MigrateAsync(target, progress: progress, cancellationToken: cancellation.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();
            Assert.IsFalse(run.IsCompleted, "The executing synchronous step must still be owned and awaited.");
            var overlap = await Assert.ThrowsExactlyAsync<DatabaseOperationException>(() => migrator.MigrateAsync(target));
            Assert.AreEqual(DatabaseErrorCode.MigrationBusy, overlap.Code);
        }
        finally
        {
            release.TrySetResult();
            // Even a failing assertion must not let test cleanup race an unobserved SQLite worker.
            try { await run; }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        }
        await Assert.ThrowsAsync<OperationCanceledException>(() => run);
        Assert.AreEqual(DatabaseMigrationPhase.Cancelled, progress.Items[^1].Phase);
        CollectionAssert.AreEqual(new long[] { 1 }, engine.Calls.ToArray());
        var status = await new DatabaseStatusService(_factory, engine.Catalog).GetStatusAsync(target);
        Assert.AreEqual(DatabaseSchemaState.MigrationRequired, status.State);
        CollectionAssert.AreEqual(new long[] { 1 }, status.AppliedVersions.ToArray());
    }

    [TestMethod]
    public async Task NoOpEngineAndThrowingProgressCannotPretendThatAMigrationSucceeded()
    {
        var target = Target();
        await new DatabaseProvisioner(_factory).CreateAsync(target);
        var engine = new FixtureEngine(_factory, [1]) { NoOp = true };
        var failure = await Assert.ThrowsExactlyAsync<DatabaseOperationException>(() => new DatabaseMigrator(_factory, engine).MigrateAsync(target));
        Assert.AreEqual(DatabaseErrorCode.MigrationVerificationFailed, failure.Code);
        var progress = new ProgressProbe { Throw = true };
        failure = await Assert.ThrowsExactlyAsync<DatabaseOperationException>(() => new DatabaseMigrator(_factory, engine).MigrateAsync(target, progress: progress));
        Assert.AreEqual(DatabaseErrorCode.ProgressFailed, failure.Code);
        Assert.IsNull(failure.InnerException);
    }

    private string FilePath() => Path.Combine(_root, "communications.db");
    private string Hash() => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(FilePath())));

    private async Task Execute(DatabaseTarget target, string sql)
    {
        await using var connection = _factory.CreateConnection(target);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private async Task<object?> Scalar(DatabaseTarget target, string sql)
    {
        await using var connection = _factory.CreateConnection(target, readOnly: true);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync();
    }

    private Task FixtureSchema(DatabaseTarget target) => Execute(target,
        "CREATE TABLE wawicallpro_metadata (applicationid TEXT, formatversion INTEGER, createdatutc INTEGER);" +
        "INSERT INTO wawicallpro_metadata VALUES ('WawiCallPro.Communications',1,0); CREATE TABLE versioninfo (version INTEGER)");

    /// <summary>Test-only synchronous runner adapter using real SQLite transactions, not product migrations 1–5.</summary>
    private sealed class FixtureEngine(DatabaseFactory factory, IEnumerable<long> versions) : IDatabaseMigrationEngine
    {
        public DatabaseMigrationCatalog Catalog { get; } = new(versions);
        internal List<long> Calls { get; } = [];
        internal long? FailVersion { get; set; }
        internal bool NoOp { get; init; }
        internal Action? BeforeStep { get; init; }

        public void MigrateUp(DatabaseTarget target, long version)
        {
            Calls.Add(version);
            BeforeStep?.Invoke();
            if (NoOp) return;
            using var connection = factory.CreateConnection(target);
            connection.Open();
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "CREATE TABLE IF NOT EXISTS wawicallpro_metadata (applicationid TEXT, formatversion INTEGER, createdatutc INTEGER);" +
                "INSERT INTO wawicallpro_metadata SELECT 'WawiCallPro.Communications',1,0 WHERE NOT EXISTS (SELECT 1 FROM wawicallpro_metadata);" +
                "CREATE TABLE IF NOT EXISTS versioninfo (version INTEGER);" +
                "CREATE TABLE fixture_step_" + version + " (value TEXT); INSERT INTO versioninfo VALUES (@version)";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@version";
            parameter.Value = version;
            command.Parameters.Add(parameter);
            command.ExecuteNonQuery();
            if (FailVersion == version) throw new InvalidOperationException(Secret);
            transaction.Commit();
        }
    }

    private sealed class ProgressProbe : IProgress<DatabaseMigrationProgress>
    {
        internal List<DatabaseMigrationProgress> Items { get; } = [];
        internal bool Throw { get; init; }
        public void Report(DatabaseMigrationProgress value)
        {
            if (Throw) throw new InvalidOperationException(Secret);
            Items.Add(value);
        }
    }

    private sealed class LogProbe : ILogger<DatabaseMigrator>
    {
        internal List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception) + exception);
    }
}

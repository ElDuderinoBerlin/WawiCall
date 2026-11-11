using System.Data.Common;
using System.Diagnostics;
using System.Text.Json;
using System.Reflection;
using Microsoft.Data.SqlClient;
using MySqlConnector;
using Npgsql;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Parties;

namespace WawiCallPro.Database.Tests;

[TestClass]
public sealed class PhaseThreeAcceptanceTests
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WawiCallPro-phase3-" + Guid.NewGuid().ToString("N"));
    [TestCleanup] public void Cleanup() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
    [TestMethod]
    public async Task SQLiteFullAcceptanceIncludesRealUpgradeFailureRecoveryAndTwoProcesses()
    {
        var target = DatabaseTarget.Sqlite(Path.Combine(_root, "calls.db"));
        var receipt = await new DatabaseProvisioner(new()).CreateAsync(target);
        await PhaseThreeAcceptance.RunAsync(target, receipt, _root);
    }
}

/// <summary>Run only against a newly provisioned disposable communications database.</summary>
public static class PhaseThreeAcceptance
{
    public static async Task RunAsync(DatabaseTarget target, DatabaseCreation receipt, string artifactDirectory)
    {
        var factory = new DatabaseFactory(); var engine = new FluentMigratorEngine(target.Type);
        var status = new DatabaseStatusService(factory, engine.Catalog);
        Assert.AreEqual(DatabaseSchemaState.EmptyDatabase, (await status.GetStatusAsync(target)).State);
        engine.MigrateUp(target, 5);
        var company = new CompanyKey(LexwareProductLine.Pro, "upgrade-source Ä 电话", "0001 ");
        var party = new PartyKey(company, PartyKind.Customer, "0001 ");
        var companyId = DatabaseId.Encode(Guid.NewGuid()); var callId = DatabaseId.Encode(Guid.NewGuid());
        await using (var db = factory.CreateConnection(target))
        {
            await db.OpenAsync();
            await InsertAsync(db, target.Type, "companies", new()
            {
                ["id"] = companyId, ["sourcehash"] = SourceIdentityHash.Company(company), ["productline"] = 1, ["installationid"] = company.InstallationId,
                ["externalid"] = company.ExternalId, ["displayname"] = "Upgrade Ä 电话 ' ", ["writeversion"] = 1L, ["cacheversion"] = 0L,
                ["createdatutc"] = RepositoryAcceptance.Time.Ticks, ["updatedatutc"] = RepositoryAcceptance.Time.Ticks
            });
            await InsertAsync(db, target.Type, "phonecalls", new()
            {
                ["id"] = callId, ["companyid"] = companyId, ["workstationid"] = DatabaseId.Encode(Guid.NewGuid()), ["machinename"] = "UPGRADE-PC-Ä",
                ["windowsusername"] = "DOMAIN\\Upgrade", ["writeversion"] = 1L, ["revision"] = 0L, ["direction"] = 1, ["state"] = 3,
                ["localpresentation"] = 0, ["remotepresentation"] = 0, ["startedatutc"] = RepositoryAcceptance.Time.Ticks, ["statechangedatutc"] = RepositoryAcceptance.Time.Ticks
            });
            await InsertAsync(db, target.Type, "callparties", new()
            {
                ["id"] = DatabaseId.Encode(Guid.NewGuid()), ["callid"] = callId, ["partykeyhash"] = SourceIdentityHash.Party(party), ["kind"] = 1,
                ["externalid"] = party.ExternalId, ["displayname"] = "Historical Ä 电话", ["number"] = "+49301234567", ["isselected"] = 1, ["position"] = 0
            });
            await ExecuteAsync(db, "UPDATE " + Q(target.Type, "phonecalls") + " SET companyid=NULL");
        }
        var migrator = new DatabaseMigrator(factory, engine);
        Assert.AreEqual(DatabaseErrorCode.InvalidSchema, (await Assert.ThrowsExactlyAsync<DatabaseOperationException>(() => migrator.MigrateAsync(target, receipt))).Code);
        var failed = await status.GetStatusAsync(target);
        Assert.AreEqual(DatabaseSchemaState.MigrationRequired, failed.State); Assert.AreEqual(5L, failed.AppliedVersions[^1]);
        await using (var db = factory.CreateConnection(target))
        {
            await db.OpenAsync(); await ExecuteAsync(db, "UPDATE " + Q(target.Type, "phonecalls") + " SET companyid=@id", new() { ["id"] = companyId });
        }
        Assert.AreEqual(DatabaseSchemaState.Current, (await migrator.MigrateAsync(target, receipt)).State);
        var stored = await new Repositories.DapperCallRepository(new(target, factory)).GetAsync(DatabaseId.Decode(callId));
        Assert.AreEqual("Historical Ä 电话", stored!.History.Parties.Single().DisplayName);
        Assert.AreEqual(company, stored.History.Parties.Single().Party.Company); Assert.IsNull(stored.History.Parties.Single().CompanyDisplayName);
        Assert.AreEqual(RepositoryAcceptance.Time, stored.History.Call.StartedAtUtc);
        var before = await ServerBackupAcceptance.SnapshotAsync(target);
        for (var index = 0; index < 3; index++) Assert.AreEqual(DatabaseSchemaState.Current, (await status.GetStatusAsync(target)).State);
        Assert.AreEqual(DatabaseSchemaState.Current, (await migrator.MigrateAsync(target)).State);
        CollectionAssert.AreEqual(before, await ServerBackupAcceptance.SnapshotAsync(target));
        var futureVersion = engine.Catalog.LatestVersion!.Value + 1;
        await using (var db = factory.CreateConnection(target))
        {
            await db.OpenAsync(); await InsertAsync(db, target.Type, "versioninfo", new() { ["version"] = futureVersion });
        }
        Assert.AreEqual(DatabaseSchemaState.NewerSchema, (await status.GetStatusAsync(target)).State);
        Assert.AreEqual(DatabaseErrorCode.NewerSchema, (await Assert.ThrowsExactlyAsync<DatabaseOperationException>(() => migrator.MigrateAsync(target))).Code);
        await using (var db = factory.CreateConnection(target))
        {
            await db.OpenAsync(); await ExecuteAsync(db, "DELETE FROM " + Q(target.Type, "versioninfo") + " WHERE " + Q(target.Type, "version") + "=@future", new() { ["future"] = futureVersion });
        }
        CollectionAssert.AreEqual(before, await ServerBackupAcceptance.SnapshotAsync(target));
        await ProcessesAsync(target, artifactDirectory);
        CollectionAssert.AreEqual(before, await ServerBackupAcceptance.SnapshotAsync(target));
        await ForeignAsync(target, artifactDirectory);
        await RepositoryAcceptance.BatchedTransitionsAsync(target);
        Console.WriteLine(target.Type + ": upgrade with original data, real migration failure/recovery, repeated/status reads, newer/foreign rejection, two-process timeout/cancellation/crash release and batched call transitions passed.");
    }

    private static async Task ProcessesAsync(DatabaseTarget target, string artifactDirectory)
    {
        var directory = Path.Combine(artifactDirectory, "processes-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        var configuration = Path.Combine(directory, "target.json");
        await File.WriteAllTextAsync(configuration, JsonSerializer.Serialize(new { Type = target.Type.ToString(), target.SqlitePath, target.ConnectionString, LockTimeoutMilliseconds = 10000 }));
        var ready = Path.Combine(directory, "ready"); var release = Path.Combine(directory, "release");
        using var holder = Start("lease", configuration, ready, release);
        Process? waiting = null;
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            while (!File.Exists(ready))
            {
                Assert.IsFalse(holder.HasExited, "Native lease process failed."); await Task.Delay(25, deadline.Token);
            }
            var factory = new DatabaseFactory(); var gate = new DatabaseMigrationLock();
            var migrator = new DatabaseMigrator(factory, new FluentMigratorEngine(target.Type), gate, new DatabaseMigrationPermissions(factory), TimeSpan.FromMilliseconds(150));
            Assert.AreEqual(DatabaseErrorCode.MigrationLockTimeout, (await Assert.ThrowsExactlyAsync<DatabaseOperationException>(() => migrator.MigrateAsync(target))).Code);
            using (var canceled = new CancellationTokenSource(TimeSpan.FromMilliseconds(100)))
                await Assert.ThrowsAsync<OperationCanceledException>(() => gate.AcquireAsync(target, TimeSpan.FromSeconds(10), canceled.Token));
            waiting = Start("migrate", configuration); await Task.Delay(150); Assert.IsFalse(waiting.HasExited);
            holder.Kill(entireProcessTree: true); await holder.WaitForExitAsync(deadline.Token);
            var output = waiting.StandardOutput.ReadToEndAsync(deadline.Token); var error = waiting.StandardError.ReadToEndAsync(deadline.Token);
            await waiting.WaitForExitAsync(deadline.Token);
            Assert.AreEqual(0, waiting.ExitCode); Assert.AreEqual("", await error); Assert.Contains("Current", await output);
        }
        finally
        {
            if (!holder.HasExited) { holder.Kill(entireProcessTree: true); await holder.WaitForExitAsync(); }
            if (waiting is not null) { if (!waiting.HasExited) { waiting.Kill(entireProcessTree: true); await waiting.WaitForExitAsync(); } waiting.Dispose(); }
            File.Delete(configuration);
        }
    }

    private static Process Start(params string[] arguments)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "WawiCallProSolution.sln"))) root = root.Parent;
        Assert.IsNotNull(root, "Run the acceptance from the repository's Debug outputs.");
        var variant = typeof(DatabaseFactory).Assembly.GetCustomAttributes<System.Reflection.AssemblyMetadataAttribute>().First(attribute => attribute.Key == "LexwareProductLine").Value!;
        var worker = Path.Combine(root.FullName, "WawiCallPro.Database.TestWorker", "bin", "Debug", variant, "net11.0", "WawiCallPro.Database.TestWorker.dll");
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(worker); foreach (var argument in arguments) start.ArgumentList.Add(argument);
        return Process.Start(start)!;
    }

    private static async Task ForeignAsync(DatabaseTarget source, string artifactDirectory)
    {
        var name = "wcp_foreign_" + Guid.NewGuid().ToString("N");
        var target = source.Type switch
        {
            DatabaseType.Sqlite => DatabaseTarget.Sqlite(Path.Combine(artifactDirectory, name + ".db")),
            DatabaseType.SqlServer => DatabaseTarget.Server(source.Type, new SqlConnectionStringBuilder(source.ConnectionString) { InitialCatalog = name, Pooling = false }.ConnectionString),
            DatabaseType.MySql => DatabaseTarget.Server(source.Type, new MySqlConnectionStringBuilder(source.ConnectionString) { Database = name, Pooling = false }.ConnectionString),
            _ => DatabaseTarget.Server(source.Type, new NpgsqlConnectionStringBuilder(source.ConnectionString) { Database = name, Pooling = false }.ConnectionString)
        };
        var factory = new DatabaseFactory(); await new DatabaseProvisioner(factory).CreateAsync(target);
        try
        {
            await using (var db = factory.CreateConnection(target)) { await db.OpenAsync(); await ExecuteAsync(db, "CREATE TABLE foreign_source(sentinel int NOT NULL)"); await ExecuteAsync(db, "INSERT INTO foreign_source(sentinel) VALUES(42)"); }
            var migrator = new DatabaseMigrator(factory, new FluentMigratorEngine(target.Type));
            Assert.AreEqual(DatabaseErrorCode.ForeignDatabase, (await Assert.ThrowsExactlyAsync<DatabaseOperationException>(() => migrator.MigrateAsync(target))).Code);
            await using var verify = factory.CreateConnection(target); await verify.OpenAsync(); using var command = verify.CreateCommand(); command.CommandText = "SELECT sentinel FROM foreign_source";
            Assert.AreEqual(42, Convert.ToInt32(await command.ExecuteScalarAsync()));
        }
        finally
        {
            if (target.Type == DatabaseType.Sqlite) File.Delete(target.SqlitePath!);
            else { await using var db = factory.CreateMaintenanceConnection(target); await db.OpenAsync(); await ExecuteAsync(db, "DROP DATABASE " + Q(target.Type, name)); }
        }
    }

    private static string Q(DatabaseType type, string value) => type switch { DatabaseType.SqlServer => "[" + value + "]", DatabaseType.MySql => "`" + value + "`", _ => "\"" + value + "\"" };
    private static Task InsertAsync(DbConnection db, DatabaseType type, string table, Dictionary<string, object?> values) =>
        ExecuteAsync(db, "INSERT INTO " + Q(type, table) + "(" + string.Join(',', values.Keys.Select(value => Q(type, value))) + ") VALUES(" + string.Join(',', values.Keys.Select(value => "@" + value)) + ")", values);
    private static async Task ExecuteAsync(DbConnection db, string sql, Dictionary<string, object?>? values = null)
    {
        using var command = db.CreateCommand(); command.CommandText = sql;
        if (values is not null) foreach (var (name, value) in values) { var parameter = command.CreateParameter(); parameter.ParameterName = "@" + name; parameter.Value = value ?? DBNull.Value; command.Parameters.Add(parameter); }
        await command.ExecuteNonQueryAsync();
    }
}

using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace WawiCallPro.Database.Tests;

[TestClass]
public sealed class MigrationConcurrencyTests
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WawiCallPro-migration-gate-" + Guid.NewGuid().ToString("N"));
    private readonly DatabaseFactory _factory = new();
    private DatabaseTarget Target => DatabaseTarget.Sqlite(Path.Combine(_root, "calls.db"));

    [TestCleanup]
    public void Cleanup()
    {
        var full = Path.GetFullPath(_root);
        Assert.IsTrue(full.StartsWith(Path.Combine(Path.GetTempPath(), "WawiCallPro-migration-gate-"), StringComparison.OrdinalIgnoreCase));
        if (Directory.Exists(full)) Directory.Delete(full, true);
    }

    [TestMethod]
    public async Task GateTimesOutCancelsAndReleasesWithoutChangingDatabaseBytes()
    {
        var target = Target;
        await new DatabaseProvisioner(_factory).CreateAsync(target);
        var before = SHA256.HashData(await File.ReadAllBytesAsync(Path.Combine(_root, "calls.db")));
        var gate = new DatabaseMigrationLock();
        var lease = await gate.AcquireAsync(target, TimeSpan.FromSeconds(1));
        try
        {
            var error = await Assert.ThrowsExactlyAsync<DatabaseOperationException>(() => gate.AcquireAsync(target, TimeSpan.FromMilliseconds(80)));
            Assert.AreEqual(DatabaseErrorCode.MigrationLockTimeout, error.Code);
            using var cancel = new CancellationTokenSource(50);
            await Assert.ThrowsAsync<OperationCanceledException>(() => gate.AcquireAsync(target, TimeSpan.FromSeconds(5), cancel.Token));
            await lease.EnsureHeldAsync();
        }
        finally { await lease.DisposeAsync(); }
        Assert.AreEqual(DatabaseErrorCode.MigrationLockLost,
            (await Assert.ThrowsExactlyAsync<DatabaseOperationException>(() => lease.EnsureHeldAsync())).Code);
        await using var next = await gate.AcquireAsync(target, TimeSpan.FromSeconds(1));
        CollectionAssert.AreEqual(before, SHA256.HashData(await File.ReadAllBytesAsync(Path.Combine(_root, "calls.db"))));
        Assert.IsTrue(File.Exists(Path.Combine(_root, "calls.db.migration.lock")));
    }

    [TestMethod]
    public async Task MissingTargetsAndWrongProviderNeverCreateDirectoriesOrRunMigrations()
    {
        var missing = await Assert.ThrowsExactlyAsync<DatabaseOperationException>(() => new DatabaseMigrationLock().AcquireAsync(Target, TimeSpan.FromSeconds(1)));
        Assert.AreEqual(DatabaseErrorCode.MissingDatabase, missing.Code);
        var wrong = await Assert.ThrowsExactlyAsync<DatabaseOperationException>(() =>
            new DatabaseMigrator(_factory, new FluentMigratorEngine(DatabaseType.MySql)).MigrateAsync(Target));
        Assert.AreEqual(DatabaseErrorCode.InvalidTarget, wrong.Code);
        Assert.IsFalse(Directory.Exists(_root));
    }

    [TestMethod]
    public async Task ReadOnlyDatabaseRejectsPendingMigrationAndKeepsSchemaEmpty()
    {
        var target = Target;
        await new DatabaseProvisioner(_factory).CreateAsync(target);
        var path = Path.Combine(_root, "calls.db");
        File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
        try
        {
            var failure = await Assert.ThrowsExactlyAsync<DatabaseOperationException>(() =>
                new DatabaseMigrator(_factory, new FluentMigratorEngine(DatabaseType.Sqlite)).MigrateAsync(target));
            Assert.AreEqual(DatabaseErrorCode.AccessDenied, failure.Code);
            Assert.IsNull(failure.InnerException);
            var status = await new DatabaseStatusService(_factory, new([1, 2, 3, 4, 5])).GetStatusAsync(target);
            Assert.AreEqual(DatabaseSchemaState.EmptyDatabase, status.State);
        }
        finally { File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly); }
    }

    [TestMethod]
    public async Task ExplicitEmptyApprovalDoesNotCreateSchemaAndRejectsForeignContent()
    {
        var target = Target;
        var provisioner = new DatabaseProvisioner(_factory);
        await provisioner.CreateAsync(target);
        var approved = await provisioner.ApproveEmptyAsync(target);
        Assert.IsFalse(approved.WasCreated);
        var empty = await new DatabaseStatusService(_factory, new([])).GetStatusAsync(target);
        Assert.AreEqual(DatabaseSchemaState.EmptyDatabase, empty.State);
        await using var connection = _factory.CreateConnection(target);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand(); command.CommandText = "CREATE TABLE foreign_data(id INTEGER)";
        await command.ExecuteNonQueryAsync();
        var rejected = await Assert.ThrowsExactlyAsync<DatabaseOperationException>(() => provisioner.ApproveEmptyAsync(target));
        Assert.AreEqual(DatabaseErrorCode.ForeignDatabase, rejected.Code);
    }

    [TestMethod]
    public async Task TwoProcessesSerializeAndWaitingMigratorRereadsCompletedSchema()
    {
        var target = Target;
        await new DatabaseProvisioner(_factory).CreateAsync(target);
        var engine = new FluentMigratorEngine(DatabaseType.Sqlite);
        engine.MigrateUp(target, 1);
        var settings = Path.Combine(_root, "worker.json");
        await File.WriteAllTextAsync(settings, JsonSerializer.Serialize(new { Type = DatabaseType.Sqlite, SqlitePath = Path.Combine(_root, "calls.db"), LockTimeoutMilliseconds = 10000 }));
        var ready = Path.Combine(_root, "ready");
        var release = Path.Combine(_root, "release");
        using var holder = StartWorker("lease", settings, ready, release);
        Process? contender = null;
        try
        {
            await WaitUntilAsync(() => File.Exists(ready));
            var constrained = new DatabaseMigrator(_factory, engine, new DatabaseMigrationLock(), new DatabaseMigrationPermissions(_factory), TimeSpan.FromMilliseconds(100));
            var blocked = await Assert.ThrowsExactlyAsync<DatabaseOperationException>(() => constrained.MigrateAsync(target));
            Assert.AreEqual(DatabaseErrorCode.MigrationLockTimeout, blocked.Code);
            contender = StartWorker("migrate", settings);
            await Task.Delay(150);
            Assert.IsFalse(contender.HasExited);
            // Simulate the lease holder completing the remaining steps while the contender is waiting.
            engine.MigrateUp(target, engine.Catalog.LatestVersion!.Value);
            await File.WriteAllTextAsync(release, "release");
            await holder.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            await contender.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            Assert.AreEqual(0, holder.ExitCode);
            Assert.AreEqual(0, contender.ExitCode, await contender.StandardOutput.ReadToEndAsync());
            var state = await new DatabaseStatusService(_factory, engine.Catalog).GetStatusAsync(target);
            Assert.AreEqual(DatabaseSchemaState.Current, state.State);
        }
        finally
        {
            if (!holder.HasExited) { holder.Kill(true); await holder.WaitForExitAsync(); }
            if (contender is not null) { if (!contender.HasExited) { contender.Kill(true); await contender.WaitForExitAsync(); } contender.Dispose(); }
        }
    }

    [TestMethod]
    public async Task TerminatedProcessReleasesItsLeaseForTheNextProcess()
    {
        var target = Target;
        await new DatabaseProvisioner(_factory).CreateAsync(target);
        var settings = Path.Combine(_root, "worker.json");
        await File.WriteAllTextAsync(settings, JsonSerializer.Serialize(new { Type = DatabaseType.Sqlite, SqlitePath = Path.Combine(_root, "calls.db") }));
        var ready = Path.Combine(_root, "ready");
        using var holder = StartWorker("lease", settings, ready, Path.Combine(_root, "never-release"));
        try
        {
            await WaitUntilAsync(() => File.Exists(ready));
            holder.Kill(true);
            await holder.WaitForExitAsync();
            await using var recovered = await new DatabaseMigrationLock().AcquireAsync(target, TimeSpan.FromSeconds(2));
            await recovered.EnsureHeldAsync();
        }
        finally { if (!holder.HasExited) { holder.Kill(true); await holder.WaitForExitAsync(); } }
    }

    private static Process StartWorker(params string[] arguments)
    {
        var output = new DirectoryInfo(AppContext.BaseDirectory);
        var variant = output.Parent!.Name;
        var root = Path.GetFullPath(Path.Combine(output.FullName, "../../../../.."));
        var configuration = typeof(MigrationConcurrencyTests).Assembly.GetCustomAttribute<System.Reflection.AssemblyConfigurationAttribute>()!.Configuration;
        var worker = Path.Combine(root, "WawiCallPro.Database.TestWorker", "bin", configuration, variant, "net11.0", "WawiCallPro.Database.TestWorker.dll");
        Assert.IsTrue(File.Exists(worker), "Worker must be built in the same regular output configuration as the tests.");
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(worker);
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        return Process.Start(start)!;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!condition()) await Task.Delay(20, timeout.Token);
    }
}

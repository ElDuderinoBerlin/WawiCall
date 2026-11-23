using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WawiCallPro.Core.Threading;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Hosting;
using WawiCallPro.Database;
using WawiCallPro.Utilities.Operations;
using WawiCallPro.Utilities.Persistence;

namespace WawiCallPro.Utilities.Tests;

[TestClass]
public sealed class RecordingConversionRecoveryStartupTests
{
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task StartupReplaysWaveReceiptsWithoutSipAndAuditsOnlyAnEstablishedWorkstation(bool hasIdentity)
    {
        var wavePassCompleted = false;
        var auditRequests = 0;
        var conversions = new Recovery(_ =>
        {
            Assert.IsTrue(wavePassCompleted);
            Assert.AreEqual(hasIdentity ? 1 : 0, auditRequests);
            return Task.FromResult(new RecordingConversionRecoveryResult(0, 0, 0));
        });
        await using var worker = new RecordingConversionRecoveryStartup(conversions, new StartupLog(),
            new Interrupted(_ => Task.FromResult(new InterruptedMp3RecoveryResult(0, 0, 0))), new Queue(() => { }),
            new Database(DatabaseSchemaState.Current), waves: new WaveRecovery(() => wavePassCompleted = true),
            audit: new Audit(() => auditRequests++), workstation: new Identity(hasIdentity ? Guid.NewGuid() : null));
        await worker.StartAsync(default);
        await worker.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsTrue(wavePassCompleted);
        Assert.AreEqual(hasIdentity ? 1 : 0, auditRequests);
    }

    [TestMethod]
    public async Task AsyncDisposalCancelsAndAwaitsTheSingleOwnedPass()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var exited = false; var calls = 0;
        var recovery = new Recovery(async token =>
        {
            Interlocked.Increment(ref calls); entered.SetResult();
            try { await Task.Delay(Timeout.Infinite, token); return new(0, 0, 0); }
            finally { exited = true; }
        });
        var worker = new RecordingConversionRecoveryStartup(recovery, new StartupLog(), new Interrupted(_ => throw new AssertFailedException())); await worker.StartAsync(CancellationToken.None);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10)); await worker.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        await worker.DisposeAsync(); Assert.AreEqual(1, calls); Assert.IsTrue(exited);
    }
    [TestMethod]
    [DataRow(false)][DataRow(true)]
    public async Task OneCompletedPassIsObservedAndNativeFailuresAreNotLogged(bool fail)
    {
        var calls = 0; var log = new StartupLog();
        var recovery = new Recovery(token => { calls++; if (fail) throw new IOException("private-native-path"); return Task.FromResult(new RecordingConversionRecoveryResult(0, 0, 0)); });
        var interruptions = 0;
        await using var worker = new RecordingConversionRecoveryStartup(recovery, log, new Interrupted(_ => { interruptions++; Assert.AreEqual(1, calls); return Task.FromResult(new InterruptedMp3RecoveryResult(0, 0, 0)); }));
        await worker.StartAsync(CancellationToken.None); await worker.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.AreEqual(1, calls); Assert.AreEqual(fail ? 1 : 0, log.Warnings);
        Assert.AreEqual(fail ? 0 : 1, interruptions);
    }
    [TestMethod]
    public async Task DisposalAlsoCancelsAndAwaitsTheInterruptionPass()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var exited = false;
        await using var worker = new RecordingConversionRecoveryStartup(new Recovery(_ => Task.FromResult(new RecordingConversionRecoveryResult(0, 0, 0))),
            new StartupLog(), new Interrupted(async token => { entered.SetResult(); try { await Task.Delay(Timeout.Infinite, token); return new(0, 0, 0); } finally { exited = true; } }));
        await worker.StartAsync(CancellationToken.None); await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await worker.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10)); Assert.IsTrue(exited);
    }
    [TestMethod]
    public async Task InterruptionPassFailureIsRedactedAndObserved()
    {
        var log = new StartupLog();
        await using var worker = new RecordingConversionRecoveryStartup(new Recovery(_ => Task.FromResult(new RecordingConversionRecoveryResult(0, 0, 0))), log,
            new Interrupted(_ => throw new IOException("private-interruption-path")));
        await worker.StartAsync(CancellationToken.None); await worker.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.AreEqual(1, log.Warnings);
    }
    [TestMethod]
    [DataRow("ready")][DataRow("delivery-failed")][DataRow("delivery-deferred")]
    [DataRow("recovery-failed")][DataRow("recovery-deferred")][DataRow("more")]
    public async Task QueueIsRequestedOnlyAfterBothRecoveryPassesAreFullyConfirmed(string state)
    {
        var stage = 0; var queue = new Queue(() => Assert.AreEqual(2, stage));
        await using var worker = new RecordingConversionRecoveryStartup(new Recovery(_ =>
        { stage = 1; return Task.FromResult(new RecordingConversionRecoveryResult(0, state == "delivery-deferred" ? 1 : 0, state == "delivery-failed" ? 1 : 0)); }),
            new StartupLog(), new Interrupted(_ =>
            { Assert.AreEqual(1, stage); stage = 2; return Task.FromResult(new InterruptedMp3RecoveryResult(0, state == "recovery-deferred" ? 1 : 0, state == "recovery-failed" ? 1 : 0, state == "more")); }), queue);
        await worker.StartAsync(default); await worker.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual(state == "ready" ? 1 : 0, queue.Requests);
    }
    [TestMethod]
    [DataRow(DatabaseSchemaState.MigrationRequired, OperationEvent.RecordingConversionSchemaUpdateRequired)]
    [DataRow(DatabaseSchemaState.NewerSchema, OperationEvent.RecordingConversionNewerSchema)]
    [DataRow(DatabaseSchemaState.MissingDatabase, OperationEvent.RecordingConversionDatabaseUnavailable)]
    [DataRow(DatabaseSchemaState.ConnectionFailed, OperationEvent.RecordingConversionDatabaseUnavailable)]
    public async Task AnUnreadyDatabaseReportsItsCauseWithoutCheckingForPendingJobs(DatabaseSchemaState state, OperationEvent expected)
    {
        var log = new StartupLog(expected);
        var queue = new Queue(() => Assert.Fail("An unready database cannot start conversions."));
        await using var worker = new RecordingConversionRecoveryStartup(
            new Recovery(_ => throw new AssertFailedException("Recovery must not touch the outbox.")), log,
            new Interrupted(_ => throw new AssertFailedException("Recovery must not query recording jobs.")), queue, new Database(state));
        await worker.StartAsync(default);
        await worker.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual(1, log.Warnings);
        Assert.AreEqual(0, queue.Requests);
    }

    [TestMethod]
    public async Task UpdatingTheActiveSqliteTargetWakesRecoveryWithoutRestart()
    {
        var root = Path.Combine(Path.GetTempPath(), "wcp-active-database-" + Guid.NewGuid().ToString("N"));
        var host = WawiCallHost.CreateBuilder(typeof(RecordingConversionRecoveryStartupTests).Assembly).ConfigureServices(services =>
        {
            services.AddSingleton(new ConfigurationLocation(root));
            services.AddSingleton<IUiDispatcher, InlineUiDispatcher>();
        }).Build();
        try
        {
            await HostExecution.RunAsync(host, async token =>
            {
                var setup = host.Services.GetRequiredService<IDatabaseSetupService>();
                var active = host.Services.GetRequiredService<ApplicationConfigurationService>();
                var database = host.Services.GetRequiredService<ICommunicationDatabase>();
                var before = await setup.LoadAsync(token);
                var draft = setup.ReadDraft(before);
                Assert.IsTrue(setup.UsesActiveDatabase(draft));
                var target = DatabaseTarget.Sqlite(draft.SqlitePath!);
                var factory = new DatabaseFactory();
                await new DatabaseProvisioner(factory).CreateAsync(target, token);
                new FluentMigratorEngine(DatabaseType.Sqlite).MigrateUp(target, 14);
                Assert.AreEqual(DatabaseSchemaState.MigrationRequired, (await database.GetStatusAsync(token)).State);

                var warning = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var startedQueue = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var log = new StartupLog(OperationEvent.RecordingConversionSchemaUpdateRequired, () => warning.TrySetResult());
                var deliveries = 0;
                await using var worker = new RecordingConversionRecoveryStartup(
                    new Recovery(_ => { deliveries++; return Task.FromResult(new RecordingConversionRecoveryResult(0, 0, 0)); }),
                    log, new Interrupted(_ => Task.FromResult(new InterruptedMp3RecoveryResult(0, 0, 0))),
                    new Queue(() => startedQueue.TrySetResult()), database, host.Services.GetRequiredService<ActiveDatabaseReady>());
                await worker.StartAsync(token);
                await warning.Task.WaitAsync(TimeSpan.FromSeconds(5), token);

                var other = new DatabaseSetupDraft { Type = DatabaseType.Sqlite, SqlitePath = Path.Combine(root, "other.db") };
                Assert.IsFalse(setup.UsesActiveDatabase(other));
                await setup.InitializeAsync(other, false, cancellationToken: token);
                Assert.IsFalse(startedQueue.Task.IsCompleted);
                Assert.AreEqual(DatabaseSchemaState.MigrationRequired, (await database.GetStatusAsync(token)).State);

                await setup.InitializeAsync(draft, false, cancellationToken: token);
                await setup.SaveAsync(draft, before, token);
                await startedQueue.Task.WaitAsync(TimeSpan.FromSeconds(5), token);

                Assert.AreEqual(1, deliveries);
                Assert.AreEqual(DatabaseSchemaState.Current, (await database.GetStatusAsync(token)).State);
                Assert.AreEqual(active.Current.Options.Database, (await setup.LoadAsync(token)).Options.Database);
            });
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private sealed class Database(DatabaseSchemaState state) : ICommunicationDatabase
    {
        public Task<DatabaseMigrationStatus> GetStatusAsync(CancellationToken token = default) =>
            Task.FromResult(new DatabaseMigrationStatus(DatabaseType.Sqlite, state, [14], [15], 15));

        public Task<DatabaseMigrationStatus> CreateAndMigrateAsync(IProgress<DatabaseMigrationProgress>? progress = null, CancellationToken token = default) =>
            throw new AssertFailedException("Startup checks must not change the database.");
        public Task<DatabaseMigrationStatus> MigrateAsync(bool approveEmpty = false, IProgress<DatabaseMigrationProgress>? progress = null, CancellationToken token = default) =>
            throw new AssertFailedException("Startup checks must not change the database.");
        public Task BackupSqliteAsync(string destination, CancellationToken token = default) => throw new NotSupportedException();
        public Task RestoreSqliteNewAsync(string backup, CancellationToken token = default) => throw new NotSupportedException();
    }

    private sealed class Queue(Action requested) : IRecordingConversionQueue
    { internal int Requests; public void Request() { requested(); Requests++; } }
    private sealed class Interrupted(Func<CancellationToken, Task<InterruptedMp3RecoveryResult>> recover) : IInterruptedMp3Recovery
    { public Task<InterruptedMp3RecoveryResult> RecoverAsync(CancellationToken token = default) => recover(token); }
    private sealed class Recovery(Func<CancellationToken, Task<RecordingConversionRecoveryResult>> recover) : IRecordingConversionRecovery
    { public Task<RecordingConversionRecoveryResult> RecoverAsync(CancellationToken token = default) => recover(token); }
    private sealed class WaveRecovery(Action complete) : IRecordingJournalRecovery
    {
        public Task<RecordingJournalRecoveryResult> RecoverAsync(CancellationToken cancellationToken = default)
        { complete(); return Task.FromResult(new RecordingJournalRecoveryResult(1, 0, 0)); }
    }
    private sealed class Audit(Action requested) : IRecordingArtifactAuditScheduler
    { public void Request() => requested(); }
    private sealed class Identity(Guid? value) : IWorkstationIdentity
    {
        public Task<Guid?> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(value);
        public Task<Guid> InitializeAsync(CancellationToken cancellationToken = default) => throw new AssertFailedException();
    }
    private sealed class StartupLog(OperationEvent expected = OperationEvent.RecordingConversionCheckFailed, Action? logged = null) : ILogger<RecordingConversionRecoveryStartup>
    {
        internal int Warnings;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        { Assert.IsNull(exception); Assert.IsTrue(OperationEvents.TryGetEvent(state, out var operation)); Assert.AreEqual(expected, operation); Warnings++; logged?.Invoke(); }
    }
}

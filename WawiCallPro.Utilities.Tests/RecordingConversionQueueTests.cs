using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WawiCallPro.Database;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Common;
using WawiCallPro.Utilities.Operations;
using WawiCallPro.Utilities.Persistence;

namespace WawiCallPro.Utilities.Tests;

[TestClass]
public sealed class RecordingConversionQueueTests
{
    [TestMethod]
    public async Task PagesProcessOnlyPendingJobsOnceAndDoNotStopAtABlockedJob()
    {
        var jobs = new Jobs(); var factory = new Sessions();
        jobs.Rows = Enumerable.Range(0, 205).Select(_ => MakeJob()).OrderBy(row => DatabaseId.Encode(row.Job.Id), StringComparer.Ordinal).ToImmutableArray();
        factory.Blocked = jobs.Rows[0].Job.Id;
        var batch = new RecordingConversionBatch(jobs, new Identity(Guid.NewGuid()), factory);
        Assert.AreEqual(new RecordingConversionBatchResult(204, 0, 1), await batch.RunAsync());
        Assert.AreEqual(205, factory.Started.Count); Assert.AreEqual(204, factory.Disposed); Assert.AreEqual(3, jobs.Reads);
        Assert.AreEqual(205, factory.Started.Distinct().Count());
    }

    [TestMethod]
    [DataRow(RecordingConversionState.Running)][DataRow(RecordingConversionState.Completed)][DataRow(RecordingConversionState.Failed)]
    public async Task OtherStatesAreNeverStartedOrRequeued(RecordingConversionState state)
    {
        var jobs = new Jobs { Rows = [MakeJob(state)] }; var factory = new Sessions();
        Assert.AreEqual(new RecordingConversionBatchResult(0, 0, 0), await new RecordingConversionBatch(jobs, new Identity(Guid.NewGuid()), factory).RunAsync());
        Assert.AreEqual(0, factory.Started.Count);
    }

    [TestMethod]
    public async Task MissingIdentityAndCancellationDoNotStartWork()
    {
        var jobs = new Jobs { Rows = [MakeJob()] }; var factory = new Sessions();
        var batch = new RecordingConversionBatch(jobs, new Identity(null), factory);
        Assert.AreEqual(new RecordingConversionBatchResult(0, 0, 1), await batch.RunAsync()); Assert.AreEqual(0, jobs.Reads);
        await Assert.ThrowsAsync<OperationCanceledException>(() => batch.RunAsync(new CancellationToken(true)));
        Assert.AreEqual(0, factory.Started.Count);
    }

    [TestMethod]
    [DataRow(false)][DataRow(true)]
    public async Task FailedResultsAndUncertainDisposalAreNotCountedAsSuccess(bool disposalFailure)
    {
        var jobs = new Jobs { Rows = [MakeJob()] }; var factory = new Sessions { FailResult = !disposalFailure, FailDispose = disposalFailure };
        Assert.AreEqual(new RecordingConversionBatchResult(0, disposalFailure ? 0 : 1, disposalFailure ? 1 : 0),
            await new RecordingConversionBatch(jobs, new Identity(Guid.NewGuid()), factory).RunAsync()); Assert.AreEqual(1, factory.Disposed);
    }

    [TestMethod]
    public async Task ARepeatedPageIsRejectedBeforeStartingItsJobsAgain()
    {
        var jobs = new Jobs { Repeat = true, Rows = Enumerable.Range(0, 101).Select(_ => MakeJob()).OrderBy(row => DatabaseId.Encode(row.Job.Id), StringComparer.Ordinal).ToImmutableArray() };
        var factory = new Sessions();
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => new RecordingConversionBatch(jobs, new Identity(Guid.NewGuid()), factory).RunAsync());
        Assert.AreEqual(100, factory.Started.Count);
    }

    [TestMethod]
    public async Task CancellationAwaitsAndDisposesTheClaimedSession()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var factory = new Sessions { BeforeRun = async token => { started.SetResult(); await Task.Delay(Timeout.Infinite, token); } };
        using var stop = new CancellationTokenSource();
        var running = new RecordingConversionBatch(new Jobs { Rows = [MakeJob()] }, new Identity(Guid.NewGuid()), factory).RunAsync(stop.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5)); stop.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => running); Assert.AreEqual(1, factory.Disposed);
    }

    [TestMethod]
    public async Task RealPendingJobCompletesAndRepeatedPassDoesNotEncodeAgain()
    { using var database = await WaveArtifactFixture.Create(); await RecordingConversionQueueAcceptance.RunAsync(database.Target); }

    [TestMethod]
    public async Task RequestsCoalesceAndDisposalAwaitsSessionCleanup()
    {
        var first = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var next = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleaning = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var calls = 0;
        var batch = new Batch(async token =>
        {
            if (Interlocked.Increment(ref calls) == 1) { first.SetResult(); await release.Task.WaitAsync(token); }
            else { next.SetResult(); try { await Task.Delay(Timeout.Infinite, token); } finally { cleaning.SetResult(); await cleanup.Task; } }
            return new(0, 0, 0);
        });
        await using var worker = new RecordingConversionQueue(batch, NullLogger<RecordingConversionQueue>.Instance);
        await worker.StartAsync(default); Assert.AreEqual(0, calls); worker.Request(); await first.Task.WaitAsync(TimeSpan.FromSeconds(5));
        for (var i = 0; i < 1000; i++) worker.Request(); release.SetResult(); await next.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var disposal = worker.DisposeAsync().AsTask();
        try { await cleaning.Task.WaitAsync(TimeSpan.FromSeconds(5)); Assert.IsFalse(disposal.IsCompleted); }
        finally { cleanup.TrySetResult(); }
        await disposal.WaitAsync(TimeSpan.FromSeconds(5)); worker.Request(); await worker.DisposeAsync(); Assert.AreEqual(2, calls);
    }

    [TestMethod]
    public async Task PrivateErrorsAreRedactedAndNextExplicitRequestStillRuns()
    {
        var log = new QueueLog(); var next = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var calls = 0;
        await using var worker = new RecordingConversionQueue(new Batch(_ =>
        { if (Interlocked.Increment(ref calls) == 1) throw new IOException("private-recording-path"); next.SetResult(); return Task.FromResult(new RecordingConversionBatchResult(0, 0, 0)); }), log);
        await worker.StartAsync(default); worker.Request(); await log.Warned.Task.WaitAsync(TimeSpan.FromSeconds(5));
        worker.Request(); await next.Task.WaitAsync(TimeSpan.FromSeconds(5)); await worker.StopAsync(default); Assert.AreEqual(2, calls);
    }

    [TestMethod]
    public async Task RepeatedDeferralWarnsOnceUntilTheQueueRecovers()
    {
        var log = new QueueLog();
        using var invoked = new SemaphoreSlim(0);
        var results = new Queue<RecordingConversionBatchResult>([new(0, 0, 1), new(0, 0, 1), new(1, 0, 0), new(0, 0, 1)]);
        await using var worker = new RecordingConversionQueue(new Batch(_ =>
        {
            var result = results.Dequeue();
            invoked.Release();
            return Task.FromResult(result);
        }), log);

        await worker.StartAsync(default);
        for (var i = 0; i < 4; i++)
        {
            worker.Request();
            Assert.IsTrue(await invoked.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        await worker.StopAsync(default);
        Assert.AreEqual(2, log.WarningCount);
    }

    private static StoredRecordingConversion MakeJob(RecordingConversionState state = RecordingConversionState.Pending)
    {
        var now = DateTimeOffset.UtcNow;
        return new(new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new(8000, 2, 128000), state,
            state == RecordingConversionState.Pending ? 0 : 1, state == RecordingConversionState.Pending ? null : Guid.NewGuid(),
            state == RecordingConversionState.Failed ? RecordingConversionFailure.Interrupted : null), 1, now, now);
    }
    private sealed class Identity(Guid? id) : IWorkstationIdentity
    { public Task<Guid?> GetAsync(CancellationToken token = default) => Task.FromResult(id); public Task<Guid> InitializeAsync(CancellationToken token = default) => throw new AssertFailedException(); }
    private sealed class Jobs : IRecordingConversionRepository
    {
        internal ImmutableArray<StoredRecordingConversion> Rows = []; internal int Reads; internal bool Repeat;
        public Task<Page<StoredRecordingConversion>> GetPageAsync(Guid workstation, Guid? afterJobId = null, int limit = 100, CancellationToken token = default)
        { Reads++; var rows = Rows.Where(row => Repeat || afterJobId is null || StringComparer.Ordinal.Compare(DatabaseId.Encode(row.Job.Id), DatabaseId.Encode(afterJobId.Value)) > 0).Take(limit + 1).ToArray(); return Task.FromResult(new Page<StoredRecordingConversion>(rows.Take(limit).ToImmutableArray(), rows.Length > limit)); }
        public Task<StoredRecordingConversion?> GetAsync(Guid id, CancellationToken token = default) => throw new AssertFailedException();
        public Task<StoredRecordingConversion?> GetForSourceAsync(Guid id, CancellationToken token = default) => throw new AssertFailedException();
        public Task<StoredRecordingConversion> WriteAsync(RecordingConversionWrite write, CancellationToken token = default) => throw new AssertFailedException();
    }
    private sealed class Sessions : IPersistedMp3ConversionFactory
    {
        internal readonly List<Guid> Started = []; internal Guid Blocked; internal int Disposed; internal bool FailResult, FailDispose;
        internal Func<CancellationToken, Task>? BeforeRun;
        public Task<IPersistedMp3Conversion> StartAsync(Guid id, CancellationToken token = default)
        { Started.Add(id); if (id == Blocked) throw new IOException("private-lock"); return Task.FromResult<IPersistedMp3Conversion>(new Session(this)); }
        private sealed class Session(Sessions owner) : IPersistedMp3Conversion
        {
            public async Task<StoredRecordingConversionJournal> RunAsync(CancellationToken token = default)
            { if (owner.BeforeRun is not null) await owner.BeforeRun(token); var job = MakeJob(owner.FailResult ? RecordingConversionState.Failed : RecordingConversionState.Completed); return new StoredRecordingConversionJournal(null!, job); }
            public Task<StoredRecordingConversionJournal> ConfirmAsync(CancellationToken token = default) => throw new AssertFailedException();
            public ValueTask DisposeAsync() { owner.Disposed++; if (owner.FailDispose) throw new IOException("uncertain-private-confirmation"); return ValueTask.CompletedTask; }
        }
    }
    private sealed class Batch(Func<CancellationToken, Task<RecordingConversionBatchResult>> run) : IRecordingConversionBatch
    { public Task<RecordingConversionBatchResult> RunAsync(CancellationToken token = default) => run(token); }
    private sealed class QueueLog : ILogger<RecordingConversionQueue>
    {
        internal readonly TaskCompletionSource Warned = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int WarningCount { get; private set; }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null; public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> formatter)
        { Assert.IsNull(error); Assert.AreEqual(LogLevel.Warning, level); Assert.IsTrue(OperationEvents.TryGetEvent(state, out var value)); Assert.AreEqual(OperationEvent.RecordingConversionQueueIncomplete, value); WarningCount++; Warned.TrySetResult(); }
    }
}

public static class RecordingConversionQueueAcceptance
{
    public static async Task RunAsync(DatabaseTarget target)
    {
        using var ready = await Mp3AttemptFixture.Create(target); var converter = new AttemptConverter();
        var batch = new RecordingConversionBatch(ready.Jobs, new Identity(ready.Workstation), ready.Factory(converter, ready.Workstation));
        Assert.AreEqual(new RecordingConversionBatchResult(1, 0, 0), await batch.RunAsync()); Assert.IsTrue(File.Exists(ready.Output));
        var saved = await ready.Jobs.GetAsync(ready.Pair.Conversion.Job.Id);
        Assert.AreEqual(new RecordingConversionBatchResult(0, 0, 0), await batch.RunAsync()); Assert.AreEqual(1, converter.Calls);
        Assert.AreEqual(saved, await ready.Jobs.GetAsync(ready.Pair.Conversion.Job.Id));
        var foreign = new RecordingConversionBatch(ready.Jobs, new Identity(Guid.NewGuid()), ready.Factory(converter, Guid.NewGuid()));
        Assert.AreEqual(new RecordingConversionBatchResult(0, 0, 0), await foreign.RunAsync());
    }
    private sealed class Identity(Guid id) : IWorkstationIdentity
    { public Task<Guid?> GetAsync(CancellationToken token = default) => Task.FromResult<Guid?>(id); public Task<Guid> InitializeAsync(CancellationToken token = default) => throw new AssertFailedException(); }
}

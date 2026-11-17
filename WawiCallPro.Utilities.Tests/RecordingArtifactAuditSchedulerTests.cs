using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WawiCallPro.Utilities.Operations;
using WawiCallPro.Utilities.Persistence;

namespace WawiCallPro.Utilities.Tests;

[TestClass]
public sealed class RecordingArtifactAuditSchedulerTests
{
    [TestMethod]
    public async Task StartingDoesNoAuditAndAsynchronousDisposalWaitsForTheCanceledWorker()
    {
        var audit = new PausedAudit(); await using var scheduler = new RecordingArtifactAuditScheduler(audit, NullLogger<RecordingArtifactAuditScheduler>.Instance);
        var disposing = Task.CompletedTask;
        try
        {
            await scheduler.StartAsync(CancellationToken.None); Assert.AreEqual(0, audit.Calls);
            scheduler.Request(); await audit.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
            for (var index = 0; index < 10; index++) scheduler.Request();
            disposing = scheduler.DisposeAsync().AsTask(); await audit.Cleaning.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.IsFalse(disposing.IsCompleted);
        }
        finally { audit.Cleanup.TrySetResult(); }
        await disposing.WaitAsync(TimeSpan.FromSeconds(3)); Assert.AreEqual(1, audit.Calls);
        scheduler.Request(); await scheduler.DisposeAsync(); Assert.AreEqual(1, audit.Calls);
    }
    [TestMethod]
    public async Task AWorkerFailureIsSanitizedAndDoesNotPreventTheNextRequest()
    {
        var audit = new FailingAudit(); var log = new SchedulerLog();
        await using var scheduler = new RecordingArtifactAuditScheduler(audit, log); await scheduler.StartAsync(CancellationToken.None);
        scheduler.Request(); await log.Failed.Task.WaitAsync(TimeSpan.FromSeconds(3));
        scheduler.Request(); await audit.Repeated.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await scheduler.StopAsync(CancellationToken.None); Assert.AreEqual(2, audit.Calls);
    }
    private sealed class PausedAudit : IRecordingArtifactReconciliation
    {
        internal int Calls;
        internal readonly TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Cleaning = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Cleanup = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<RecordingArtifactReconciliationResult> ReconcileAsync(CancellationToken token = default)
        {
            Interlocked.Increment(ref Calls); Started.TrySetResult();
            try { await Task.Delay(Timeout.Infinite, token); }
            finally { Cleaning.TrySetResult(); await Cleanup.Task; }
            return new(0, 0, 0, 0, 0);
        }
    }
    private sealed class FailingAudit : IRecordingArtifactReconciliation
    {
        internal int Calls;
        internal readonly TaskCompletionSource Repeated = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<RecordingArtifactReconciliationResult> ReconcileAsync(CancellationToken token = default)
        {
            if (Interlocked.Increment(ref Calls) == 1) throw new IOException("private-content");
            Repeated.TrySetResult(); return Task.FromResult(new RecordingArtifactReconciliationResult(0, 0, 0, 0, 0));
        }
    }
    private sealed class SchedulerLog : ILogger<RecordingArtifactAuditScheduler>
    {
        internal readonly TaskCompletionSource Failed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool IsEnabled(LogLevel level) => true; public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? error, Func<TState, Exception?, string> formatter)
        { Assert.IsNull(error); Assert.IsTrue(OperationEvents.TryGetEvent(state, out var value)); Assert.AreEqual(OperationEvent.RecordingArtifactAuditIncomplete, value); Failed.TrySetResult(); }
    }
}

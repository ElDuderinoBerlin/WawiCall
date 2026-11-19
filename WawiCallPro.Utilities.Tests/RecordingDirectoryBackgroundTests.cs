using Microsoft.Extensions.Logging;
using WawiCallPro.Utilities.Operations;
using WawiCallPro.Utilities.Persistence;

namespace WawiCallPro.Utilities.Tests;

[TestClass]
public sealed class RecordingDirectoryBackgroundTests
{
    [TestMethod]
    [DataRow("unknown")][DataRow("temporary")][DataRow("link")][DataRow("clean")][DataRow("failure")]
    public async Task InventoryRunsAfterAuditAndLogsOnlyClosedEvents(string mode)
    {
        var audit = new Audit(); var inventory = new Inventory(audit, mode); var log = new InventoryLog();
        await using var worker = new RecordingArtifactAuditScheduler(audit, log, inventory);
        await worker.StartAsync(CancellationToken.None);
        Assert.AreEqual(0, inventory.Calls);
        worker.Request(); await inventory.Completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (mode != "clean") await log.First.Task.WaitAsync(TimeSpan.FromSeconds(5));
        // Stop waits for the event emitted after the inventory adapter returns.
        await worker.StopAsync(CancellationToken.None);
        Assert.AreEqual(1, inventory.Calls);
        if (mode == "clean") Assert.AreEqual(0, log.Events.Count);
        else Assert.AreEqual(mode == "failure" ? OperationEvent.RecordingDirectoryInventoryIncomplete : OperationEvent.RecordingDirectoryReviewRequired, log.Events.Single());
    }

    [TestMethod]
    public async Task InventoryFailureDoesNotPreventARepeatedRequest()
    {
        var audit = new Audit(); var inventory = new Inventory(audit, "failure"); var log = new InventoryLog();
        await using var worker = new RecordingArtifactAuditScheduler(audit, log, inventory);
        await worker.StartAsync(CancellationToken.None); worker.Request();
        await log.First.Task.WaitAsync(TimeSpan.FromSeconds(5));
        worker.Request(); await log.Second.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await worker.StopAsync(CancellationToken.None); Assert.AreEqual(2, inventory.Calls);
    }

    [TestMethod]
    public async Task ShutdownCancelsInventoryAndWaitsForCleanupWithoutWarning()
    {
        var audit = new Audit(); var inventory = new PausedInventory(); var log = new InventoryLog();
        await using var worker = new RecordingArtifactAuditScheduler(audit, log, inventory);
        await worker.StartAsync(CancellationToken.None); worker.Request(); await inventory.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var disposing = worker.DisposeAsync().AsTask();
        try
        {
            await inventory.Cleaning.Task.WaitAsync(TimeSpan.FromSeconds(5)); Assert.IsFalse(disposing.IsCompleted);
        }
        finally { inventory.Cleanup.TrySetResult(); }
        await disposing.WaitAsync(TimeSpan.FromSeconds(5)); Assert.AreEqual(0, log.Events.Count);
    }

    private sealed class Audit : IRecordingArtifactReconciliation
    {
        internal int Calls;
        public Task<RecordingArtifactReconciliationResult> ReconcileAsync(CancellationToken token = default)
        { token.ThrowIfCancellationRequested(); Calls++; return Task.FromResult(new RecordingArtifactReconciliationResult(0, 0, 0, 0, 0)); }
    }
    private sealed class Inventory(Audit audit, string mode) : IRecordingDirectoryInventory
    {
        internal int Calls;
        internal readonly TaskCompletionSource Completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<RecordingDirectoryInventoryResult> ReadAsync(CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested(); Calls++; Assert.AreEqual(Calls, audit.Calls); Completed.TrySetResult();
            if (mode == "failure") throw new IOException("private-path-and-database-details");
            return Task.FromResult(new RecordingDirectoryInventoryResult(2, mode == "unknown" ? 1 : 0, mode == "temporary" ? 1 : 0, mode == "link" ? 1 : 0,
                [new("private-recording.wav", mode == "link")]));
        }
    }
    private sealed class PausedInventory : IRecordingDirectoryInventory
    {
        internal readonly TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Cleaning = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Cleanup = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<RecordingDirectoryInventoryResult> ReadAsync(CancellationToken token = default)
        {
            Started.TrySetResult();
            try { await Task.Delay(Timeout.Infinite, token); }
            finally { Cleaning.TrySetResult(); await Cleanup.Task; }
            return new(0, 0, 0, 0, []);
        }
    }
    private sealed class InventoryLog : ILogger<RecordingArtifactAuditScheduler>
    {
        internal readonly List<OperationEvent> Events = [];
        internal readonly TaskCompletionSource First = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Second = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool IsEnabled(LogLevel level) => true;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> formatter)
        {
            Assert.IsNull(error); Assert.AreEqual(LogLevel.Warning, level); Assert.IsTrue(OperationEvents.TryGetEvent(state, out var operation));
            Assert.AreEqual(operation.ToString(), formatter(state, error)); Events.Add(operation);
            First.TrySetResult(); if (Events.Count == 2) Second.TrySetResult();
        }
    }
}

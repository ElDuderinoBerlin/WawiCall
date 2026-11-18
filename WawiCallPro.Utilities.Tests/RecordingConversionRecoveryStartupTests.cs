using Microsoft.Extensions.Logging;
using WawiCallPro.Utilities.Operations;
using WawiCallPro.Utilities.Persistence;

namespace WawiCallPro.Utilities.Tests;

[TestClass]
public sealed class RecordingConversionRecoveryStartupTests
{
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
    private sealed class Queue(Action requested) : IRecordingConversionQueue
    { internal int Requests; public void Request() { requested(); Requests++; } }
    private sealed class Interrupted(Func<CancellationToken, Task<InterruptedMp3RecoveryResult>> recover) : IInterruptedMp3Recovery
    { public Task<InterruptedMp3RecoveryResult> RecoverAsync(CancellationToken token = default) => recover(token); }
    private sealed class Recovery(Func<CancellationToken, Task<RecordingConversionRecoveryResult>> recover) : IRecordingConversionRecovery
    { public Task<RecordingConversionRecoveryResult> RecoverAsync(CancellationToken token = default) => recover(token); }
    private sealed class StartupLog : ILogger<RecordingConversionRecoveryStartup>
    {
        internal int Warnings;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        { Assert.IsNull(exception); Assert.IsTrue(OperationEvents.TryGetEvent(state, out var operation)); Assert.AreEqual(OperationEvent.RecordingConversionRecoveryIncomplete, operation); Warnings++; }
    }
}

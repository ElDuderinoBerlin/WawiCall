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
        var worker = new RecordingConversionRecoveryStartup(recovery, new StartupLog()); await worker.StartAsync(CancellationToken.None);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10)); await worker.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        await worker.DisposeAsync(); Assert.AreEqual(1, calls); Assert.IsTrue(exited);
    }
    [TestMethod]
    [DataRow(false)][DataRow(true)]
    public async Task OneCompletedPassIsObservedAndNativeFailuresAreNotLogged(bool fail)
    {
        var calls = 0; var log = new StartupLog();
        var recovery = new Recovery(token => { calls++; if (fail) throw new IOException("private-native-path"); return Task.FromResult(new RecordingConversionRecoveryResult(0, 0, 0)); });
        await using var worker = new RecordingConversionRecoveryStartup(recovery, log);
        await worker.StartAsync(CancellationToken.None); await worker.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.AreEqual(1, calls); Assert.AreEqual(fail ? 1 : 0, log.Warnings);
    }
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

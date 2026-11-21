using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WawiCallPro.App.Hosting;
using WawiCallPro.Automation;
using WawiCallPro.Utilities.Hosting;

namespace WawiCallPro.App.Tests;

[TestClass]
public sealed class AppComShutdownTests
{
    [TestMethod]
    public async Task AllShutdownCallersAwaitTheSameWorkerBeforeDependenciesCanStop()
    {
        await using var h = await AppComTelephonyTests.Harness.CreateAsync();
        await AppComTelephonyTests.Done(h.App, h.App.ConnectTelephony());
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Fixture.Phone.BeforeDial = async token => { entered.TrySetResult(); await release.Task; token.ThrowIfCancellationRequested(); };
        var first = h.App.Dial("00123", h.Company, Guid.NewGuid().ToString("D"));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var queued = h.App.Dial("00456", h.Company, Guid.NewGuid().ToString("D"));
        var operations = h.Services.GetRequiredService<AutomationOperations>();
        var stopping = new AppAutomationShutdown(h.State, operations).StopAsync(default);
        var second = operations.DisposeAsync().AsTask();
        try
        {
            await AppComTelephonyTests.Until(() => h.Fixture.Phone.LastDialToken.IsCancellationRequested);
            Assert.IsFalse(stopping.IsCompleted); Assert.IsFalse(second.IsCompleted);
            Assert.AreEqual(0, h.Fixture.Phone.Disconnects);
            Assert.AreEqual("Stopping", h.State.GetStatus().State);
            Assert.Throws<System.Runtime.InteropServices.COMException>(() => h.App.ConnectTelephony());
        }
        finally { release.TrySetResult(); }
        await Task.WhenAll(stopping, second).WaitAsync(TimeSpan.FromSeconds(4));
        Assert.AreEqual("Canceled", operations.Get(first).State);
        Assert.AreEqual("Canceled", operations.Get(queued).State);
        Assert.AreEqual(1, h.Fixture.Phone.Attempts); Assert.IsFalse(operations.HasPendingOperations);
        Assert.AreEqual(0, h.Fixture.Phone.Disconnects);
        await h.Adapter.StopAsync(default); Assert.AreEqual(1, h.Fixture.Phone.Disconnects);
    }
    [TestMethod]
    public async Task MetadataStartupIsSerializedWithStopAndCannotStartLaterServices()
    {
        var log = new List<string>();
        var state = new AppAutomationState(new ApplicationDescriptor("WawiCallPro", "Pro"), TimeProvider.System);
        var foundation = new Service("foundation", log);
        var metadata = new Service("metadata", log) { Block = true };
        var late = new Service("late", log);
        var pipeline = new AppStartupPipeline([() => foundation, () => metadata, () => late], 1, state)
        { MetadataServices = [() => metadata, () => late] };
        var starting = pipeline.StartAsync(default);
        await metadata.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var stopping = pipeline.StopAsync(default); var again = pipeline.StopAsync(default);
        Assert.AreSame(stopping, again); Assert.IsFalse(stopping.IsCompleted);
        var resuming = pipeline.ResumeAsync(default);
        metadata.Release.TrySetResult();
        await Task.WhenAll(starting, stopping).WaitAsync(TimeSpan.FromSeconds(3));
        await Assert.ThrowsAsync<OperationCanceledException>(() => resuming);
        Assert.AreEqual("Stopping", state.GetStatus().State);
        CollectionAssert.AreEqual(new[] { "start:foundation", "start:metadata", "stop:metadata", "stop:foundation" }, log);
        await pipeline.StartAsync(default);
        Assert.AreEqual(4, log.Count); Assert.AreEqual("Stopping", state.GetStatus().State);
    }
    [TestMethod]
    public async Task IdleShutdownAndConcurrentExportHaveOneAtomicWinner()
    {
        for (var i = 0; i < 200; i++)
        {
            var clock = new Clock(); var state = new AppAutomationState(new ApplicationDescriptor("WawiCallPro", "Pro"), clock);
            state.Ready(); clock.Now += TimeSpan.FromSeconds(31);
            var exported = false; var stopped = false;
            await Task.WhenAll(Task.Run(() =>
            {
                try { state.ServerLock(true); exported = true; }
                catch (AutomationFault error) { Assert.AreEqual(AutomationFailure.Stopping, error.Failure); }
            }), Task.Run(() => stopped = state.TryIdleShutdown(false)));
            Assert.AreNotEqual(exported, stopped);
            if (exported) state.ServerLock(false);
        }
    }
    private sealed class Clock : TimeProvider
    {
        internal DateTimeOffset Now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class Service(string name, List<string> log) : IHostedService
    {
        internal bool Block;
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task StartAsync(CancellationToken cancellationToken)
        { log.Add("start:" + name); Entered.TrySetResult(); if (Block) await Release.Task; }
        public Task StopAsync(CancellationToken cancellationToken) { log.Add("stop:" + name); return Task.CompletedTask; }
    }
}

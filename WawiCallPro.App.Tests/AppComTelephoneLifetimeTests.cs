using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using WawiCallPro.Automation;
using WawiCallPro.Interfaces.Telephony;

namespace WawiCallPro.App.Tests;

[TestClass]
public sealed class AppComTelephoneLifetimeTests
{
    [TestMethod]
    public async Task HundredClientRetriesDispatchOneBackendCallAndRemainDeduplicatedAfterHangup()
    {
        await using var h = await AppComTelephonyTests.Harness.CreateAsync(); var app = h.App;
        await AppComTelephonyTests.Done(app, app.ConnectTelephony());
        var request = Guid.NewGuid().ToString("D");
        var ids = await Task.WhenAll(Enumerable.Range(0, 100).Select(_ => Task.Run(() => h.NewClient().Dial("00123", h.Company, request))));
        Assert.AreEqual(1, ids.Distinct().Count());
        var result = await AppComTelephonyTests.Done(app, ids[0]);
        Assert.AreEqual("Succeeded", result.State); Assert.AreEqual(1, h.Fixture.Phone.Attempts);
        Assert.AreEqual(unchecked((int)0x8004A106), Assert.Throws<COMException>(() => app.Dial("00456", h.Company, request)).HResult);
        Assert.AreEqual("Succeeded", (await AppComTelephonyTests.Done(app, app.HangUp(result.CallId))).State);
        Assert.AreEqual(ids[0], app.Dial("00123", h.Company, request));
        Assert.AreEqual(1, h.Fixture.Phone.Attempts, "A retry of a completed request must not place another call.");
        Assert.AreEqual(0, h.Presentation.Shown);
    }
    [TestMethod]
    public async Task LosingTheLastClientDuringDialDoesNotCancelTheCallOrLetTheHostGoIdle()
    {
        var clock = new TestClock();
        await using var h = await AppComTelephonyTests.Harness.CreateAsync(TimeSpan.FromSeconds(10), clock);
        await AppComTelephonyTests.Done(h.App, h.App.ConnectTelephony());
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var accepted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Fixture.Phone.BeforeDial = _ => { entered.TrySetResult(); return accepted.Task; };
        h.App = null!;
        var (weak, id) = SubmitFromTemporaryClient(h);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        Assert.IsFalse(weak.IsAlive, "The submitted command must not retain its facade.");
        Assert.IsFalse(h.Fixture.Phone.LastDialToken.IsCancellationRequested);
        var operations = h.Services.GetRequiredService<AutomationOperations>();
        clock.Advance(TimeSpan.FromSeconds(31));
        Assert.IsFalse(h.State.TryIdleShutdown(operations.HasPendingOperations));
        accepted.SetResult();
        await AppComTelephonyTests.Until(() => operations.Get(id).State == "Succeeded");
        var call = h.Fixture.Phone.GetCalls().Single(); Assert.AreEqual(CallState.Dialing, call.State);
        Assert.IsFalse(h.State.TryIdleShutdown(false)); Assert.AreEqual(0, h.Fixture.Phone.Disconnects);
        await h.Adapter.HangUpAsync(call.Id, default);
        clock.Advance(TimeSpan.FromSeconds(29)); Assert.IsFalse(h.State.TryIdleShutdown(false));
        clock.Advance(TimeSpan.FromSeconds(2)); Assert.IsTrue(h.State.TryIdleShutdown(false));
        Assert.AreEqual(1, h.Fixture.Phone.Attempts); Assert.AreEqual(0, h.Presentation.Shown);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference Client, string Operation) SubmitFromTemporaryClient(AppComTelephonyTests.Harness h)
    {
        var app = h.NewClient();
        return (new WeakReference(app), app.Dial("00123", h.Company, Guid.NewGuid().ToString("D")));
    }
    private sealed class TestClock : TimeProvider
    {
        private readonly object _gate = new();
        private DateTimeOffset _now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() { lock (_gate) return _now; }
        internal void Advance(TimeSpan value) { lock (_gate) _now += value; }
    }
}

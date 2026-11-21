using WawiCallPro.App.Hosting;
using WawiCallPro.Automation;
using WawiCallPro.Utilities.Hosting;

namespace WawiCallPro.App.Tests;

[TestClass]
public sealed class AppComLifetimeTests
{
    [TestMethod]
    public void NativeHoldsPreventIdleAndTheLastReleaseRestartsTheIdlePeriod()
    {
        var clock = new Clock(); var state = Create(clock); state.Ready();
        state.ServerLock(true); state.ServerLock(true);
        clock.Advance(60); Assert.IsFalse(state.TryIdleShutdown(false));
        state.ServerLock(false); clock.Advance(60); Assert.IsFalse(state.TryIdleShutdown(false));
        state.ServerLock(false); clock.Advance(29); Assert.IsFalse(state.TryIdleShutdown(false));
        clock.Advance(2); Assert.IsTrue(state.TryIdleShutdown(false));
        Assert.AreEqual("Stopping", state.GetStatus().State);
        Assert.AreEqual(AutomationFailure.Stopping, Assert.Throws<AutomationFault>(() => state.ServerLock(true)).Failure);
    }
    [TestMethod]
    public void ReleasesDuringExplicitShutdownSucceedAndLateStartupCannotResurrectTheHost()
    {
        var state = Create(new Clock()); state.Ready(); state.ServerLock(true); state.Stopping();
        state.ServerLock(false); Assert.AreEqual(0, state.ServerLocks);
        state.Ready(); state.Failed(new InvalidOperationException("private-details"));
        Assert.AreEqual("Stopping", state.GetStatus().State);
        Assert.AreEqual(AutomationFailure.Stopping, Assert.Throws<AutomationFault>(() => state.EnterUserMode()).Failure);
    }
    [TestMethod]
    public void NativeHoldsAreBoundedAndUnderflowCannotCorruptOwnership()
    {
        var state = Create(new Clock()); state.Ready();
        Assert.AreEqual(AutomationFailure.Conflict, Assert.Throws<AutomationFault>(() => state.ServerLock(false)).Failure);
        for (var i = 0; i < 4096; i++) state.ServerLock(true);
        Assert.AreEqual(AutomationFailure.Busy, Assert.Throws<AutomationFault>(() => state.ServerLock(true)).Failure);
        Assert.AreEqual(4096, state.ServerLocks);
        for (var i = 0; i < 4096; i++) state.ServerLock(false);
        Assert.AreEqual(0, state.ServerLocks);
    }
    [TestMethod]
    public void VisibleUserOwnershipAndPendingWorkKeepTheHostAlive()
    {
        var clock = new Clock(); var state = Create(clock); state.Ready();
        clock.Advance(60); Assert.IsFalse(state.TryIdleShutdown(true));
        state.EnterUserMode(); state.Visible(); state.UpdateVisibility(false);
        clock.Advance(60); Assert.IsFalse(state.TryIdleShutdown(false), "A user-owned window in the tray must not become an abandoned COM host.");
    }
    private static AppAutomationState Create(Clock clock) => new(new ApplicationDescriptor("WawiCallPro", "Pro"), clock);
    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => _now;
        internal void Advance(int seconds) => _now += TimeSpan.FromSeconds(seconds);
    }
}

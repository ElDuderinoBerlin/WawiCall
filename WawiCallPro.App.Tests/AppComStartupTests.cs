using Microsoft.Extensions.Hosting;
using WawiCallPro.App.Hosting;
using WawiCallPro.Utilities.Hosting;
using WawiCallPro.Utilities.Licensing;

namespace WawiCallPro.App.Tests;

[TestClass]
public sealed class AppComStartupTests
{
    [TestMethod]
    public async Task ComFoundationChecksBothLexwareProductsIndependently()
    {
        var registry = new RegistryProbe();
        var installation = new WawiCallPro.Utilities.Plugins.LexwareInstallation(registry,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<WawiCallPro.Utilities.Plugins.LexwareInstallation>.Instance);
        await new AppComInstallationCheck(installation).StartAsync(CancellationToken.None);
        Assert.AreEqual(2, registry.Checks); Assert.IsFalse(installation.Current.IsAvailable);
    }
    [TestMethod]
    public async Task FirstUserModeRevalidatesLicenseBeforeDeferredWork()
    {
        var state = State(); var log = new List<string>();
        var first = new Service("foundation", log); var worker = new Service("worker", log);
        var validations = 0;
        var pipeline = new AppStartupPipeline([() => first, () => worker], 1, state)
        {
            RevalidateBeforeUserMode = _ => { if (++validations == 1) throw new LicenseAccessException(LicenseStatus.Expired); return Task.CompletedTask; }
        };
        await pipeline.StartAsync(CancellationToken.None); state.EnterUserMode();
        await Assert.ThrowsExactlyAsync<LicenseAccessException>(() => pipeline.ResumeAsync(CancellationToken.None));
        Assert.AreEqual("LicenseRequired", state.GetStatus().State); Assert.IsFalse(log.Contains("start:worker"));
        await pipeline.ResumeAsync(CancellationToken.None); await pipeline.ResumeAsync(CancellationToken.None);
        Assert.AreEqual(2, validations); Assert.AreEqual(1, log.Count(entry => entry == "start:worker"));
        await pipeline.StopAsync(CancellationToken.None);
    }
    [TestMethod]
    public async Task BackgroundWorkStartsOnlyOnceAfterExplicitUserMode()
    {
        var state = State(); var log = new List<string>();
        var foundation = new Service("config", log); var business = new Service("business", log);
        var pipeline = new AppStartupPipeline([() => foundation, () => business], 1, state);
        await pipeline.StartAsync(CancellationToken.None);
        Assert.AreEqual("Ready", state.GetStatus().State);
        CollectionAssert.AreEqual(new[] { "start:config" }, log);
        state.EnterUserMode();
        await Task.WhenAll(pipeline.ResumeAsync(CancellationToken.None), pipeline.ResumeAsync(CancellationToken.None));
        CollectionAssert.AreEqual(new[] { "start:config", "start:business" }, log);
        await pipeline.StopAsync(CancellationToken.None);
        CollectionAssert.AreEqual(new[] { "start:config", "start:business", "stop:business", "stop:config" }, log);
    }

    [TestMethod]
    public async Task LicenseFailureRemainsQueryableAndCannotStartBusinessWork()
    {
        var state = State(); var log = new List<string>(); var checks = 0;
        var license = new Service("license", log, () => { if (++checks == 1) throw new LicenseAccessException(LicenseStatus.Missing); });
        var business = new Service("business", log);
        var pipeline = new AppStartupPipeline([() => license, () => business], 1, state);
        await pipeline.StartAsync(CancellationToken.None);
        Assert.AreEqual("LicenseRequired", state.GetStatus().State);
        Assert.IsFalse(state.GetStatus().IsVisible); Assert.IsFalse(log.Contains("start:business"));
        state.EnterUserMode(); await pipeline.ResumeAsync(CancellationToken.None);
        Assert.AreEqual("Ready", state.GetStatus().State); Assert.AreEqual(2, checks);
        await pipeline.StopAsync(CancellationToken.None);
    }

    [TestMethod]
    public async Task ConstructionFailureKeepsStatusSafeAndStopsAttemptedServices()
    {
        var state = State(); var log = new List<string>(); var first = new Service("first", log);
        var pipeline = new AppStartupPipeline([() => first, () => throw new InvalidOperationException("private-source-detail")], 2, state);
        await pipeline.StartAsync(CancellationToken.None);
        Assert.AreEqual("Failed", state.GetStatus().State);
        Assert.AreEqual("ProviderUnavailable", state.GetStatus().ErrorCode);
        await pipeline.StopAsync(CancellationToken.None);
        CollectionAssert.AreEqual(new[] { "start:first", "stop:first" }, log);
    }

    [TestMethod]
    public void StatusAndUserOwnershipNeverCreateAWindow()
    {
        var state = State(); Assert.AreEqual("Starting", state.GetStatus().State);
        Assert.AreEqual("Stopped", state.GetStatus().SipState);
        state.Ready(); state.EnterUserMode();
        Assert.IsFalse(state.GetStatus().IsVisible);
        Assert.IsFalse(state.TryIdleShutdown(false));
        state.Stopping();
        Assert.AreEqual("Stopping", state.GetStatus().State);
    }

    private static AppAutomationState State() => new(new ApplicationDescriptor("WawiCallPro", "Pro"), TimeProvider.System);
    private sealed class RegistryProbe : WawiCallPro.Utilities.Plugins.ILexwareRegistryProbe
    {
        internal int Checks;
        public bool IsProInstalled() { Checks++; return false; }
        public bool IsFinOfficeInstalled() { Checks++; return false; }
    }
    private sealed class Service(string name, List<string> log, Action? start = null) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) { log.Add("start:" + name); start?.Invoke(); return Task.CompletedTask; }
        public Task StopAsync(CancellationToken cancellationToken) { log.Add("stop:" + name); return Task.CompletedTask; }
    }
}

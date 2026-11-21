using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using WawiCallPro.App.Hosting;
using WawiCallPro.Automation;
using WawiCallPro.Automation.Contracts;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Utilities.Licensing;

namespace WawiCallPro.App.Tests;

[TestClass]
public sealed class AppComTelephonyTests
{
    [TestMethod]
    public async Task SetupFailureAndRegistrationFailureAreObservedWithoutUiOrLeakedObservers()
    {
        await using var h = await Harness.CreateAsync();
        h.Fixture.Phone.ConnectFailure = new TelephonyException("AccountMissing");
        var failed = await Done(h.App, h.App.ConnectTelephony());
        Assert.AreEqual("NotReady", failed.ErrorCode); Assert.AreEqual("Failed", h.App.GetStatus().SipState);
        Assert.AreEqual(0, h.Fixture.Phone.RegistrationSubscribers); Assert.AreEqual(0, h.Presentation.Shown);
    }
    [TestMethod]
    public async Task ConnectDialGetCallAndHangupUseTheSharedRuntimeWithoutPresentation()
    {
        await using var h = await Harness.CreateAsync(); var app = h.App;
        Assert.AreEqual(0, h.Fixture.Phone.Connects, "Constructing COM must not register an account.");
        Assert.AreEqual("Succeeded", (await Done(app, app.ConnectTelephony())).State);
        Assert.AreEqual("Registered", app.GetStatus().SipState);
        var dial = await Done(app, app.Dial("00123", h.Company, Guid.NewGuid().ToString("D")));
        Assert.AreEqual("Succeeded", dial.State); Assert.AreNotEqual(dial.Id, dial.CallId);
        Assert.AreEqual("Dialing", app.GetCall(dial.CallId).State);
        var call = h.Fixture.Phone.GetCalls().Single();
        Assert.AreEqual(h.Fixture.A, call.Company); Assert.AreEqual("00123", call.RemoteAddress.OriginalValue);
        await h.Fixture.Phone.PublishAsync(call with { Revision = 2, State = CallState.Connected, AnsweredAtUtc = DateTimeOffset.UtcNow });
        Assert.AreEqual("Connected", app.GetCall(dial.CallId).State);
        Assert.AreEqual("Succeeded", (await Done(app, app.HangUp(dial.CallId))).State);
        Assert.AreEqual("Ended", app.GetCall(dial.CallId).State);
        Assert.AreEqual(0, h.Presentation.Shown);
        Assert.AreEqual(unchecked((int)0x8004A101), Assert.Throws<COMException>(() => app.GetCall(Guid.NewGuid().ToString("D"))).HResult);
    }
    [TestMethod]
    public async Task RegistrationWaitsForARealOutcomeAndReleasesItsObserver()
    {
        await using var h = await Harness.CreateAsync(); h.Fixture.Phone.RegisterOnConnect = false;
        var id = h.App.ConnectTelephony();
        await Until(() => h.Fixture.Phone.Connects == 1);
        Assert.AreEqual("Running", h.App.GetOperation(id).State);
        h.Fixture.Phone.Register(RegistrationState.Failed);
        var result = await Done(h.App, id); Assert.AreEqual("NotReady", result.ErrorCode);
        Assert.AreEqual(0, h.Fixture.Phone.RegistrationSubscribers); Assert.AreEqual(0, h.Fixture.Phone.Attempts);
    }
    [TestMethod]
    public async Task RegistrationTimeoutDoesNotInventSuccessOrLeaveAnObserver()
    {
        await using var h = await Harness.CreateAsync(TimeSpan.FromMilliseconds(150)); h.Fixture.Phone.RegisterOnConnect = false;
        var result = await Done(h.App, h.App.ConnectTelephony());
        Assert.AreEqual("Timeout", result.ErrorCode); Assert.AreEqual(0, h.Fixture.Phone.RegistrationSubscribers);
        Assert.AreEqual(0, h.Fixture.Phone.Attempts);
    }
    [TestMethod]
    public async Task GatesAndProviderFailuresHaveSafeOperationCodes()
    {
        await using var h = await Harness.CreateAsync();
        Assert.AreEqual("NotReady", (await Done(h.App, h.App.Dial("00123", h.Company, Guid.NewGuid().ToString("D")))).ErrorCode);
        await Done(h.App, h.App.ConnectTelephony());
        h.Fixture.License.Status = LicenseStatus.Revoked;
        Assert.AreEqual("AccessDenied", (await Done(h.App, h.App.Dial("00123", h.Company, Guid.NewGuid().ToString("D")))).ErrorCode);
        h.Fixture.License.Status = LicenseStatus.Valid;
        h.Fixture.Installation.State = new(false, false, false);
        Assert.AreEqual("AccessDenied", (await Done(h.App, h.App.Dial("00123", h.Company, Guid.NewGuid().ToString("D")))).ErrorCode);
        h.Fixture.Installation.State = new(true, true, false);
        h.Fixture.Phone.Failure = new IOException("private-provider-sentinel");
        var failure = await Done(h.App, h.App.Dial("00123", h.Company, Guid.NewGuid().ToString("D")));
        Assert.AreEqual("ProviderUnavailable", failure.ErrorCode); Assert.IsFalse(failure.Message.Contains("sentinel", StringComparison.Ordinal));
        h.Fixture.Phone.Failure = null;
        var dial = await Done(h.App, h.App.Dial("00123", h.Company, Guid.NewGuid().ToString("D")));
        Assert.AreEqual("Busy", (await Done(h.App, h.App.Dial("00456", h.Company, Guid.NewGuid().ToString("D")))).ErrorCode);
        h.Fixture.License.Status = LicenseStatus.Revoked; h.Fixture.Source.Available = false;
        h.State.Failed(new LicenseAccessException(LicenseStatus.Revoked));
        Assert.AreEqual("Dialing", h.App.GetCall(dial.CallId).State);
        Assert.AreEqual("NotReady", (await Done(h.App, h.App.Dial("00456", h.Company, Guid.NewGuid().ToString("D")))).ErrorCode);
        Assert.AreEqual("Succeeded", (await Done(h.App, h.App.HangUp(dial.CallId))).State);
    }

    internal static async Task<IOperationV1> Done(IApplicationV1 app, string id)
    {
        IOperationV1? result = null;
        await Until(() => { result = app.GetOperation(id); return result.State is "Succeeded" or "Failed" or "Canceled"; });
        return result!;
    }
    internal static async Task Until(Func<bool> condition)
    {
        var limit = DateTime.UtcNow.AddSeconds(8);
        while (!condition()) { if (DateTime.UtcNow >= limit) throw new TimeoutException("Synthetic phone operation did not finish."); await Task.Delay(10); }
    }
    internal sealed class Harness : IAsyncDisposable
    {
        internal readonly ComTelephonyFixture Fixture = new();
        internal readonly NoPresentation Presentation = new();
        internal AppAutomationState State = null!;
        internal AppAutomationTelephony Adapter = null!;
        internal ServiceProvider Services = null!;
        internal IApplicationV1 App = null!;
        internal string Company = "";
        internal IApplicationV1 NewClient() => new AppAutomationFactory(Services.GetRequiredService<IAutomationApplicationFactory>(), State).Create();
        internal static async Task<Harness> CreateAsync(TimeSpan? timeout = null, TimeProvider? clock = null)
        {
            var h = new Harness();
            h.State = new(h.Fixture.Application, clock ?? TimeProvider.System) { Runtime = h.Fixture.Source };
            h.State.PublishCatalog([new(h.Fixture.A, "Firma A"), new(h.Fixture.B, "Firma B")], AppAutomationCatalog.BaseViews, h.Fixture.Source.ActivePluginId, true);
            h.Adapter = new(h.Fixture.Service, h.Fixture.Service, h.State);
            await h.Adapter.StartAsync(default); h.State.Ready();
            var services = new ServiceCollection(); services.AddSingleton<IAutomationEnvironment>(h.State);
            services.AddSingleton<IPartySearch, NoSearch>(); services.AddSingleton<IAutomationPresentation>(h.Presentation);
            services.AddSingleton<IAutomationTelephony>(h.Adapter); services.AddWawiAutomation(new() { CommandTimeout = timeout ?? TimeSpan.FromSeconds(3) });
            h.Services = services.BuildServiceProvider(); h.App = h.NewClient();
            using var catalog = System.Text.Json.JsonDocument.Parse(h.App.GetCompaniesJson());
            h.Company = catalog.RootElement.GetProperty("Items")[0].GetProperty("Key").GetString()!;
            return h;
        }
        public async ValueTask DisposeAsync()
        {
            await Services.DisposeAsync(); await Adapter.StopAsync(default); await Fixture.DisposeAsync();
        }
    }
    internal sealed class NoPresentation : IAutomationPresentation
    {
        internal int Shown;
        public Task ShowAsync(PresentationRequest request, CancellationToken token) { Shown++; throw new AssertFailedException("No UI expected."); }
    }
    private sealed class NoSearch : IPartySearch
    { public Task<PartySearchResult> SearchAsync(PartySearchQuery query, CancellationToken token = default) => throw new AssertFailedException("No party query expected."); }
}

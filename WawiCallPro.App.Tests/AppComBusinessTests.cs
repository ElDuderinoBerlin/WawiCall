using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using WawiCallPro.App.Hosting;
using WawiCallPro.Automation;
using WawiCallPro.Automation.Contracts;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Utilities.Licensing;

namespace WawiCallPro.App.Tests;

[TestClass]
public sealed class AppComBusinessTests
{
    [TestMethod, DataRow(false), DataRow(true)]
    public async Task CustomerAndSupplierUseTheHostBusinessGuardBeforeReadingAndShowing(bool supplier)
    {
        await using var h = new Harness();
        var result = await AppComTelephonyTests.Done(h.App, h.Submit(supplier));
        Assert.AreEqual("Succeeded", result.State); Assert.AreEqual(1, h.Reads); Assert.AreEqual(1, h.Shown);
        Assert.AreEqual(supplier ? PartyKind.Supplier : PartyKind.Customer, h.Last!.Party!.Kind);
        Assert.AreEqual(h.Fixture.A, h.Last.Company);
        Assert.AreEqual("00123", h.Last.Number);
        Assert.IsTrue(h.Fixture.License.Checks >= 4); Assert.IsTrue(h.Fixture.Installation.Checks >= 4);
    }
    [TestMethod, DataRow(LicenseStatus.Missing), DataRow(LicenseStatus.Expired), DataRow(LicenseStatus.Revoked),
        DataRow(LicenseStatus.ConfigurationMissing), DataRow(LicenseStatus.ServerUnavailable), DataRow(LicenseStatus.NotChecked)]
    public async Task InvalidCurrentLicenseBlocksBothRolesBeforeSourceReadsWithoutPrompting(LicenseStatus license)
    {
        await using var h = new Harness(); h.Fixture.License.Status = license;
        foreach (var supplier in new[] { false, true })
            Assert.AreEqual("AccessDenied", (await AppComTelephonyTests.Done(h.App, h.Submit(supplier))).ErrorCode);
        Assert.AreEqual(0, h.Reads); Assert.AreEqual(0, h.Shown);
    }
    [TestMethod, DataRow(false, false, false, false), DataRow(true, false, false, true), DataRow(false, true, false, true),
        DataRow(true, true, false, true), DataRow(true, true, true, false)]
    public async Task InstallationMatrixIsEnforcedForBusinessCommands(bool pro, bool fin, bool failed, bool allowed)
    {
        await using var h = new Harness(); h.Fixture.Installation.State = new(pro, fin, failed);
        var result = await AppComTelephonyTests.Done(h.App, h.Submit(false));
        Assert.AreEqual(allowed ? "Succeeded" : "AccessDenied", allowed ? result.State : result.ErrorCode);
        Assert.AreEqual(allowed ? 1 : 0, h.Reads); Assert.AreEqual(allowed ? 1 : 0, h.Shown);
    }
    [TestMethod]
    public async Task AStaleCompanyCatalogDoesNotAuthorizeACompanyRemovedFromTheSource()
    {
        await using var h = new Harness(); h.Fixture.Source.Catalog = _ => Task.FromResult(ImmutableArray.Create(new Company(h.Fixture.B, "Other company")));
        Assert.AreEqual("AccessDenied", (await AppComTelephonyTests.Done(h.App, h.Submit(false))).ErrorCode);
        Assert.AreEqual(0, h.Reads); Assert.AreEqual(0, h.Shown);
    }
    [TestMethod]
    public async Task RevocationWhileResolvingAPartyPreventsPresentation()
    {
        await using var h = new Harness(); h.OnRead = () => h.Fixture.License.Status = LicenseStatus.Revoked;
        Assert.AreEqual("AccessDenied", (await AppComTelephonyTests.Done(h.App, h.Submit(true))).ErrorCode);
        Assert.AreEqual(1, h.Reads); Assert.AreEqual(0, h.Shown);
    }
    [TestMethod]
    public async Task CurrentCatalogFailureIsRedactedAndCannotReachPresentation()
    {
        await using var h = new Harness(); h.Fixture.Source.Catalog = _ => throw new IOException("private-secret-connection-string");
        var result = await AppComTelephonyTests.Done(h.App, h.Submit(false));
        Assert.AreEqual("ProviderUnavailable", result.ErrorCode); Assert.DoesNotContain("private", result.Message);
        Assert.AreEqual(0, h.Reads); Assert.AreEqual(0, h.Shown);
    }
    [TestMethod]
    public async Task ViewContextCannotBypassTheBusinessGuardBeforePartyResolution()
    {
        await using var h = new Harness(); h.Fixture.License.Status = LicenseStatus.Missing;
        var context = JsonSerializer.Serialize(new { companyKey = h.Company, supplierNumber = "00123" });
        var id = h.App.ShowView("plugin:record", context);
        Assert.AreEqual("AccessDenied", (await AppComTelephonyTests.Done(h.App, id)).ErrorCode);
        Assert.AreEqual(0, h.Reads); Assert.AreEqual(0, h.Shown);
    }
    private sealed class Harness : IAsyncDisposable, IPartySearch, IAutomationPresentation, IAutomationTelephony
    {
        internal readonly ComTelephonyFixture Fixture = new();
        internal readonly ServiceProvider Services;
        internal readonly IApplicationV1 App;
        internal readonly string Company;
        internal int Reads, Shown;
        internal Action? OnRead;
        internal PresentationRequest? Last;
        internal Harness()
        {
            var state = new AppAutomationState(Fixture.Application, TimeProvider.System) { Runtime = Fixture.Source };
            state.PublishCatalog([new(Fixture.A, "Company A"), new(Fixture.B, "Company B")], [new("plugin:record", "Record")], Fixture.Source.ActivePluginId, true);
            state.Ready();
            var access = new AppAutomationBusinessAccess(state, Fixture.License, new("fixture", Guid.NewGuid(), "1.0", true),
                Fixture.Installation, Fixture.Source, Fixture.Failures);
            var services = new ServiceCollection(); services.AddSingleton<IAutomationEnvironment>(state);
            services.AddSingleton<IAutomationBusinessAccess>(access); services.AddSingleton<IPartySearch>(this);
            services.AddSingleton<IAutomationPresentation>(this); services.AddSingleton<IAutomationTelephony>(this);
            services.AddWawiAutomation(); Services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
            App = Services.GetRequiredService<IAutomationApplicationFactory>().Create();
            using var catalog = JsonDocument.Parse(App.GetCompaniesJson());
            Company = catalog.RootElement.GetProperty("Items")[0].GetProperty("Key").GetString()!;
        }
        internal string Submit(bool supplier) => supplier ? App.ShowSupplier(Company, "00123") : App.ShowCustomer(Company, "00123");
        public Task<PartySearchResult> SearchAsync(PartySearchQuery query, CancellationToken cancellationToken = default)
        {
            Reads++; OnRead?.Invoke();
            return Task.FromResult(new PartySearchResult(query, [new Party { Key = new(query.Company, query.Kind!.Value, "party"), Number = "00123", DisplayName = "Synthetic party" }]));
        }
        public Task ShowAsync(PresentationRequest request, CancellationToken cancellationToken) { Shown++; Last = request; return Task.CompletedTask; }
        public Task ConnectAsync(CancellationToken cancellationToken) => throw new AssertFailedException("No telephone call expected.");
        public Task<Guid> DialAsync(CompanyKey company, string destination, CancellationToken cancellationToken) => throw new AssertFailedException("No telephone call expected.");
        public Task HangUpAsync(Guid callId, CancellationToken cancellationToken) => throw new AssertFailedException("No telephone call expected.");
        public AutomationCall GetCall(Guid callId) => throw new AssertFailedException("No telephone call expected.");
        public async ValueTask DisposeAsync() { await Services.DisposeAsync(); await Fixture.DisposeAsync(); }
    }
}

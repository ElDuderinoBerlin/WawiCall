using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using WawiCallPro.Automation.Contracts;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Interfaces.Parties;

namespace WawiCallPro.Automation.Tests;

[TestClass]
public sealed class AutomationTests
{
    [TestMethod, DataRow(1), DataRow(2)]
    public async Task CustomerSupplierAndCompanyBoundariesPreserveExactNumbers(int product)
    {
        await using var fixture = new Fixture((LexwareProductLine)product);
        var keys = fixture.Keys(); var app = fixture.App;
        Assert.AreEqual("Succeeded", (await Done(app, app.ShowCustomer(keys[0], "00123"))).State);
        Assert.AreEqual(new PartyKey(fixture.Environment.A, PartyKind.Customer, "c-a"), fixture.Environment.Last!.Party);
        Assert.AreEqual("Succeeded", (await Done(app, app.ShowSupplier(keys[0], "00123"))).State);
        Assert.AreEqual(PartyKind.Supplier, fixture.Environment.Last!.Party!.Kind);
        await Done(app, app.ShowCustomer(keys[1], "00123"));
        Assert.AreEqual(new PartyKey(fixture.Environment.B, PartyKind.Customer, "c-b"), fixture.Environment.Last!.Party);
        Assert.AreEqual("NotFound", (await Done(app, app.ShowCustomer(keys[0], "123"))).ErrorCode);
        Assert.AreEqual("Ambiguous", (await Done(app, app.ShowCustomer(keys[0], "00777"))).ErrorCode);
        Assert.AreEqual(3, fixture.Environment.Shown);
    }

    [TestMethod]
    public async Task UnicodeNumbersAndCatalogRoundTripWithoutLosingLeadingZeros()
    {
        await using var fixture = new Fixture();
        var json = JsonDocument.Parse(fixture.App.GetCompaniesJson());
        Assert.AreEqual("Müller 東京", json.RootElement.GetProperty("Items")[0].GetProperty("Name").GetString());
        Assert.AreEqual("Succeeded", (await Done(fixture.App, fixture.App.ShowCustomer(fixture.Keys()[0], "Ä-東京"))).State);
        Assert.AreEqual("unicode", fixture.Environment.Last!.Party!.ExternalId);
    }

    [TestMethod]
    public async Task DuplicateRequestIsAtomicAcrossConcurrentClientsAndConflictIsExplicit()
    {
        await using var fixture = new Fixture();
        var factory = fixture.Services.GetRequiredService<IAutomationApplicationFactory>();
        var key = fixture.Keys()[0]; var request = Guid.NewGuid().ToString("D");
        var ids = await Task.WhenAll(Enumerable.Range(0, 100).Select(_ => Task.Run(() => factory.Create().Dial("+4930000000", key, request))));
        Assert.AreEqual(1, ids.Distinct().Count());
        var result = await Done(fixture.App, ids[0]);
        Assert.AreEqual("Succeeded", result.State); Assert.AreNotEqual(result.Id, result.CallId);
        Assert.AreEqual(1, fixture.Environment.Dials);
        var error = Assert.Throws<COMException>(() => fixture.App.Dial("+4930000001", key, request));
        Assert.AreEqual(unchecked((int)0x8004A106), error.HResult);
        Assert.AreEqual("Dialing", fixture.App.GetCall(result.CallId).State);
        await Done(fixture.App, fixture.App.HangUp(result.CallId));
        Assert.AreEqual("Ended", fixture.App.GetCall(result.CallId).State);
    }

    [TestMethod]
    public async Task InvalidInputsCannotReachTheSourceOrExecuteACommand()
    {
        await using var fixture = new Fixture(); var app = fixture.App; var key = fixture.Keys()[0];
        foreach (var number in new[] { "", " ", "00\n123", new string('1', 201) })
            Assert.AreEqual(unchecked((int)0x80070057), Assert.Throws<COMException>(() => app.ShowCustomer(key, number)).HResult);
        Assert.Throws<COMException>(() => app.ShowSupplier("foreign", "00123"));
        Assert.Throws<COMException>(() => app.GetOperation("bad"));
        Assert.Throws<COMException>(() => app.Dial("1", key, Guid.Empty.ToString("D")));
        foreach (var context in new[] { "[]", "{\"secret\":\"x\"}", "{\"callId\":\"bad\"}", "{\"callId\":\"x\",\"callId\":\"y\"}", new string(' ', 8193) })
            Assert.Throws<COMException>(() => app.ShowView("history", context));
        Assert.AreEqual(0, fixture.Environment.Reads); Assert.AreEqual(0, fixture.Environment.Shown); Assert.AreEqual(0, fixture.Environment.Dials);
    }

    [TestMethod]
    public async Task SourceAndComProviderFailuresNeverExposeRawMessages()
    {
        await using var fixture = new Fixture();
        fixture.Environment.SourceThrows = true;
        var result = await Done(fixture.App, fixture.App.ShowCustomer(fixture.Keys()[0], "00123"));
        Assert.AreEqual("ProviderUnavailable", result.ErrorCode); Assert.DoesNotContain("private-secret", result.Message);
        fixture.Environment.CatalogThrows = true;
        var error = Assert.Throws<COMException>(() => fixture.App.GetViewsJson());
        Assert.DoesNotContain("private-secret", error.Message);
    }

    [TestMethod]
    public async Task ForeignSourceRowsAreRejectedInsteadOfPublishingWrongParty()
    {
        await using var fixture = new Fixture(); fixture.Environment.ForeignResponse = true;
        var result = await Done(fixture.App, fixture.App.ShowCustomer(fixture.Keys()[0], "00123"));
        Assert.AreEqual("ProviderUnavailable", result.ErrorCode); Assert.AreEqual(0, fixture.Environment.Shown);
    }

    [TestMethod]
    public async Task AccessIsCheckedAgainAfterQueueingAndBeforePresentation()
    {
        await using var fixture = new Fixture(); fixture.Environment.Block = true;
        var first = fixture.App.Show(); await fixture.Environment.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var customer = fixture.App.ShowCustomer(fixture.Keys()[0], "00123");
        fixture.Environment.Allowed = false; fixture.Environment.Release.TrySetResult();
        await Done(fixture.App, first);
        Assert.AreEqual("AccessDenied", (await Done(fixture.App, customer)).ErrorCode);
        Assert.AreEqual(0, fixture.Environment.Reads);
    }

    [TestMethod]
    public async Task BoundedRetentionRejectsBusyWithoutEvictingDeduplicationEvidence()
    {
        var clock = new FixtureClock();
        await using var fixture = new Fixture(options: new() { MaximumOperations = 1, Retention = TimeSpan.FromSeconds(1) }, clock: clock);
        var key = fixture.Keys()[0]; var request = Guid.NewGuid().ToString("D");
        var id = fixture.App.Dial("1", key, request); var snapshot = await Done(fixture.App, id);
        Assert.AreEqual("Succeeded", snapshot.State);
        Assert.AreEqual(id, fixture.App.Dial("1", key, request));
        Assert.AreEqual(unchecked((int)0x8004A107), Assert.Throws<COMException>(() => fixture.App.Show()).HResult);
        clock.Advance(TimeSpan.FromSeconds(2));
        Assert.Throws<COMException>(() => fixture.App.GetOperation(id));
        var nextId = fixture.App.Dial("1", key, request);
        Assert.AreNotEqual(id, nextId);
        Assert.AreEqual("Succeeded", (await Done(fixture.App, nextId)).State);
        Assert.AreEqual(2, fixture.Environment.Dials);
        Assert.AreEqual("Succeeded", snapshot.State);
    }

    [TestMethod]
    public async Task CatalogLimitsAndStatusRedactionApplyBeforePublishingSnapshots()
    {
        await using var fixture = new Fixture(); var env = fixture.Environment;
        env.CompanyCatalog = Enumerable.Range(0, 1001).Select(i => new Company(new(LexwareProductLine.Pro, "fixture", i.ToString()), "Company")).ToImmutableArray();
        Assert.Throws<COMException>(() => fixture.App.GetCompaniesJson());
        env.CompanyCatalog = [new(env.A, "A"), new(env.A, "Duplicate")];
        Assert.Throws<COMException>(() => fixture.App.GetCompaniesJson());
        env.CompanyCatalog = [new(new(LexwareProductLine.FinOffice, "fixture", "0001"), "Foreign")];
        Assert.Throws<COMException>(() => fixture.App.GetCompaniesJson());
        env.ViewCatalog = Enumerable.Range(0, 257).Select(i => new AutomationView(i.ToString(), "View")).ToImmutableArray();
        Assert.Throws<COMException>(() => fixture.App.GetViewsJson());
        env.ViewCatalog = [new("home", "A"), new("home", "Duplicate")];
        Assert.Throws<COMException>(() => fixture.App.GetViewsJson());
        env.StatusOverride = new(LexwareProductLine.Pro, "private-secret", false, "private-secret", "private-secret");
        var status = fixture.App.GetStatus();
        Assert.AreEqual("Failed", status.State); Assert.AreEqual("Failed", status.SipState); Assert.AreEqual("ProviderUnavailable", status.ErrorCode);
        env.Allowed = false;
        Assert.AreEqual(unchecked((int)0x80070005), Assert.Throws<COMException>(() => fixture.App.GetCompaniesJson()).HResult);
    }

    [TestMethod]
    public async Task TimeoutAndShutdownHaveObservedFinalStates()
    {
        await using var fixture = new Fixture(options: new() { CommandTimeout = TimeSpan.FromMilliseconds(60) });
        fixture.Environment.Block = true;
        Assert.AreEqual("Timeout", (await Done(fixture.App, fixture.App.Show())).ErrorCode);
        var id = fixture.App.Show();
        await fixture.Services.GetRequiredService<AutomationOperations>().DisposeAsync();
        Assert.AreEqual("Canceled", fixture.App.GetOperation(id).State);
        Assert.Throws<COMException>(() => fixture.App.Show());
    }

    [TestMethod]
    public async Task PresentationAdapterUsesUiDispatcherAndSnapshotsAreDetached()
    {
        await using var fixture = new Fixture();
        var before = fixture.App.GetStatus(); var operation = fixture.App.Show();
        var old = fixture.App.GetOperation(operation); var oldState = old.State;
        await Done(fixture.App, operation);
        Assert.AreEqual(1, fixture.Dispatcher.Count); Assert.IsFalse(before.IsVisible); Assert.IsTrue(fixture.App.GetStatus().IsVisible);
        Assert.AreEqual(oldState, old.State);
        Assert.AreEqual("NotSupported", (await Done(fixture.App, fixture.App.ShowView("unknown", ""))).ErrorCode);
    }

    private static async Task<IOperationV1> Done(IApplicationV1 app, string id)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (true)
        {
            var result = app.GetOperation(id);
            if (result.State is "Succeeded" or "Failed" or "Canceled") return result;
            await Task.Delay(5, timeout.Token);
        }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        internal readonly FixtureEnvironment Environment;
        internal readonly FixtureDispatcher Dispatcher = new();
        internal ServiceProvider Services { get; }
        internal AutomationApplication App { get; }
        internal Fixture(LexwareProductLine product = LexwareProductLine.Pro, AutomationOptions? options = null, TimeProvider? clock = null)
        {
            Environment = new(product);
            var services = new ServiceCollection();
            services.AddSingleton<IAutomationEnvironment>(Environment); services.AddSingleton<IPartySearch>(Environment);
            services.AddSingleton<IAutomationViewHost>(Environment); services.AddSingleton<IAutomationTelephony>(Environment);
            services.AddSingleton<IUiDispatcher>(Dispatcher); services.AddSingleton<IAutomationPresentation, DispatchedAutomationPresentation>();
            if (clock is not null) services.AddSingleton(clock);
            services.AddWawiAutomation(options);
            Services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
            App = Services.GetRequiredService<IAutomationApplicationFactory>().Create();
        }
        internal string[] Keys()
        {
            using var catalog = JsonDocument.Parse(App.GetCompaniesJson());
            return catalog.RootElement.GetProperty("Items").EnumerateArray().Select(item => item.GetProperty("Key").GetString()!).ToArray();
        }
        public ValueTask DisposeAsync() => Services.DisposeAsync();
    }

    private sealed class FixtureDispatcher : IUiDispatcher
    {
        internal int Count;
        public ValueTask InvokeAsync(Action callback, CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); Interlocked.Increment(ref Count); callback(); return ValueTask.CompletedTask; }
    }
    private sealed class FixtureClock : TimeProvider
    {
        private long _ticks = DateTimeOffset.UtcNow.UtcTicks;
        public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref _ticks), TimeSpan.Zero);
        internal void Advance(TimeSpan duration) => Interlocked.Add(ref _ticks, duration.Ticks);
    }
    private sealed class FixtureEnvironment(LexwareProductLine product) : IAutomationEnvironment, IPartySearch, IAutomationViewHost, IAutomationTelephony
    {
        internal CompanyKey A { get; } = new(product, "fixture", "0001");
        internal CompanyKey B { get; } = new(product, "fixture", "0002");
        internal bool Allowed = true, SourceThrows, CatalogThrows, ForeignResponse, Block;
        internal int Shown, Dials, Reads;
        internal PresentationRequest? Last;
        internal ImmutableArray<Company>? CompanyCatalog;
        internal ImmutableArray<AutomationView>? ViewCatalog;
        internal AutomationStatus? StatusOverride;
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private AutomationCall? _call;
        public AutomationStatus GetStatus() => StatusOverride ?? new(product, "Ready", Shown > 0, "Registered");
        public ImmutableArray<Company> GetCompanies() => CompanyCatalog ?? [new(A, "Müller 東京"), new(B, "Zweiter Mandant")];
        public ImmutableArray<AutomationView> GetViews() => CatalogThrows ? throw new COMException("private-secret") : ViewCatalog ?? [new("home", "Start"), new("history", "Historie")];
        public void RequireAccess(AutomationAccess access, CompanyKey? company = null) { if (!Allowed || company is not null && company != A && company != B) throw new AutomationFault(AutomationFailure.AccessDenied); }
        public Task<PartySearchResult> SearchAsync(PartySearchQuery query, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref Reads);
            if (SourceThrows) throw new InvalidOperationException("private-secret");
            var company = ForeignResponse ? (query.Company == A ? B : A) : query.Company;
            Party[] parties = [new() { Key = new(company, query.Kind!.Value, query.Company == A ? "c-a" : "c-b"), Number = "00123", DisplayName = "Test" },
                new() { Key = new(company, query.Kind.Value, "d1"), Number = "00777", DisplayName = "Doppelt" },
                new() { Key = new(company, query.Kind.Value, "d2"), Number = "00777", DisplayName = "Doppelt" },
                new() { Key = new(company, query.Kind.Value, "unicode"), Number = "Ä-東京", DisplayName = "Unicode" }];
            return Task.FromResult(new PartySearchResult(query, parties.Where(item => item.Number!.Contains(query.Text, StringComparison.Ordinal)).ToImmutableArray()));
        }
        public async Task ShowAsync(PresentationRequest request, CancellationToken token)
        {
            if (Block) { Entered.TrySetResult(); await Release.Task.WaitAsync(token); }
            Last = request; Interlocked.Increment(ref Shown);
        }
        public Task ConnectAsync(CancellationToken token) => Task.CompletedTask;
        public Task<Guid> DialAsync(CompanyKey company, string destination, CancellationToken token) { Interlocked.Increment(ref Dials); var id = Guid.NewGuid(); _call = new(id, "Dialing"); return Task.FromResult(id); }
        public Task HangUpAsync(Guid id, CancellationToken token) { _call = new(id, "Ended"); return Task.CompletedTask; }
        public AutomationCall GetCall(Guid id) => _call?.Id == id ? _call : throw new AutomationFault(AutomationFailure.NotFound);
    }
}

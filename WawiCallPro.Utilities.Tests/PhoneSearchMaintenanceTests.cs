using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WawiCallPro.Core.Threading;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Database.Repositories;
using WawiCallPro.Interfaces.Capabilities;
using WawiCallPro.Interfaces.Common;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Interfaces.PhoneNumbers;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Hosting;
using WawiCallPro.Utilities.Persistence;

namespace WawiCallPro.Utilities.Tests;

[TestClass]
public sealed class PhoneSearchMaintenanceTests
{
    [TestMethod]
    public Task ChangesRenormalizeRawSourcesAndRetainCallSelectionAndHistoricalEvidence() => RunAsync(RunAcceptanceAsync);

    /// <summary>The same lifecycle runs with real Dapper repositories on SQLite and every disposable server engine.</summary>
    public static async Task RunAcceptanceAsync(IServiceProvider services)
    {
        await using var test = new Fixture(services);
        await test.SelectAsync(test.A);
        await test.SeedAsync(test.A, oldMetadata: true);
        var before = await test.Cache.GetStateAsync(test.A.Key);
        var rebuilt = await test.Index.RefreshAsync();
        Assert.IsTrue(rebuilt.IsComplete);
        Assert.AreEqual(0, test.Source.PartyReads, "Metadata repair must use saved originals rather than fetching ERP.");
        var reindexed = await test.Cache.GetStateAsync(test.A.Key);
        Assert.AreEqual(before.Revision + 1, reindexed.Revision); Assert.AreEqual(before.RefreshedAtUtc, reindexed.RefreshedAtUtc);
        var party = (await test.Cache.GetAsync(test.Party(test.A).Key))!;
        Assert.AreEqual("1234567", party.Phones[0].OriginalValue); Assert.AreEqual("+49301234567", party.Phones[0].E164);
        Assert.AreEqual("fixture", party.Phones[0].Source.RecordType);
        Assert.AreEqual("+49301234567", party.Addresses[0].Phones[0].E164); Assert.AreEqual("42", party.Addresses[0].Phones[0].Extension);
        var oldResult = test.Find("+49301234567");
        var decision = services.GetRequiredService<ICallerMatchDecisionService>().Evaluate(oldResult, new());
        Assert.IsNotNull(decision.Selection);
        var now = DateTimeOffset.UtcNow;
        var call = await services.GetRequiredService<ICallJournal>().BeginAsync(new() { Id = Guid.NewGuid(), Revision = 0,
            Direction = CallDirection.Incoming, State = CallState.Created, StartedAtUtc = now, StateChangedAtUtc = now,
            LocalAddress = new(NumberPresentation.Unknown), RemoteAddress = new(NumberPresentation.Available, "+49301234567") });
        var changeVersion = test.Changes.Revision;
        var history = await services.GetRequiredService<ICallAssociationService>().SaveAsync(call.Id, decision);
        Assert.AreEqual(changeVersion, test.Changes.Revision, "Call writes must not invalidate the phone index.");
        var historical = JsonSerializer.Serialize(history);
        await using var scope = services.GetRequiredService<ICallViewScopeFactory>().Create(call);
        await scope.SelectAsync(decision);
        var originalContext = scope.Current;

        var profile = await test.Matching.SaveProfileAsync(new(test.A.Key, true, 4), null);
        Assert.AreEqual(PhoneSearchScopeState.ScopeChanged, test.Index.GetStatus().State); Assert.IsEmpty(test.Find("+49301234567").Candidates);
        await test.Index.RefreshAsync(); Assert.HasCount(1, test.Find("+49301234999").Candidates);
        await test.Matching.SaveProfileAsync(new(test.A.Key, true, 5), profile.Version);
        await test.Index.RefreshAsync(); Assert.IsEmpty(test.Find("+49301234999").Candidates);
        test.Interpreter.Revision++;
        Assert.IsTrue(await test.Index.NeedsRefreshAsync(CancellationToken.None));
        var revision = (await test.Cache.GetStateAsync(test.A.Key)).Revision;
        await test.Index.RefreshAsync(); Assert.AreEqual(revision + 1, (await test.Cache.GetStateAsync(test.A.Key)).Revision);
        Assert.AreEqual(0, test.Source.PartyReads);

        await test.SeedAsync(test.B);
        await test.SelectAsync(test.B);
        Assert.AreEqual(PhoneSearchScopeState.ScopeChanged, test.Index.GetStatus().State);
        await test.Index.RefreshAsync(); Assert.AreEqual(test.B.Key, test.Find("+49301234567").Candidates.Single().Party.Company);
        var state = await test.Cache.GetStateAsync(test.B.Key);
        var changed = test.Party(test.B, "+49309876543") with { DisplayName = "Geänderter Stammdatensatz" };
        await test.Cache.ReplaceAsync(new(test.B.Key, state.Revision, [changed], test.Clock.Now, test.Metadata.ParserVersion, state.ContextVersion));
        Assert.AreEqual(PhoneSearchScopeState.ScopeChanged, test.Index.GetStatus().State);
        await test.Index.RefreshAsync(); Assert.IsEmpty(test.Find("+49301234567").Candidates);
        Assert.AreEqual("Geänderter Stammdatensatz", test.Find("+49309876543").Candidates.Single().DisplayName);
        await services.GetRequiredService<IPhoneMatchingRepository>().WriteRuleAsync(new(new PhoneNumberBlockRule
        {
            Id = Guid.NewGuid(), Organisation = changed.Key, Kind = PhoneNumberBlockKind.Confirmed,
            CountryCallingCode = 49, DestinationCode = "30", Stem = "98765", MinimumSuffixDigits = 1, MaximumSuffixDigits = 4,
            Evidence = "Synthetic confirmed block"
        }, null, DateTimeOffset.UtcNow));
        Assert.AreEqual(PhoneSearchScopeState.ScopeChanged, test.Index.GetStatus().State);
        await test.Index.RefreshAsync();
        state = await test.Cache.GetStateAsync(test.B.Key);
        await services.GetRequiredService<DapperPartyCacheRepository>().ReplaceAsync(new(test.B.Key, state.Revision,
            [test.Party(test.B)], test.Clock.Now, test.Metadata.ParserVersion, state.ContextVersion));
        Assert.IsTrue(await test.Index.NeedsRefreshAsync(CancellationToken.None), "External-process writes have no local signal and must be polled.");
        await test.Index.RefreshAsync(); Assert.HasCount(1, test.Find("+49301234567").Candidates);

        Assert.AreEqual(historical, JsonSerializer.Serialize((await services.GetRequiredService<ICallHistoryService>().GetCallAsync(call.Id))!));
        Assert.AreEqual(originalContext, scope.Current);
        Assert.AreEqual("Firma A", oldResult.Candidates.Single().DisplayName);
        Assert.AreEqual(test.A.Key, oldResult.Lookup.SearchCompanies.Single().Company);
    }

    [TestMethod]
    public Task ScheduledRefreshDiscoversSourceEditsWithoutRewritingAnExistingDecision() => RunAsync(async services =>
    {
        await using var test = new Fixture(services);
        await test.SelectAsync(test.A);
        var initial = await test.Worker.ReconcileAsync(); Assert.IsTrue(initial.IsComplete); Assert.AreEqual(1, test.Source.PartyReads);
        var old = test.Find("+49301234567");
        var generation = test.Index.GetStatus().Generation;
        await test.Worker.ReconcileAsync(); Assert.AreEqual(1, test.Source.PartyReads); Assert.AreEqual(generation, test.Index.GetStatus().Generation);
        test.Source.Number = "+49309876543";
        test.Clock.Now += TimeSpan.FromMinutes(16);
        await test.Worker.ReconcileAsync();
        Assert.AreEqual(2, test.Source.PartyReads); Assert.IsEmpty(test.Find("+49301234567").Candidates);
        Assert.HasCount(1, test.Find("+49309876543").Candidates); Assert.HasCount(1, old.Candidates);
    });
    [TestMethod]
    public Task ANewPluginSessionRefreshesSourcesEvenWhenCompanyKeysStayTheSame() => RunAsync(async services =>
    {
        await using var test = new Fixture(services); await test.SelectAsync(test.A);
        await test.Worker.ReconcileAsync(); Assert.AreEqual(1, test.Source.PartyReads);
        test.Source.Number = "+49309876543"; test.Changes.PluginChanged();
        await test.Worker.ReconcileAsync(); Assert.AreEqual(2, test.Source.PartyReads);
        Assert.IsEmpty(test.Find("+49301234567").Candidates); Assert.HasCount(1, test.Find("+49309876543").Candidates);
    });
    [TestMethod]
    public Task UnrelatedSettingsAndFailedWritesDoNotInvalidateButSelectionRemovalDoes() => RunAsync(async services =>
    {
        await using var test = new Fixture(services); await test.SelectAsync(test.A); await test.SeedAsync(test.A); await test.Index.RefreshAsync();
        var revision = test.Changes.Revision;
        var saved = await test.Settings.LoadAsync();
        await test.Settings.SaveAsync(saved.Options with { User = saved.Options.User with { UseDirectXRendering = !saved.Options.User.UseDirectXRendering } }, saved);
        Assert.AreEqual(revision, test.Changes.Revision); Assert.IsFalse(await test.Index.NeedsRefreshAsync(CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ConfigurationException>(() => test.Settings.SaveAsync(saved.Options, saved));
        Assert.AreEqual(revision, test.Changes.Revision);
        await test.SelectAsync(); Assert.AreEqual(PhoneSearchScopeState.ScopeChanged, test.Index.GetStatus().State);
        await test.Worker.ReconcileAsync(); Assert.AreEqual(PhoneSearchScopeState.NoSelection, test.Index.GetStatus().State);
        Assert.IsEmpty(test.Find("+49301234567").Candidates); Assert.AreEqual(0, test.Source.PartyReads);
    });
    [TestMethod]
    public Task PluginEpochInvalidatesPublishedAndInFlightIndexesEvenWhenTheConfiguredIdIsUnchanged() => RunAsync(async services =>
    {
        await using var test = new Fixture(services); await test.SelectAsync(test.A); await test.SeedAsync(test.A); await test.Index.RefreshAsync();
        test.Changes.PluginChanged(); Assert.AreEqual(PhoneSearchScopeState.ScopeChanged, test.Index.GetStatus().State);
        var calls = 0;
        test.Source.BeforeCatalog = () => { if (++calls == 2) test.Changes.PluginChanged(); return Task.CompletedTask; };
        Assert.AreEqual(PhoneSearchScopeState.ScopeChanged, (await test.Index.RefreshAsync()).State);
        Assert.IsEmpty(test.Find("+49301234567").Candidates);
    });
    [TestMethod]
    public Task BackgroundSignalsCoalesceAndShutdownDrainsAnIgnoringSource() => RunAsync(async services =>
    {
        await using var test = new Fixture(services); await test.SelectAsync(test.A);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        test.Source.BeforeRead = async () => { entered.TrySetResult(); await release.Task; };
        await test.Worker.StartAsync(CancellationToken.None);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        for (var index = 0; index < 100; index++) test.Changes.Invalidate();
        var stop = test.Worker.StopAsync(CancellationToken.None); Assert.IsFalse(stop.IsCompleted);
        release.SetResult(); await stop;
        Assert.AreEqual(1, test.Source.PartyReads); Assert.IsEmpty(test.Find("+49301234567").Candidates);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => test.Worker.StartAsync(CancellationToken.None));
    });
    [TestMethod]
    public Task AQuietEmptyScopeDoesNotOpenStorageOrCallTheErp() => RunAsync(async services =>
    {
        await using var test = new Fixture(services); await test.Worker.ReconcileAsync(); await test.Worker.ReconcileAsync();
        Assert.AreEqual(PhoneSearchScopeState.NoSelection, test.Index.GetStatus().State);
        Assert.AreEqual(0, test.Source.PartyReads); Assert.AreEqual(0, test.Source.CatalogReads);
    }, false);

    [TestMethod]
    public Task BackgroundRefreshDropsAnOldSourceResponseAndRebuildsTheNewSelectedCompany() => RunAsync(async services =>
    {
        await using var test = new Fixture(services); await test.SelectAsync(test.A);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        test.Source.BeforeRead = async () => { entered.TrySetResult(); await release.Task; };
        await test.Worker.StartAsync(CancellationToken.None); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await test.SelectAsync(test.B); Assert.IsEmpty(test.Find("+49301234567").Candidates);
        release.SetResult();
        var until = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < until && !(test.Index.GetStatus().IsComplete && test.Index.GetStatus().Companies.Single().Company == test.B.Key)) await Task.Delay(10);
        Assert.AreEqual(test.B.Key, test.Find("+49301234567").Candidates.Single().Party.Company);
        Assert.IsNull(await services.GetRequiredService<ICompanyRepository>().GetAsync(test.A.Key));
        await test.Worker.StopAsync(CancellationToken.None);
    });
    [TestMethod]
    public Task FailedSourceReadsWaitForTheNextIntervalAndFailedCacheWritesDoNotSignal() => RunAsync(async services =>
    {
        await using var test = new Fixture(services); await test.SelectAsync(test.A); await test.SeedAsync(test.A);
        await test.Index.RefreshAsync(); var change = test.Changes.Revision;
        var state = await test.Cache.GetStateAsync(test.A.Key);
        await Assert.ThrowsExactlyAsync<RepositoryConflictException>(() => test.Cache.ReplaceAsync(new(test.A.Key, state.Revision + 1,
            [test.Party(test.A)], test.Clock.Now, test.Metadata.ParserVersion, state.ContextVersion)));
        Assert.AreEqual(change, test.Changes.Revision);
        test.Source.BeforeRead = () => throw new IOException("private-source-sentinel");
        await test.Worker.ReconcileAsync(); var reads = test.Source.PartyReads;
        await test.Worker.ReconcileAsync(); Assert.AreEqual(reads, test.Source.PartyReads);
        Assert.HasCount(1, test.Find("+49301234567").Candidates);
    });

    private static async Task RunAsync(Func<IServiceProvider, Task> action, bool createDatabase = true)
    {
        var root = Path.Combine(Path.GetTempPath(), "WawiCallPro-maintenance-" + Guid.NewGuid().ToString("N"));
        var host = WawiCallHost.CreateBuilder(Assembly.GetExecutingAssembly()).ConfigureServices(services =>
        { services.AddSingleton(new ConfigurationLocation(root)); services.AddSingleton<IUiDispatcher, InlineUiDispatcher>(); }).Build();
        try
        {
            await HostExecution.RunAsync(host, async token =>
            { if (createDatabase) await host.Services.GetRequiredService<ICommunicationDatabase>().CreateAndMigrateAsync(cancellationToken: token); await action(host.Services); });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    private sealed class Fixture : IAsyncDisposable
    {
        internal Clock Clock { get; } = new();
        internal Source Source { get; } = new();
        internal Company A { get; }
        internal Company B { get; }
        internal ConfigurationStore Settings { get; }
        internal PhoneSearchChanges Changes { get; }
        internal IPartyCacheRepository Cache { get; }
        internal ICompanyPhoneMatchingService Matching { get; }
        internal VersionedInterpreter Interpreter { get; }
        internal IPhoneCacheMetadata Metadata { get; }
        internal PhoneSearchIndexService Index { get; }
        internal PhoneSearchMaintenance Worker { get; }
        private readonly ICompanyRepository _companies;
        internal Fixture(IServiceProvider services)
        {
            var application = services.GetRequiredService<ApplicationDescriptor>(); var line = Enum.Parse<LexwareProductLine>(application.ProductLine);
            A = new(new(line, "maintenance-A", Guid.NewGuid().ToString("N")), "Firma A"); B = new(new(line, "maintenance-B", Guid.NewGuid().ToString("N")), "Firma B");
            Source.Companies = [A, B]; Source.CreateParty = company => Party(company, Source.Number);
            Settings = services.GetRequiredService<ConfigurationStore>(); Changes = services.GetRequiredService<PhoneSearchChanges>();
            Cache = services.GetRequiredService<IPartyCacheRepository>(); Matching = services.GetRequiredService<ICompanyPhoneMatchingService>(); _companies = services.GetRequiredService<ICompanyRepository>();
            var parser = services.GetRequiredService<IPhoneNumberParser>(); Interpreter = new(services.GetRequiredService<IPhoneNumberInterpreter>()); Metadata = new PhoneCacheMetadata(parser, Interpreter);
            var errors = services.GetRequiredService<INumberParseLogRepository>(); var active = services.GetRequiredService<ApplicationConfigurationService>();
            var reindexer = new PhoneCacheReindexer(Cache, errors, Interpreter, Metadata, Settings, active, Clock);
            Index = new(Settings, active, application, Source, Cache, Matching, Metadata, Clock, new(), Changes, reindexer);
            var cache = new PhoneCacheService(Settings, active, application, services.GetRequiredService<ICommunicationDatabase>(), Source, _companies, Cache, errors, parser, Interpreter, Clock, Metadata);
            Worker = new(Index, cache, Changes, Clock, new() { CheckInterval = TimeSpan.FromDays(1) });
        }
        internal async Task SelectAsync(params Company[] companies)
        {
            var saved = await Settings.LoadAsync();
            await Settings.SaveAsync(saved.Options with { Companies = companies.Select(company => new CompanySettings
                { Company = company.Key, DisplayName = company.DisplayName, IncludeInPhoneCache = true, DefaultPhoneRegion = "DE", LocalAreaCode = "030" }).ToImmutableArray() }, saved);
        }
        internal Party Party(Company company, string number = "1234567") => new() { Key = new(company.Key, PartyKind.Customer, "01"), DisplayName = company.DisplayName,
            Phones = [new(number, PhoneKind.Telephone, new("fixture", "01", "phone"), number.StartsWith('+') ? number : "+49301234567")],
            Addresses = [new() { ExternalId = "address-01", Kind = AddressKind.Billing, Phones = [new(number, PhoneKind.Mobile, new("fixture_address", "address-01", "mobile"), number.StartsWith('+') ? number : "+49301234567", "42")] }] };
        internal async Task SeedAsync(Company company, bool oldMetadata = false)
        {
            if (await _companies.GetAsync(company.Key) is null) await _companies.WriteAsync(new(company, null, Clock.Now));
            var state = await Cache.GetStateAsync(company.Key);
            await Cache.ReplaceAsync(new(company.Key, state.Revision, [Party(company)], Clock.Now.AddHours(-2), oldMetadata ? "retired-parser" : Metadata.ParserVersion,
                oldMetadata ? new string('a', 64) : Metadata.GetContextVersion(new() { Company = company.Key, DefaultPhoneRegion = "DE", LocalAreaCode = "030" })));
        }
        internal PhoneSearchResult Find(string number) => Index.Find(Interpreter.InterpretIncoming(new(NumberPresentation.Available, number)));
        public async ValueTask DisposeAsync() { await Worker.DisposeAsync(); await Index.DisposeAsync(); }
    }
    private sealed class Clock : TimeProvider { internal DateTimeOffset Now = DateTimeOffset.UtcNow; public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class VersionedInterpreter(IPhoneNumberInterpreter inner) : IPhoneNumberInterpreter
    {
        internal int Revision;
        public PhoneNumberInterpretation InterpretCompanyPhone(PartyPhone phone, CompanyPhoneNumberContext context, PhoneNumberInputKind inputKind = PhoneNumberInputKind.Automatic)
        { var value = inner.InterpretCompanyPhone(phone, context, inputKind); return value with { Version = value.Version + ";fixture/" + Revision }; }
        public PhoneNumberInterpretation InterpretIncoming(CallAddress address, PhoneLineContext? context = null, PhoneNumberInputKind inputKind = PhoneNumberInputKind.Automatic)
        { var value = inner.InterpretIncoming(address, context, inputKind); return value with { Version = value.Version + ";fixture/" + Revision }; }
    }
    private sealed class Source : IBusinessDirectory
    {
        internal ImmutableArray<Company> Companies = [];
        internal Func<Company, Party> CreateParty = null!;
        internal string Number = "1234567";
        internal int PartyReads;
        internal int CatalogReads;
        internal Func<Task>? BeforeRead;
        internal Func<Task>? BeforeCatalog;
        public async Task<ImmutableArray<Company>> GetCompaniesAsync(CancellationToken cancellationToken = default)
        { Interlocked.Increment(ref CatalogReads); if (BeforeCatalog is not null) await BeforeCatalog(); return Companies; }
        public async Task<PartyPage> GetPartiesAsync(PartyQuery query, CancellationToken cancellationToken = default)
        { Interlocked.Increment(ref PartyReads); if (BeforeRead is not null) await BeforeRead(); return new(query.Company, new([CreateParty(Companies.Single(company => company.Key == query.Company))], false)); }
        public Task<Party?> GetPartyAsync(PartyKey key, CancellationToken cancellationToken = default) => throw new AssertFailedException("Index maintenance must not load detail views.");
        public Task<CapabilitySet> GetCapabilitiesAsync(CompanyKey company, CancellationToken cancellationToken = default) => throw new AssertFailedException("Not needed for cache refresh.");
    }
}

using System.Collections.Immutable;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WawiCallPro.Core.Companies;
using WawiCallPro.Core.Threading;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Capabilities;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Interfaces.PhoneNumbers;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Hosting;
using WawiCallPro.Utilities.Persistence;

namespace WawiCallPro.Utilities.Tests;

[TestClass]
public sealed class PhoneSearchIndexTests
{
    [TestMethod]
    public Task RealDiCachesPublishSeparateCompaniesAndReuseOnlyUnchangedIndexes() => RunAsync(RunAcceptanceAsync);

    /// <summary>Also run against each isolated native database by the provider acceptance script.</summary>
    public static async Task RunAcceptanceAsync(IServiceProvider services)
    {
        await using var test = new Fixture(services);
        await test.SelectAsync(test.A, test.B);
        await test.SeedAsync(test.A);
        await test.SeedAsync(test.B);
        await services.GetRequiredService<CompanyContext>().SelectAsync(test.B);
        var settings = services.GetRequiredService<ConfigurationStore>();
        var saved = await settings.LoadAsync();
        await settings.SaveAsync(saved.Options with { User = saved.Options.User with { PreferredCompany = test.B.Key } }, saved);
        // A different installation with the same external company/party IDs and phone is a distinct hit.
        Assert.AreEqual(test.A.Key.ExternalId, test.B.Key.ExternalId);
        var status = await test.Search.RefreshAsync();
        Assert.IsTrue(status.IsComplete);
        var first = test.Find();
        Assert.HasCount(2, first.Candidates);
        CollectionAssert.AreEquivalent(new[] { test.A.Key, test.B.Key }, first.Candidates.Select(hit => hit.Party.Company).ToArray());
        Assert.IsTrue(first.Lookup.IsComplete);
        Assert.IsNull(first.Lookup.SelectionReason);
        var reads = test.Matching.ProfileReads;
        for (var i = 0; i < 20; i++) { Assert.HasCount(2, test.Find().Candidates); test.Search.GetStatus(); }
        Assert.AreEqual(reads, test.Matching.ProfileReads, "Lookups must not read rules or wait for I/O.");
        Assert.AreEqual(2, test.Matching.Builds.Count);
        await test.Search.RefreshAsync();
        Assert.AreEqual(2, test.Matching.Builds.Count, "Unchanged immutable indexes are reused.");
        var profile = await test.Matching.GetProfileAsync(test.A.Key);
        await test.Matching.SaveProfileAsync(new(test.A.Key, true, 4), profile.Version);
        await test.Search.RefreshAsync();
        Assert.AreEqual(3, test.Matching.Builds.Count);
        Assert.AreEqual(1L, test.Find().Lookup.SearchCompanies.Single(company => company.Company == test.A.Key).RuleRevision);
        await test.SeedAsync(test.B);
        await test.Search.RefreshAsync();
        Assert.AreEqual(4, test.Matching.Builds.Count);
        Assert.AreEqual(2L, test.Find().Lookup.SearchCompanies.Single(company => company.Company == test.B.Key).CacheRevision);
        Assert.AreEqual(1L, first.Lookup.SearchCompanies.Single(company => company.Company == test.B.Key).CacheRevision);
        test.Source.Companies = [test.B];
        status = await test.Search.RefreshAsync();
        Assert.AreEqual(PhoneSearchScopeState.Partial, status.State);
        Assert.AreEqual(PhoneSearchCompanyState.NotAuthorized, status.Companies[0].State);
        Assert.AreEqual(test.B.Key, test.Find().Candidates.Single().Party.Company);
        Assert.HasCount(2, first.Candidates, "An earlier call's detached evidence is immutable.");
    }

    [TestMethod]
    public Task ConstructionAndEmptySelectionNeverReadCatalogOrOpenDatabase() => RunAsync(async services =>
    {
        await using var test = new Fixture(services);
        Assert.AreEqual(PhoneSearchScopeState.NotReady, test.Find().Index.State);
        Assert.IsFalse(test.Find().Lookup.IsComplete);
        Assert.AreEqual(PhoneSearchScopeState.NoSelection, (await test.Search.RefreshAsync()).State);
        Assert.AreEqual(0, test.Source.Reads);
        Assert.AreEqual(0, test.Matching.ProfileReads);
        var root = Path.GetDirectoryName(services.GetRequiredService<ConfigurationLocation>().FilePath)!;
        Assert.IsFalse(File.Exists(Path.Combine(root, "communications.db")));
    }, createDatabase: false);

    [TestMethod]
    public Task UnavailableAndWrongEditionCompaniesNeverReachMatcher() => RunAsync(async services =>
    {
        await using var test = new Fixture(services);
        var foreign = new Company(new(test.A.Key.ProductLine == LexwareProductLine.Pro ? LexwareProductLine.FinOffice : LexwareProductLine.Pro,
            test.A.Key.InstallationId, test.A.Key.ExternalId), test.A.DisplayName);
        var unavailable = new Company(new(test.A.Key.ProductLine, "unknown-installation", test.A.Key.ExternalId), test.A.DisplayName);
        test.Source.Companies = [test.A, foreign];
        await test.SelectAsync(unavailable, foreign, test.B);
        Assert.IsFalse((await test.Search.RefreshAsync()).IsComplete);
        Assert.IsTrue(test.Search.GetStatus().Companies.All(company => company.State == PhoneSearchCompanyState.NotAuthorized));
        Assert.AreEqual(0, test.Matching.ProfileReads);
        Assert.IsEmpty(test.Find().Candidates);
    });

    [TestMethod]
    [DataRow(PhoneSearchCompanyState.CacheStale)]
    [DataRow(PhoneSearchCompanyState.NormalizationOutdated)]
    [DataRow(PhoneSearchCompanyState.ContextChanged)]
    [DataRow(PhoneSearchCompanyState.Incomplete)]
    public Task OldOrIncompleteCacheProvidesOnlyIncompleteEvidence(PhoneSearchCompanyState expected) => RunAsync(async services =>
    {
        await using var test = new Fixture(services);
        await test.SelectAsync(test.A);
        await test.SeedAsync(test.A, expected);
        test.Matching.ForceIncomplete = expected == PhoneSearchCompanyState.Incomplete;
        var status = await test.Search.RefreshAsync();
        Assert.AreEqual(PhoneSearchScopeState.Partial, status.State);
        Assert.AreEqual(expected, status.Companies.Single().State);
        var result = test.Find();
        Assert.HasCount(1, result.Candidates);
        Assert.IsFalse(result.Lookup.IsComplete);
        Assert.IsFalse(result.Lookup.SearchCompanies.Single().IsComplete);
    });

    [TestMethod]
    public Task MissingCacheAndFutureOrExpiredObservationsAreDistinct() => RunAsync(async services =>
    {
        await using var test = new Fixture(services);
        await test.SelectAsync(test.A, test.B);
        await test.SeedAsync(test.A);
        var status = await test.Search.RefreshAsync();
        Assert.AreEqual(PhoneSearchCompanyState.CacheMissing, status.Companies[1].State);
        test.Clock.Now += TimeSpan.FromMinutes(6);
        Assert.AreEqual(PhoneSearchScopeState.ScopeExpired, test.Find().Index.State);
        Assert.IsEmpty(test.Find().Candidates);
        await test.Search.RefreshAsync();
        Assert.HasCount(1, test.Find().Candidates);
        test.Clock.Now -= TimeSpan.FromMinutes(10);
        Assert.AreEqual(PhoneSearchScopeState.ScopeExpired, test.Search.GetStatus().State);
        status = await test.Search.RefreshAsync();
        Assert.AreEqual(PhoneSearchCompanyState.CacheStale, status.Companies[0].State, "Future cache timestamps are not fresh.");
    });

    [TestMethod]
    public Task CatalogFailureOrMalformedCatalogCannotReusePreviouslyAuthorizedHits() => RunAsync(async services =>
    {
        await using var test = new Fixture(services);
        await test.SelectAsync(test.A);
        await test.SeedAsync(test.A);
        await test.Search.RefreshAsync();
        Assert.HasCount(1, test.Find().Candidates);
        test.Source.Read = _ => throw new IOException("private-catalog-sentinel");
        Assert.AreEqual(PhoneSearchScopeState.CatalogUnavailable, (await test.Search.RefreshAsync()).State);
        Assert.IsEmpty(test.Find().Candidates);
        test.Source.Read = null;
        test.Source.Companies = [test.A, test.A];
        Assert.AreEqual(PhoneSearchScopeState.CatalogUnavailable, (await test.Search.RefreshAsync()).State);
        Assert.IsEmpty(test.Find().Candidates);
        test.Source.Companies = default;
        Assert.AreEqual(PhoneSearchScopeState.CatalogUnavailable, (await test.Search.RefreshAsync()).State);
    });

    [TestMethod]
    public Task CacheAgeIsReevaluatedInMemoryBetweenRefreshes() => RunAsync(async services =>
    {
        await using var test = new Fixture(services);
        await test.SelectAsync(test.A);
        test.Clock.Now -= TimeSpan.FromHours(23) + TimeSpan.FromMinutes(59);
        await test.SeedAsync(test.A);
        test.Clock.Now += TimeSpan.FromHours(23) + TimeSpan.FromMinutes(59);
        Assert.IsTrue((await test.Search.RefreshAsync()).IsComplete);
        var reads = test.Matching.ProfileReads;
        test.Clock.Now += TimeSpan.FromMinutes(2);
        var result = test.Find();
        Assert.AreEqual(PhoneSearchScopeState.Partial, result.Index.State);
        Assert.AreEqual(PhoneSearchCompanyState.CacheStale, result.Index.Companies.Single().State);
        Assert.HasCount(1, result.Candidates);
        Assert.AreEqual(reads, test.Matching.ProfileReads);
    });

    [TestMethod]
    [DataRow("selection")]
    [DataRow("permission")]
    [DataRow("cache")]
    [DataRow("rule")]
    [DataRow("context")]
    public Task ConcurrentChangesDiscardTheEntireMixedSnapshot(string change) => RunAsync(async services =>
    {
        await using var test = new Fixture(services);
        await test.SelectAsync(test.A, test.B);
        await test.SeedAsync(test.A);
        await test.SeedAsync(test.B);
        test.Matching.BeforeBuild = async company =>
        {
            if (company != test.B.Key) return;
            switch (change)
            {
                case "selection": await test.SelectAsync(test.B); break;
                case "permission": test.Source.Companies = [test.B]; break;
                case "cache": await test.SeedAsync(test.A); break;
                case "rule": await test.Matching.SaveProfileAsync(new(test.A.Key, true, 4), null); break;
                case "context":
                    var store = services.GetRequiredService<ConfigurationStore>();
                    var saved = await store.LoadAsync();
                    await store.SaveAsync(saved.Options with { Companies = saved.Options.Companies.Select(company => company with { LocalAreaCode = "030" }).ToImmutableArray() }, saved);
                    break;
            }
        };
        Assert.AreEqual(PhoneSearchScopeState.ScopeChanged, (await test.Search.RefreshAsync()).State);
        Assert.IsEmpty(test.Find().Candidates);
    });

    [TestMethod]
    public Task SlowRefreshNeverBlocksLookupAndQueuedCancellationDoesNotCancelTheOwner() => RunAsync(async services =>
    {
        await using var test = new Fixture(services);
        await test.SelectAsync(test.A);
        await test.SeedAsync(test.A);
        await test.Search.RefreshAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        test.Source.Read = async token => { entered.TrySetResult(); await release.Task.WaitAsync(token); return test.Source.Companies; };
        var owner = test.Search.RefreshAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var during = await Task.Run(test.Find).WaitAsync(TimeSpan.FromSeconds(2));
        Assert.AreEqual(PhoneSearchScopeState.Refreshing, during.Index.State);
        Assert.IsEmpty(during.Candidates);
        using var cancel = new CancellationTokenSource();
        var queued = test.Search.RefreshAsync(cancel.Token);
        cancel.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await queued);
        Assert.AreEqual(PhoneSearchScopeState.Refreshing, test.Search.GetStatus().State);
        release.SetResult();
        Assert.IsTrue((await owner).IsComplete);
        Assert.HasCount(1, test.Find().Candidates);
    });

    [TestMethod]
    public Task CancellationAndDisposalDrainBackgroundWorkAndPublishNoLateResults() => RunAsync(async services =>
    {
        await using var test = new Fixture(services);
        await test.SelectAsync(test.A);
        await test.SeedAsync(test.A);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        test.Source.Read = async token => { entered.TrySetResult(); await Task.Delay(Timeout.Infinite, token); return []; };
        using var cancel = new CancellationTokenSource();
        var refresh = test.Search.RefreshAsync(cancel.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancel.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await refresh);
        Assert.AreEqual(PhoneSearchScopeState.Cancelled, test.Find().Index.State);
        entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var active = test.Search.RefreshAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var queued = test.Search.RefreshAsync();
        await test.Search.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await active);
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await queued);
        await test.Search.DisposeAsync();
        Assert.AreEqual(PhoneSearchScopeState.Stopped, test.Find().Index.State);
        Assert.IsEmpty(test.Find().Candidates);
        Assert.ThrowsExactly<ObjectDisposedException>(() => test.Search.RefreshAsync());
    });

    [TestMethod]
    public Task DatabaseSwitchRequiresRestartBeforeCatalogOrCacheAccess() => RunAsync(async services =>
    {
        await using var test = new Fixture(services);
        await test.SelectAsync(test.A);
        var store = services.GetRequiredService<ConfigurationStore>();
        var saved = await store.LoadAsync();
        await store.SaveAsync(saved.Options with { Database = saved.Options.Database with { SqliteFileName = "other.db" } }, saved);
        Assert.AreEqual(PhoneSearchScopeState.RestartRequired, (await test.Search.RefreshAsync()).State);
        Assert.AreEqual(0, test.Source.Reads);
        Assert.AreEqual(0, test.Matching.ProfileReads);
    });

    private static async Task RunAsync(Func<IServiceProvider, Task> action, bool createDatabase = true)
    {
        var root = Path.Combine(Path.GetTempPath(), "WawiCallPro-phone-search-" + Guid.NewGuid().ToString("N"));
        var host = WawiCallHost.CreateBuilder(Assembly.GetExecutingAssembly()).ConfigureServices(services =>
        {
            services.AddSingleton(new ConfigurationLocation(root));
            services.AddSingleton<IUiDispatcher, InlineUiDispatcher>();
        }).Build();
        try
        {
            await HostExecution.RunAsync(host, async token =>
            {
                if (createDatabase) await host.Services.GetRequiredService<ICommunicationDatabase>().CreateAndMigrateAsync(cancellationToken: token);
                await action(host.Services);
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly IServiceProvider _services;
        internal readonly Clock Clock = new();
        internal readonly Source Source = new();
        internal readonly Matching Matching;
        internal readonly PhoneSearchIndexService Search;
        internal Company A { get; }
        internal Company B { get; }
        internal Fixture(IServiceProvider services)
        {
            _services = services;
            var line = Enum.Parse<LexwareProductLine>(services.GetRequiredService<ApplicationDescriptor>().ProductLine);
            A = new(new(line, "phone-search-A", "0001"), "Firma A");
            B = new(new(line, "phone-search-B", "0001"), "Firma B");
            Source.Companies = [A, B];
            Matching = new(services.GetRequiredService<ICompanyPhoneMatchingService>());
            Search = new(services.GetRequiredService<ConfigurationStore>(), services.GetRequiredService<ApplicationConfigurationService>(),
                services.GetRequiredService<ApplicationDescriptor>(), Source, services.GetRequiredService<IPartyCacheRepository>(),
                Matching, services.GetRequiredService<IPhoneCacheMetadata>(), Clock, new(), null, null);
        }
        internal async Task SelectAsync(params Company[] selected)
        {
            var store = _services.GetRequiredService<ConfigurationStore>();
            var saved = await store.LoadAsync();
            await store.SaveAsync(saved.Options with { Companies = selected.Select(company => new CompanySettings
            { Company = company.Key, DisplayName = company.DisplayName, IncludeInPhoneCache = true }).ToImmutableArray() }, saved);
        }
        internal async Task SeedAsync(Company company, PhoneSearchCompanyState? kind = null)
        {
            var companies = _services.GetRequiredService<ICompanyRepository>();
            if (await companies.GetAsync(company.Key) is null) await companies.WriteAsync(new(company, null, Clock.Now));
            var cache = _services.GetRequiredService<IPartyCacheRepository>();
            var state = await cache.GetStateAsync(company.Key);
            var metadata = _services.GetRequiredService<IPhoneCacheMetadata>();
            var party = new Party { Key = new(company.Key, PartyKind.Customer, "01"), DisplayName = "Synthetic party",
                Phones = [new("+49301234567", PhoneKind.Telephone, new("fixture", "01", "phone"), "+49301234567")] };
            await cache.ReplaceAsync(new(company.Key, state.Revision, [party],
                kind == PhoneSearchCompanyState.CacheStale ? Clock.Now.AddDays(-2) : Clock.Now,
                kind == PhoneSearchCompanyState.NormalizationOutdated ? "old-parser" : metadata.ParserVersion,
                kind == PhoneSearchCompanyState.ContextChanged ? metadata.GetContextVersion(new() { Company = company.Key, LocalAreaCode = "089" })
                    : metadata.GetContextVersion(new() { Company = company.Key })));
        }
        internal PhoneSearchResult Find() => Search.Find(_services.GetRequiredService<IPhoneNumberInterpreter>()
            .InterpretIncoming(new(NumberPresentation.Available, "+49301234567")));
        public ValueTask DisposeAsync() => Search.DisposeAsync();
    }

    private sealed class Clock : TimeProvider
    {
        internal DateTimeOffset Now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class Source : IBusinessDirectory
    {
        internal ImmutableArray<Company> Companies;
        internal Func<CancellationToken, Task<ImmutableArray<Company>>>? Read;
        internal int Reads;
        public Task<ImmutableArray<Company>> GetCompaniesAsync(CancellationToken cancellationToken = default)
        { Interlocked.Increment(ref Reads); return Read?.Invoke(cancellationToken) ?? Task.FromResult(Companies); }
        public Task<CapabilitySet> GetCapabilitiesAsync(CompanyKey company, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PartyPage> GetPartiesAsync(PartyQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Party?> GetPartyAsync(PartyKey key, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class Matching(ICompanyPhoneMatchingService inner) : ICompanyPhoneMatchingService
    {
        internal int ProfileReads;
        internal readonly List<CompanyKey> Builds = [];
        internal Func<CompanyKey, Task>? BeforeBuild;
        internal bool ForceIncomplete;
        public Task<PhoneMatchingProfile> GetProfileAsync(CompanyKey company, CancellationToken cancellationToken = default)
        { Interlocked.Increment(ref ProfileReads); return inner.GetProfileAsync(company, cancellationToken); }
        public Task<PhoneMatchingProfile> SaveProfileAsync(PhoneMatchingSettings settings, long? expectedVersion, CancellationToken cancellationToken = default)
            => inner.SaveProfileAsync(settings, expectedVersion, cancellationToken);
        public async Task<ICompanyPhoneMatchIndex> BuildIndexAsync(CompanyKey company, CancellationToken cancellationToken = default)
        {
            Builds.Add(company);
            if (BeforeBuild is { } before) await before(company);
            var index = await inner.BuildIndexAsync(company, cancellationToken);
            return ForceIncomplete ? new IncompleteIndex(index) : index;
        }
    }
    private sealed class IncompleteIndex(ICompanyPhoneMatchIndex inner) : ICompanyPhoneMatchIndex
    {
        public PhoneMatchingProfile Profile => inner.Profile;
        public long CacheRevision => inner.CacheRevision;
        public string Version => inner.Version;
        public bool IsComplete => false;
        public ImmutableArray<PhoneMatchCandidate> Find(PhoneNumberInterpretation incoming, CancellationToken cancellationToken = default)
            => inner.Find(incoming, cancellationToken);
    }
}

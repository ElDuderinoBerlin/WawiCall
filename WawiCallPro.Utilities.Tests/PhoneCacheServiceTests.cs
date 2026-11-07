using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WawiCallPro.Core.Companies;
using WawiCallPro.Core.Threading;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Capabilities;
using WawiCallPro.Interfaces.Common;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Interfaces.PhoneNumbers;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Hosting;
using WawiCallPro.Utilities.Persistence;

namespace WawiCallPro.Utilities.Tests;

[TestClass]
public sealed class PhoneCacheServiceTests
{
    [TestMethod]
    public Task SelectedAuthorizedCompaniesNormalizeAllPhoneOccurrencesAndPreserveDuplicatesAndSources() => RunAsync(async test =>
    {
        await test.SaveSelectionAsync(test.A, test.B);
        await test.Services.GetRequiredService<CompanyContext>().SelectAsync(test.C);
        var first = Party(test.A);
        var invalid = new PartyPhone("keine-telefonnummer", PhoneKind.Telephone, new("fixture", "01", "badPhone"));
        test.Source.Read = query => Task.FromResult(new PartyPage(query.Company, new(new Party[] {
            first with { Phones = [first.Phones[0], invalid], Addresses = [new PartyAddress
            {
                ExternalId = "address-1", Kind = AddressKind.Shipping,
                Phones = [new("+49 30 1234567 ext. 42", PhoneKind.Mobile, new("fixture_address", "address-1", "mobile"))]
            }] },
            Party(test.A, "02")
        }.Select(party => party with { Key = new(query.Company, party.Key.Kind, party.Key.ExternalId) }).ToImmutableArray(), false)));
        var result = await test.Cache.RefreshAsync();
        Assert.AreEqual(2, result.Companies.Length);
        Assert.IsTrue(result.Companies.All(company => company.State == PhoneCacheRefreshState.Updated));
        Assert.IsFalse(test.Source.Reads.Any(query => query.Company == test.C.Key));
        Assert.AreEqual(test.C.Key, test.Services.GetRequiredService<CompanyContext>().Current.Company!.Key);
        var stored = await test.Parties.GetAsync(first.Key);
        Assert.IsNotNull(stored);
        Assert.AreEqual("+49301234567", stored.Phones[0].E164);
        Assert.AreEqual(first.Phones[0].OriginalValue, stored.Phones[0].OriginalValue);
        Assert.AreEqual(first.Phones[0].Source, stored.Phones[0].Source);
        Assert.IsNull(stored.Phones[1].E164);
        Assert.AreEqual("42", stored.Addresses[0].Phones[0].Extension);
        Assert.AreEqual("fixture_address", stored.Addresses[0].Phones[0].Source.RecordType);
        Assert.AreEqual(2, (await test.Parties.FindByE164Async(test.A.Key, "+49301234567", new())).Page.Items.Length);
        var errors = await test.Services.GetRequiredService<INumberParseLogRepository>().GetPageAsync(new(test.A.Key, new()));
        Assert.AreEqual(invalid.OriginalValue, errors.Items.Single().OriginalValue);
        var state = await test.Parties.GetStateAsync(test.A.Key);
        Assert.IsNotNull(state.ParserVersion);
        Assert.AreEqual(64, state.ContextVersion!.Length);
    });

    [TestMethod]
    public Task LaterSourcePageFailureLeavesThePreviousCompleteCacheAndRevisionUntouched() => RunAsync(async test =>
    {
        await test.SaveSelectionAsync(test.A);
        await test.SeedAsync();
        test.Source.Read = query => query.Page.Offset == 0
            ? Task.FromResult(new PartyPage(query.Company, new([Party(test.A, "new")], true)))
            : throw new IOException("private-source-sentinel");
        var result = await test.Cache.RefreshAsync();
        Assert.AreEqual(PhoneCacheRefreshState.Failed, result.Companies.Single().State);
        Assert.AreEqual(1, (await test.Parties.GetStateAsync(test.A.Key)).Revision);
        Assert.IsNotNull(await test.Parties.GetAsync(new(test.A.Key, PartyKind.Customer, "old")));
        Assert.IsNull(await test.Parties.GetAsync(new(test.A.Key, PartyKind.Customer, "new")));
        Assert.AreEqual(2, test.Source.Reads.Count);
    });

    [TestMethod]
    public Task ConfirmedEmptySourceReplacesThePreviousCache() => RunAsync(async test =>
    {
        await test.SaveSelectionAsync(test.A);
        await test.SeedAsync();
        test.Source.Read = query => Task.FromResult(new PartyPage(query.Company, new([], false)));
        var result = await test.Cache.RefreshAsync();
        Assert.AreEqual(PhoneCacheRefreshState.Updated, result.Companies.Single().State);
        Assert.AreEqual(0, result.Companies.Single().PartyCount);
        Assert.AreEqual(2, (await test.Parties.GetStateAsync(test.A.Key)).Revision);
        Assert.IsNull(await test.Parties.GetAsync(new(test.A.Key, PartyKind.Customer, "old")));
    });

    [TestMethod]
    public Task UnauthorizedAndForeignInstallationKeysAreNotReadOrReassignedByDisplayName() => RunAsync(async test =>
    {
        var missing = new Company(new(test.A.Key.ProductLine, "unavailable-installation", test.A.Key.ExternalId), test.A.DisplayName);
        await test.SaveSelectionAsync(missing, test.B);
        var result = await test.Cache.RefreshAsync();
        Assert.AreEqual(PhoneCacheRefreshState.NotAuthorized, result.Companies[0].State);
        Assert.AreEqual(PhoneCacheRefreshState.Updated, result.Companies[1].State);
        Assert.IsTrue(test.Source.Reads.All(query => query.Company == test.B.Key));
        Assert.IsNull(await test.Companies.GetAsync(missing.Key));
        Assert.IsNull(await test.Companies.GetAsync(test.A.Key));
    });

    [TestMethod]
    public Task CancellationWaitsForTheSourceAndNeverPublishesItsLateResponse() => RunAsync(async test =>
    {
        await test.SaveSelectionAsync(test.A);
        await test.SeedAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        test.Source.Read = async query => { entered.SetResult(); await release.Task; return new(query.Company, new([Party(test.A, "late")], false)); };
        using var cancellation = new CancellationTokenSource();
        var refresh = test.Cache.RefreshAsync(cancellationToken: cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        Assert.IsFalse(refresh.IsCompleted);
        release.SetResult();
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await refresh);
        Assert.AreEqual(1, (await test.Parties.GetStateAsync(test.A.Key)).Revision);
        Assert.IsNull(await test.Parties.GetAsync(new(test.A.Key, PartyKind.Customer, "late")));
    });

    [TestMethod]
    public Task SavedDeselectionDuringReadDropsTheResponseWithoutTouchingTheOldCache() => RunAsync(async test =>
    {
        await test.SaveSelectionAsync(test.A);
        await test.SeedAsync();
        test.Source.Read = async query => { await test.SaveSelectionAsync(); return new(query.Company, new([Party(test.A, "stale")], false)); };
        var result = await test.Cache.RefreshAsync();
        Assert.AreEqual(PhoneCacheRefreshState.SelectionChanged, result.Companies.Single().State);
        Assert.AreEqual(1, (await test.Parties.GetStateAsync(test.A.Key)).Revision);
        Assert.IsNotNull(await test.Parties.GetAsync(new(test.A.Key, PartyKind.Customer, "old")));
    });

    [TestMethod]
    public Task ConcurrentCacheCommitIsPreservedAndReportedAsAConflict() => RunAsync(async test =>
    {
        await test.SaveSelectionAsync(test.A);
        await test.SeedAsync();
        test.Source.Read = async query =>
        {
            await test.Parties.ReplaceAsync(new(test.A.Key, 1, [Party(test.A, "concurrent")], DateTimeOffset.UtcNow));
            return new(query.Company, new([Party(test.A, "stale")], false));
        };
        var result = await test.Cache.RefreshAsync();
        Assert.AreEqual(PhoneCacheRefreshFailure.Conflict, result.Companies.Single().Failure);
        Assert.AreEqual(2, (await test.Parties.GetStateAsync(test.A.Key)).Revision);
        Assert.IsNotNull(await test.Parties.GetAsync(new(test.A.Key, PartyKind.Customer, "concurrent")));
        Assert.IsNull(await test.Parties.GetAsync(new(test.A.Key, PartyKind.Customer, "stale")));
    });

    [TestMethod]
    public Task MalformedOrWrongCompanyPagesDoNotRegisterANewCompany() => RunAsync(async test =>
    {
        await test.SaveSelectionAsync(test.A);
        test.Source.Read = _ => Task.FromResult(new PartyPage(test.B.Key, new([], false)));
        var result = await test.Cache.RefreshAsync();
        Assert.AreEqual(PhoneCacheRefreshFailure.InvalidSource, result.Companies.Single().Failure);
        Assert.IsNull(await test.Companies.GetAsync(test.A.Key));
    });

    [TestMethod]
    public Task FailedCompanyDoesNotPreventOtherSelectedCompanyFromFinishing() => RunAsync(async test =>
    {
        await test.SaveSelectionAsync(test.A, test.B);
        test.Source.Read = query => query.Company == test.A.Key ? throw new IOException() : Task.FromResult(new PartyPage(query.Company, new([Party(test.B)], false)));
        var result = await test.Cache.RefreshAsync();
        Assert.AreEqual(PhoneCacheRefreshState.Failed, result.Companies[0].State);
        Assert.AreEqual(PhoneCacheRefreshState.Updated, result.Companies[1].State);
        Assert.IsNotNull(await test.Parties.GetAsync(new(test.B.Key, PartyKind.Customer, "01")));
    });

    [TestMethod]
    public Task NormalizationContextFingerprintIsStableAndChangesWithCompanyDefaults() => RunAsync(async test =>
    {
        await test.SaveSelectionAsync(test.A);
        await test.Cache.RefreshAsync();
        var first = await test.Parties.GetStateAsync(test.A.Key);
        await test.Cache.RefreshAsync();
        Assert.AreEqual(first.ContextVersion, (await test.Parties.GetStateAsync(test.A.Key)).ContextVersion);
        var saved = await test.Settings.LoadAsync();
        await test.Settings.SaveAsync(saved.Options with { Companies = [saved.Options.Companies.Single() with { LocalAreaCode = "030" }] }, saved);
        await test.Cache.RefreshAsync();
        var updated = await test.Parties.GetStateAsync(test.A.Key);
        Assert.AreNotEqual(first.ContextVersion, updated.ContextVersion);
        Assert.AreEqual(first.ParserVersion, updated.ParserVersion);
    });

    [TestMethod]
    public Task DatabaseTargetChangesRequireRestartAndDoNotCreateAnotherDatabase() => RunAsync(async test =>
    {
        await test.SaveSelectionAsync(test.A);
        var saved = await test.Settings.LoadAsync();
        await test.Settings.SaveAsync(saved.Options with { Database = saved.Options.Database with { SqliteFileName = "other.db" } }, saved);
        var failure = await Assert.ThrowsAsync<PhoneCacheRefreshException>(() => test.Cache.RefreshAsync());
        Assert.AreEqual(PhoneCacheRefreshFailure.RestartRequired, failure.Failure);
        Assert.IsFalse(File.Exists(Path.Combine(test.Root, "other.db")));
        Assert.AreEqual(0, test.Source.CatalogReads);
    });

    [TestMethod]
    public Task EmptySelectionDoesNotOpenTheDatabaseOrReadTheWawiCatalog() => RunAsync(async test =>
    {
        var result = await test.Cache.RefreshAsync();
        Assert.IsTrue(result.Companies.IsEmpty);
        Assert.AreEqual(0, test.Source.CatalogReads);
        Assert.IsFalse(File.Exists(Path.Combine(test.Root, "communications.db")));
    }, createDatabase: false);

    private static Party Party(Company company, string id = "01") => new()
    {
        Key = new(company.Key, PartyKind.Customer, id), DisplayName = "Geschäftspartner " + id,
        Phones = [new("+49 30 1234567", PhoneKind.Telephone, new("fixture", id, "telephone"))]
    };

    private static async Task RunAsync(Func<Fixture, Task> action, bool createDatabase = true)
    {
        var root = Path.Combine(Path.GetTempPath(), "WawiCallPro-cache-" + Guid.NewGuid().ToString("N"));
        var source = new Source();
        var host = WawiCallHost.CreateBuilder(Assembly.GetExecutingAssembly()).ConfigureServices(services =>
        {
            services.AddSingleton(new ConfigurationLocation(root));
            services.AddSingleton<IUiDispatcher, InlineUiDispatcher>();
            services.AddSingleton<IBusinessDirectory>(source);
            services.AddSingleton<IPhoneCacheService, PhoneCacheService>();
        }).Build();
        try
        {
            await HostExecution.RunAsync(host, async token =>
            {
                if (createDatabase) await host.Services.GetRequiredService<ICommunicationDatabase>().CreateAndMigrateAsync(cancellationToken: token);
                var fixture = new Fixture(host.Services, root, source);
                source.Companies = [fixture.A, fixture.B, fixture.C];
                source.Read = query => Task.FromResult(new PartyPage(query.Company, new([Party(source.Companies.Single(company => company.Key == query.Company))], false)));
                await action(fixture);
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private sealed class Fixture(IServiceProvider services, string root, Source source)
    {
        internal IServiceProvider Services => services;
        internal string Root => root;
        internal Source Source => source;
        internal IPhoneCacheService Cache => services.GetRequiredService<IPhoneCacheService>();
        internal IPartyCacheRepository Parties => services.GetRequiredService<IPartyCacheRepository>();
        internal ICompanyRepository Companies => services.GetRequiredService<ICompanyRepository>();
        internal ConfigurationStore Settings => services.GetRequiredService<ConfigurationStore>();
        private LexwareProductLine ProductLine => Enum.Parse<LexwareProductLine>(services.GetRequiredService<ApplicationDescriptor>().ProductLine);
        internal Company A => new(new(ProductLine, "installation-A", "01"), "Firma A");
        internal Company B => new(new(ProductLine, "installation-B", "01"), "Firma B");
        internal Company C => new(new(ProductLine, "installation-A", "03"), "Firma C");
        internal async Task SaveSelectionAsync(params Company[] selected)
        {
            var snapshot = await Settings.LoadAsync();
            await Settings.SaveAsync(snapshot.Options with { Companies = selected.Select(company => new CompanySettings
            { Company = company.Key, IncludeInPhoneCache = true, DisplayName = company.DisplayName }).ToImmutableArray() }, snapshot);
        }
        internal async Task SeedAsync()
        {
            await Companies.WriteAsync(new(A, null, DateTimeOffset.UtcNow));
            await Parties.ReplaceAsync(new(A.Key, 0, [Party(A, "old")], DateTimeOffset.UtcNow));
        }
    }

    private sealed class Source : IBusinessDirectory
    {
        internal ImmutableArray<Company> Companies = [];
        internal Func<PartyQuery, Task<PartyPage>> Read = null!;
        internal readonly ConcurrentQueue<PartyQuery> Reads = new();
        internal int CatalogReads;
        public Task<ImmutableArray<Company>> GetCompaniesAsync(CancellationToken cancellationToken = default) { Interlocked.Increment(ref CatalogReads); return Task.FromResult(Companies); }
        public Task<PartyPage> GetPartiesAsync(PartyQuery query, CancellationToken cancellationToken = default) { Reads.Enqueue(query); return Read(query); }
        public Task<CapabilitySet> GetCapabilitiesAsync(CompanyKey company, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Party?> GetPartyAsync(PartyKey key, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}

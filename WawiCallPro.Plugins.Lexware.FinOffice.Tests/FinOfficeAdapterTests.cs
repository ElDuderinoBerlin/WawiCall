using System.Collections.Concurrent;
using System.Data;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Quantarix.Pcfk.DataAccess.Domain;
using Quantarix.Pcfk.DataAccess.Entities;
using Quantarix.Pcfk.DataAccess.Infrastructure;
using Quantarix.Pcfk.DataAccess.Manager;
using Quantarix.Pcfk.DataAccess.Repositories;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;

namespace WawiCallPro.Plugins.Lexware.FinOffice.Tests;

[TestClass]
public sealed class FinOfficeAdapterTests
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WawiCallPro-fin-adapter-" + Guid.NewGuid().ToString("N"));
    private FixtureManager _manager = null!;

    [TestInitialize]
    public async Task Initialize()
    {
        Directory.CreateDirectory(_root);
        _manager = new FixtureManager(_root);
        using (var catalog = await Open(_manager.CatalogPath, readOnly: false))
        {
            await catalog.ExecuteAsync("CREATE TABLE lx_firma_view_verwaltg (lNr INTEGER, iKennung INTEGER, szName TEXT);");
            await catalog.ExecuteAsync("INSERT INTO lx_firma_view_verwaltg VALUES (2, 2, 'Alpha'), (1, 1, 'Zeta');");
        }
        foreach (var nr in new[] { 1, 2 })
        {
            var path = _manager.CompanyPath(nr);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var connection = await Open(path, readOnly: false);
            await connection.ExecuteAsync("CREATE TABLE sentinel (value TEXT); INSERT INTO sentinel VALUES (@value);", new { value = "company-" + nr });
        }
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [TestMethod]
    public async Task RealDataAccessCatalogMapsSortedCompaniesAndReadsWithoutWrites()
    {
        await using var provider = Build();
        var companies = await provider.GetRequiredService<IBusinessDirectory>().GetCompaniesAsync();
        CollectionAssert.AreEqual(new[] { "Alpha", "Zeta" }, companies.Select(company => company.DisplayName).ToArray());
        Assert.AreEqual(new CompanyKey(LexwareProductLine.FinOffice, "isolated-fin", "2"), companies[0].Key);
        var result = await provider.GetRequiredService<IFinOfficeCompanySession>().ReadAsync(companies[1].Key,
            (connection, _) => connection.QuerySingleAsync<string>("SELECT value FROM sentinel"));
        Assert.AreEqual("company-1", result);
        Assert.IsTrue(_manager.ReadOnlyRequests.All(readOnly => readOnly));
        AssertConnectionsDisposed();
    }

    [TestMethod]
    public async Task EmptySuccessfulCatalogIsEmptyAndDoesNotInventCompanies()
    {
        using (var catalog = await Open(_manager.CatalogPath, false)) await catalog.ExecuteAsync("DELETE FROM lx_firma_view_verwaltg");
        await using var provider = Build();
        Assert.IsEmpty(await provider.GetRequiredService<IBusinessDirectory>().GetCompaniesAsync());
        Assert.IsEmpty(_manager.Connections);
    }

    [TestMethod]
    [DataRow("duplicate")]
    [DataRow("missing-name")]
    [DataRow("zero-id")]
    [DataRow("null-list")]
    [DataRow("relative-path")]
    [DataRow("wrong-directory")]
    public async Task InvalidOrPartialCatalogsFailWithoutLeakingSourceValues(string fault)
    {
        _manager.Transform = installation => fault switch
        {
            "duplicate" => installation with { Mandanten = [installation.Mandanten[0], installation.Mandanten[0]] },
            "missing-name" => installation with { Mandanten = [installation.Mandanten[0] with { Firma = new LxFirmaVerwaltungView { Nr = 1 } }] },
            "zero-id" => installation with { Mandanten = [installation.Mandanten[0] with { Firma = new LxFirmaVerwaltungView { Nr = 0, Name = "private-source-value" } }] },
            "null-list" => installation with { Mandanten = null! },
            "relative-path" => installation with { CompaniesDatabasePath = "private-source-value" },
            "wrong-directory" => installation with { Mandanten = [installation.Mandanten[0] with { DirectoryPath = _root }] },
            _ => throw new InvalidOperationException()
        };
        await using var provider = Build();
        var error = await Assert.ThrowsExactlyAsync<FinOfficeAdapterException>(() => provider.GetRequiredService<IBusinessDirectory>().GetCompaniesAsync());
        Assert.AreEqual(FinOfficeFailure.InvalidCatalog, error.Failure);
        Assert.IsNull(error.InnerException);
        Assert.DoesNotContain(_root, error.ToString());
        Assert.DoesNotContain("private-source-value", error.ToString());
    }

    [TestMethod]
    public async Task MissingDatabaseDoesNotFallBackToAnotherCompanyOrCreateAFile()
    {
        var missing = _manager.CompanyPath(1);
        File.Delete(missing);
        await using var provider = Build();
        var error = await Assert.ThrowsExactlyAsync<FinOfficeAdapterException>(() => provider.GetRequiredService<IBusinessDirectory>().GetCompaniesAsync());
        Assert.AreEqual(FinOfficeFailure.CompanyUnavailable, error.Failure);
        Assert.IsFalse(File.Exists(missing));
        Assert.IsEmpty(_manager.Connections);
    }

    [TestMethod]
    public async Task InstallationErrorsAreNotConvertedToEmptySuccessOrRawExceptionText()
    {
        _manager.DetectionError = new IOException("private-source-value");
        await using var provider = Build();
        var error = await Assert.ThrowsExactlyAsync<FinOfficeAdapterException>(() => provider.GetRequiredService<IBusinessDirectory>().GetCompaniesAsync());
        Assert.AreEqual(FinOfficeFailure.InstallationUnavailable, error.Failure);
        Assert.IsNull(error.InnerException);
        Assert.DoesNotContain("private-source-value", error.ToString());
    }

    [TestMethod]
    [DataRow("product", FinOfficeFailure.ForeignCompany)]
    [DataRow("installation", FinOfficeFailure.ForeignCompany)]
    [DataRow("unknown", FinOfficeFailure.CompanyUnavailable)]
    [DataRow("leading-zero", FinOfficeFailure.CompanyUnavailable)]
    public async Task ForeignOrUnknownCompanyCannotOpenAnotherDatabase(string fault, FinOfficeFailure expected)
    {
        var company = fault switch
        {
            "product" => new CompanyKey(LexwareProductLine.Pro, "isolated-fin", "1"),
            "installation" => new CompanyKey(LexwareProductLine.FinOffice, "another-installation", "1"),
            "unknown" => new CompanyKey(LexwareProductLine.FinOffice, "isolated-fin", "99"),
            _ => new CompanyKey(LexwareProductLine.FinOffice, "isolated-fin", "01")
        };
        await using var provider = Build();
        var error = await Assert.ThrowsExactlyAsync<FinOfficeAdapterException>(() => provider.GetRequiredService<IFinOfficeCompanySession>()
            .ReadAsync(company, (_, _) => Task.FromResult(0)));
        Assert.AreEqual(expected, error.Failure);
        Assert.IsEmpty(_manager.Connections);
    }

    [TestMethod]
    public async Task ParallelCompanyReadsUseIndependentConnectionsAndDisposeThem()
    {
        await using var provider = Build();
        var session = provider.GetRequiredService<IFinOfficeCompanySession>();
        var results = await Task.WhenAll(Enumerable.Range(1, 2).Select(nr => session.ReadAsync(
            new CompanyKey(LexwareProductLine.FinOffice, "isolated-fin", nr.ToString()), async (connection, token) =>
            {
                await Task.Delay(20, token);
                return await connection.QuerySingleAsync<string>("SELECT value FROM sentinel");
            })));
        CollectionAssert.AreEquivalent(new[] { "company-1", "company-2" }, results);
        Assert.AreEqual(2, _manager.Connections.Distinct().Count());
        AssertConnectionsDisposed();
    }

    [TestMethod]
    public async Task ReadOnlySourceRejectsWritesAndDisposesOnFailure()
    {
        await using var provider = Build();
        var error = await Assert.ThrowsExactlyAsync<FinOfficeAdapterException>(() => provider.GetRequiredService<IFinOfficeCompanySession>()
            .ReadAsync(new CompanyKey(LexwareProductLine.FinOffice, "isolated-fin", "1"),
                (connection, _) => connection.ExecuteAsync("INSERT INTO sentinel VALUES ('not-permitted')")));
        Assert.AreEqual(FinOfficeFailure.ReadFailed, error.Failure);
        using var check = await Open(_manager.CompanyPath(1), true);
        Assert.AreEqual(1, await check.ExecuteScalarAsync<long>("SELECT count(*) FROM sentinel"));
        AssertConnectionsDisposed();
    }

    [TestMethod]
    public async Task PreCancelledDiscoveryDoesNotTouchTheInstallation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await using var provider = Build();
        await Assert.ThrowsAsync<OperationCanceledException>(() => provider.GetRequiredService<IBusinessDirectory>().GetCompaniesAsync(cancellation.Token));
        Assert.IsEmpty(_manager.ReadOnlyRequests);
    }

    [TestMethod]
    public async Task CancellationAfterReadStartsWaitsForConnectionDisposalAndDropsTheResult()
    {
        await using var provider = Build();
        using var cancellation = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var operation = provider.GetRequiredService<IFinOfficeCompanySession>().ReadAsync(
            new CompanyKey(LexwareProductLine.FinOffice, "isolated-fin", "1"), async (_, _) =>
            {
                entered.SetResult();
                await release.Task;
                return "stale";
            }, cancellation.Token);
        await entered.Task;
        cancellation.Cancel();
        Assert.IsFalse(operation.IsCompleted);
        release.SetResult();
        await Assert.ThrowsAsync<OperationCanceledException>(() => operation);
        AssertConnectionsDisposed();
    }

    [TestMethod]
    public async Task DeterministicIdentityUsesCatalogPathAndSupportsAnExplicitPersistentId()
    {
        var first = await new FinOfficeInstallationSource(_manager, new FinOfficeConnectionOptions()).DetectAsync();
        var second = await new FinOfficeInstallationSource(_manager, new FinOfficeConnectionOptions()).DetectAsync();
        Assert.AreEqual(first.Id, second.Id);
        Assert.StartsWith("lexware-finoffice/", first.Id);
        Assert.DoesNotContain(_root, first.Id);
        var persistent = await new FinOfficeInstallationSource(_manager, new FinOfficeConnectionOptions { InstallationId = "persistent" }).DetectAsync();
        Assert.AreEqual("persistent", persistent.Id);
    }

    [TestMethod]
    public async Task ProductionRegistrationIsLazyAndResolvesTheActualQuantarixManager()
    {
        var services = new ServiceCollection();
        new FinOfficePluginServices().ConfigureServices(services);
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        Assert.IsInstanceOfType<PcfkInstallationManager>(provider.GetRequiredService<IPcfkInstallationManager>());
        Assert.IsInstanceOfType<FinOfficeBusinessDirectory>(provider.GetRequiredService<IBusinessDirectory>());
        Assert.IsInstanceOfType<FinOfficeCompanySession>(provider.GetRequiredService<IFinOfficeCompanySession>());
    }

    private void AssertConnectionsDisposed()
    {
        foreach (var connection in _manager.Connections)
            Assert.ThrowsExactly<ObjectDisposedException>(() => _ = connection.State);
    }

    private ServiceProvider Build()
    {
        var services = new ServiceCollection();
        new FinOfficePluginServices().ConfigureServices(services);
        services.AddSingleton<IPcfkInstallationManager>(_manager);
        services.AddSingleton(new FinOfficeConnectionOptions { InstallationId = "isolated-fin" });
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    private static Task<IDbConnection> Open(string path, bool readOnly)
        => new PcfkSqliteConnectionFactory(new PcfkConnectionOptions { DatabasePath = path, ReadOnly = readOnly }).CreateOpenConnectionAsync();

    private sealed class FixtureManager(string root) : IPcfkInstallationManager
    {
        public string CatalogPath { get; } = Path.Combine(root, "companies.dbs");
        public ConcurrentBag<bool> ReadOnlyRequests { get; } = [];
        public ConcurrentBag<IDbConnection> Connections { get; } = [];
        public Exception? DetectionError { get; set; }
        public Func<PcfkInstallationInfo, PcfkInstallationInfo>? Transform { get; set; }
        public string CompanyPath(int nr) => Path.Combine(root, nr.ToString("D8"), "fkstdcompany.dbs");
        public async Task<PcfkInstallationInfo> GetInstallationAsync(bool readOnly = true)
        {
            ReadOnlyRequests.Add(readOnly);
            if (DetectionError is not null) throw DetectionError;
            using var connection = await Open(CatalogPath, readOnly);
            var rows = await new PcfkFirmaRepository(connection).GetAllAsync();
            var mandants = rows.Select(row => new PcfkMandantInfo(row, null, null, Path.GetDirectoryName(CompanyPath(row.Nr))!, CompanyPath(row.Nr))).ToArray();
            var result = new PcfkInstallationInfo(root, CatalogPath, mandants);
            return Transform?.Invoke(result) ?? result;
        }
        public async Task<IReadOnlyList<PcfkMandantInfo>> GetMandantenAsync(bool readOnly = true) => (await GetInstallationAsync(readOnly)).Mandanten;
        public async Task<PcfkMandantInfo?> GetMandantAsync(int firmaNr, bool readOnly = true) => (await GetMandantenAsync(readOnly)).SingleOrDefault(mandant => mandant.Nr == firmaNr);
        public Task<IDbConnection> CreateCompaniesConnectionAsync(bool readOnly = true) => Open(CatalogPath, readOnly);
        public async Task<IDbConnection> CreateMandantConnectionAsync(PcfkMandantInfo mandant, bool readOnly = true)
        {
            ReadOnlyRequests.Add(readOnly);
            var connection = await Open(mandant.DatabasePath, readOnly);
            Connections.Add(connection);
            return connection;
        }
    }
}

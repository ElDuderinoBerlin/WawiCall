using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Capabilities;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Common;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Core.Companies;
using WawiCallPro.Core.Threading;
using WawiCallPro.Interfaces.Hosting;

namespace WawiCallPro.Plugins.Lexware.Pro.Tests;

[TestClass]
public sealed class ProAdapterTests
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WawiCallPro-pro-adapter-" + Guid.NewGuid().ToString("N"));
    private static readonly Uri ApiUrl = new("http://isolated.invalid:9710");
    private static ProConnectionOptions Credentials => new()
    {
        ApiUrl = ApiUrl, InstallationId = "isolated-installation", Username = "synthetic-user",
        Password = "synthetic-password", ClientKey = "{11111111-1111-1111-1111-111111111111}"
    };

    [TestMethod]
    public async Task CapabilitiesCheckAuthorizedCompanyAndKeepRoleLimits()
    {
        var handler = new ScriptedHandler((request, _) => Task.FromResult(IsLogin(request)
            ? Json("""{"data":{"token":"opaque-token"}}""")
            : request.Query.Contains("companies", StringComparison.Ordinal)
                ? Json("""{"data":{"companies":[{"id":1,"name":"Test"}]}}""")
                : Json("""{"data":{"company":{"companyName1":"Test"}}}""")));
        await using var provider = Build(handler);
        var directory = provider.GetRequiredService<IBusinessDirectory>();
        var company = new CompanyKey(LexwareProductLine.Pro, "isolated-installation", "1");
        var report = await directory.GetCapabilitiesAsync(company);
        Assert.AreEqual(company, report.Context!.Company);
        Assert.AreEqual(FeatureAvailability.Supported, report.Context.Customers.GetAvailability(BusinessFeature.AdditionalAddresses));
        Assert.AreEqual(FeatureAvailability.Partial, report.Context.Suppliers.GetAvailability(BusinessFeature.AdditionalAddresses));
        Assert.AreEqual(FeatureAvailability.Partial, report.GetAvailability(BusinessFeature.AdditionalAddresses));
        Assert.AreEqual(FeatureAvailability.Unsupported, report.GetAvailability(BusinessFeature.Orders));
        Assert.AreEqual(FeatureAvailability.Unsupported, report.GetAvailability(BusinessFeature.UpdateAddresses));
        Assert.AreEqual(Enum.GetValues<BusinessFeature>().Length, report.Entries.Count);
        Assert.AreEqual("1", handler.Requests.Last().Headers["CompanyId"]);
        var restored = JsonSerializer.Deserialize<CapabilitySet>(JsonSerializer.Serialize(report))!;
        Assert.AreEqual(company, restored.Context!.Company);
        Assert.AreEqual(FeatureAvailability.Partial, restored.Context.Suppliers.GetAvailability(BusinessFeature.AdditionalAddresses));
        await Assert.ThrowsExactlyAsync<ProAdapterException>(() => directory.GetCapabilitiesAsync(
            new CompanyKey(LexwareProductLine.Pro, "isolated-installation", "2")));
        await Assert.ThrowsExactlyAsync<ProAdapterException>(() => directory.GetCapabilitiesAsync(
            new CompanyKey(LexwareProductLine.Pro, "other", "1")));
    }

    [TestMethod]
    public async Task CapabilitiesDoNotHideApiFailureOrCancellation()
    {
        await using var provider = Build(DefaultHandler("""{"errors":[{"extensions":{"code":"2001"}}]}"""));
        var directory = provider.GetRequiredService<IBusinessDirectory>();
        var company = new CompanyKey(LexwareProductLine.Pro, "isolated-installation", "1");
        await Assert.ThrowsExactlyAsync<ProAdapterException>(() => directory.GetCapabilitiesAsync(company));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => directory.GetCapabilitiesAsync(company, cancellation.Token));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task CapabilitiesDoNotPublishWhenScopedCompanyReadFailsOrIsCancelled(bool cancel)
    {
        using var cancellation = new CancellationTokenSource();
        var handler = new ScriptedHandler((request, _) =>
        {
            if (IsLogin(request)) return Task.FromResult(Json("""{"data":{"token":"opaque-token"}}"""));
            if (request.Query.Contains("companies", StringComparison.Ordinal))
                return Task.FromResult(Json("""{"data":{"companies":[{"id":1,"name":"Test"}]}}"""));
            if (cancel) cancellation.Cancel();
            return Task.FromResult(Json(cancel ? """{"data":{"company":{"companyName1":"Late"}}}"""
                : """{"errors":[{"extensions":{"code":"2001"}}]}"""));
        });
        await using var provider = Build(handler);
        var directory = provider.GetRequiredService<IBusinessDirectory>();
        var company = new CompanyKey(LexwareProductLine.Pro, "isolated-installation", "1");
        if (cancel)
            await Assert.ThrowsAsync<OperationCanceledException>(() => directory.GetCapabilitiesAsync(company, cancellation.Token));
        else
            await Assert.ThrowsExactlyAsync<ProAdapterException>(() => directory.GetCapabilitiesAsync(company));
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    [TestMethod]
    public async Task CompanyCatalogUsesRealApiClientAndOnlyAuthorizedCompanyIds()
    {
        var store = CreateStore();
        WriteCompany(store, "1", "Lokaler Name");
        WriteCompany(store, "2", "Name aus PropertyStore");
        WriteCompany(store, "99", "Nicht freigegebene Firma");
        var handler = new ScriptedHandler((request, _) => Task.FromResult(IsLogin(request) ? Json("""{"data":{"token":"opaque-token"}}""")
            : Json("""{"data":{"companies":[{"id":2,"name":null},{"id":1,"name":"API-Name"}]}}""")));
        await using var provider = Build(handler, store);
        var directory = provider.GetRequiredService<IBusinessDirectory>();
        var companies = await directory.GetCompaniesAsync();
        Assert.AreEqual(2, companies.Length);
        Assert.AreEqual("API-Name", companies[0].DisplayName);
        Assert.AreEqual("Name aus PropertyStore", companies[1].DisplayName);
        Assert.AreEqual(new CompanyKey(LexwareProductLine.Pro, "isolated-installation", "2"), companies[1].Key);
        Assert.IsFalse(handler.Requests.Last().Headers.ContainsKey("CompanyId"));
        Assert.IsTrue(handler.Requests.All(request => request.Headers["ClientKey"] == Credentials.ClientKey));
        Assert.IsFalse(Credentials.ToString().Contains("synthetic-password", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task SuccessfulEmptyApiCatalogStaysEmptyDespiteLocalCompanies()
    {
        var store = CreateStore(); WriteCompany(store, "1", "Nur lokal");
        await using var provider = Build(DefaultHandler("""{"data":{"companies":[]}}"""), store);
        Assert.AreEqual(0, (await provider.GetRequiredService<IBusinessDirectory>().GetCompaniesAsync()).Length);
    }

    [TestMethod]
    [DataRow("{\"data\":{}}")]
    [DataRow("{\"data\":{\"companies\":null}}")]
    [DataRow("{\"data\":{\"companies\":[]},\"errors\":[{\"message\":\"private-server-secret\"}]}")]
    [DataRow("{\"data\":{\"companies\":[{\"id\":1,\"name\":\"A\"},{\"id\":1,\"name\":\"B\"}]}}")]
    [DataRow("{\"data\":{\"companies\":[{\"id\":0,\"name\":\"A\"}]}}")]
    [DataRow("{\"data\":{\"companies\":[{\"id\":1,\"name\":null}]}}")]
    [DataRow("not-json")]
    [DataRow("{\"data\":{\"companies\":[]},\"errors\":{\"message\":\"private-server-secret\"}}")]
    public async Task MalformedPartialOrInconsistentResultsNeverBecomeEmptySuccess(string response)
    {
        await using var provider = Build(DefaultHandler(response));
        var error = await Assert.ThrowsExactlyAsync<ProAdapterException>(() => provider.GetRequiredService<IBusinessDirectory>().GetCompaniesAsync());
        Assert.AreEqual(ProFailure.InvalidResponse, error.Failure);
        Assert.IsFalse(error.ToString().Contains("private-server-secret", StringComparison.Ordinal));
        Assert.IsNull(error.InnerException);
    }

    [TestMethod]
    [DataRow(3001)]
    [DataRow(3002)]
    public async Task ExplicitTokenRejectionRenewsOnceAndRepeatsRead(int code)
    {
        var logins = 0; var reads = 0;
        var handler = new ScriptedHandler((request, _) => Task.FromResult(IsLogin(request)
            ? Json(JsonSerializer.Serialize(new { data = new { token = "token-" + ++logins } }))
            : ++reads == 1 ? Json(JsonSerializer.Serialize(new { errors = new[] { new { extensions = new { code = code.ToString() } } } }))
            : Json("""{"data":{"companies":[]}}""")));
        await using var provider = Build(handler);
        await provider.GetRequiredService<IBusinessDirectory>().GetCompaniesAsync();
        Assert.AreEqual(2, logins); Assert.AreEqual(2, reads);
        Assert.AreEqual("token-2", handler.Requests.Last().Headers["Token"]);
    }

    [TestMethod]
    public async Task RepeatedInvalidTokenStopsAfterOneRenewal()
    {
        var handler = DefaultHandler("""{"errors":[{"extensions":{"code":"3001"}}]}""");
        await using var provider = Build(handler);
        var error = await Assert.ThrowsExactlyAsync<ProAdapterException>(() => provider.GetRequiredService<IBusinessDirectory>().GetCompaniesAsync());
        Assert.AreEqual(ProFailure.InvalidToken, error.Failure);
        Assert.AreEqual(4, handler.Requests.Count);
    }

    [TestMethod]
    [DataRow(401, 2, ProFailure.InvalidToken)]
    [DataRow(403, 1, ProFailure.AuthenticationFailed)]
    [DataRow(500, 1, ProFailure.ApiUnavailable)]
    public async Task HttpErrorsRetryOnlyTokenRejection(int status, int expectedReads, ProFailure expectedFailure)
    {
        var handler = new ScriptedHandler((request, _) => Task.FromResult(IsLogin(request)
            ? Json("""{"data":{"token":"opaque-token"}}""")
            : new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent("private-server-secret") }));
        await using var provider = Build(handler);
        var error = await Assert.ThrowsExactlyAsync<ProAdapterException>(() => provider.GetRequiredService<IBusinessDirectory>().GetCompaniesAsync());
        Assert.AreEqual(expectedFailure, error.Failure);
        Assert.AreEqual(expectedReads, handler.Requests.Count(request => !IsLogin(request)));
        Assert.IsFalse(error.ToString().Contains("private-server-secret", StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow("{\"data\":{\"token\":null}}")]
    [DataRow("{\"data\":{\"token\":\"\"}}")]
    [DataRow("{\"errors\":[{\"message\":\"synthetic-password\"}]}")]
    public async Task FailedLoginNeverQueriesCompaniesOrExposesCredentials(string response)
    {
        var handler = new ScriptedHandler((_, _) => Task.FromResult(Json(response)));
        await using var provider = Build(handler);
        var error = await Assert.ThrowsExactlyAsync<ProAdapterException>(() => provider.GetRequiredService<IBusinessDirectory>().GetCompaniesAsync());
        Assert.AreEqual(1, handler.Requests.Count);
        Assert.IsFalse(error.ToString().Contains("synthetic-password", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task TokenRenewalPreservesCompanyAndGlobalCatalogDoesNotInheritItsHeader()
    {
        var companyReads = 0;
        var handler = new ScriptedHandler((request, _) =>
        {
            if (IsLogin(request)) return Task.FromResult(Json("""{"data":{"token":"opaque-token"}}"""));
            if (request.Query.Contains("companies", StringComparison.Ordinal))
                return Task.FromResult(Json("""{"data":{"companies":[]}}"""));
            Assert.AreEqual("7", request.Headers["CompanyId"]);
            return Task.FromResult(++companyReads == 1
                ? Json("""{"errors":[{"extensions":{"code":"3001"}}]}""")
                : Json("""{"data":{"company":{"companyName1":"Company 7"}}}"""));
        });
        await using var provider = Build(handler);
        var session = provider.GetRequiredService<IProApiSession>();
        var company = await session.ReadCompanyAsync(new CompanyKey(LexwareProductLine.Pro, "isolated-installation", "7"),
            (client, token, ct) => client.GetCompanyAsync(token, ct), CancellationToken.None);
        Assert.AreEqual("Company 7", company.CompanyName1);
        await session.GetCompaniesAsync(CancellationToken.None);
        Assert.IsFalse(handler.Requests.Last().Headers.ContainsKey("CompanyId"));
        Assert.AreEqual(2, companyReads);
    }

    [TestMethod]
    public async Task PermissionErrorsAreNotRetriedOrReplacedWithPropertyStoreData()
    {
        var store = CreateStore(); WriteCompany(store, "1", "Lokal sichtbar");
        var handler = DefaultHandler("""{"errors":[{"message":"private-server-secret","extensions":{"code":"2001"}}]}""");
        await using var provider = Build(handler, store);
        var error = await Assert.ThrowsExactlyAsync<ProAdapterException>(() => provider.GetRequiredService<IBusinessDirectory>().GetCompaniesAsync());
        Assert.AreEqual(ProFailure.InvalidResponse, error.Failure);
        Assert.AreEqual(2, handler.Requests.Count);
    }

    [TestMethod]
    public async Task CallerCancellationSurvivesUpstreamTokenCancellationHandling()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new ScriptedHandler(async (_, ct) => { entered.SetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, ct); return Json("{}"); });
        await using var provider = Build(handler);
        using var cancellation = new CancellationTokenSource();
        var read = provider.GetRequiredService<IBusinessDirectory>().GetCompaniesAsync(cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await cancellation.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => read);
        Assert.AreEqual(1, handler.Requests.Count);
    }

    [TestMethod]
    public async Task CancellationDuringDataReadIsPreservedAndTransportRemainsUsable()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reads = 0;
        var handler = new ScriptedHandler(async (request, ct) =>
        {
            if (IsLogin(request)) return Json("""{"data":{"token":"opaque-token"}}""");
            if (++reads == 1) { entered.SetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, ct); }
            return Json("""{"data":{"companies":[]}}""");
        });
        await using var provider = Build(handler);
        using var cancellation = new CancellationTokenSource();
        var directory = provider.GetRequiredService<IBusinessDirectory>();
        var read = directory.GetCompaniesAsync(cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await cancellation.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => read);
        Assert.AreEqual(0, (await directory.GetCompaniesAsync()).Length);
        Assert.AreEqual(1, handler.Requests.Count(IsLogin));
    }

    [TestMethod]
    public async Task TransportFailureIsSafeAndDoesNotPretendNoCompaniesExist()
    {
        var handler = new ScriptedHandler((_, _) => throw new HttpRequestException("private-server-secret"));
        await using var provider = Build(handler);
        var error = await Assert.ThrowsExactlyAsync<ProAdapterException>(() => provider.GetRequiredService<IBusinessDirectory>().GetCompaniesAsync());
        Assert.IsFalse(error.ToString().Contains("private-server-secret", StringComparison.Ordinal));
        Assert.AreEqual(1, handler.Requests.Count);
    }

    [TestMethod]
    public async Task ParallelCompaniesAreScopedAndShareOnlySerializedTokenRenewal()
    {
        var logins = 0;
        var handler = new ScriptedHandler(async (request, ct) =>
        {
            await Task.Delay(5, ct);
            if (IsLogin(request)) { Interlocked.Increment(ref logins); return Json("""{"data":{"token":"opaque-token"}}"""); }
            var id = request.Headers["CompanyId"];
            return Json(JsonSerializer.Serialize(new { data = new { company = new { companyName1 = "Company " + id } } }));
        });
        await using var provider = Build(handler);
        var session = provider.GetRequiredService<IProApiSession>();
        var tasks = Enumerable.Range(1, 8).Select(id => session.ReadCompanyAsync(
            new CompanyKey(LexwareProductLine.Pro, "isolated-installation", id.ToString()),
            (client, token, ct) => client.GetCompanyAsync(token, ct), CancellationToken.None)).ToArray();
        var results = await Task.WhenAll(tasks);
        CollectionAssert.AreEqual(Enumerable.Range(1, 8).Select(id => "Company " + id).ToArray(), results.Select(result => result.CompanyName1).ToArray());
        Assert.AreEqual(1, logins);
        Assert.AreEqual(1, handler.MaximumConcurrent);
    }

    [TestMethod]
    public async Task ForeignProductOrInstallationIsRejectedBeforeAuthentication()
    {
        var handler = DefaultHandler("{}");
        await using var provider = Build(handler);
        var session = provider.GetRequiredService<IProApiSession>();
        foreach (var key in new[] { new CompanyKey(LexwareProductLine.FinOffice, "isolated-installation", "1"), new CompanyKey(LexwareProductLine.Pro, "other", "1") })
        {
            var error = await Assert.ThrowsExactlyAsync<ProAdapterException>(() => session.ReadCompanyAsync(key,
                (client, token, ct) => client.GetCompanyAsync(token, ct), CancellationToken.None));
            Assert.AreEqual(ProFailure.ForeignCompany, error.Failure);
        }
        Assert.AreEqual(0, handler.Requests.Count);
    }

    [TestMethod]
    public async Task CachedTokenIsReusedAndRenewedAfterLocalMaximumAge()
    {
        var clock = new MutableClock(); var handler = DefaultHandler("""{"data":{"companies":[]}}""");
        await using var provider = Build(handler, clock: clock);
        var directory = provider.GetRequiredService<IBusinessDirectory>();
        await directory.GetCompaniesAsync(); await directory.GetCompaniesAsync();
        Assert.AreEqual(1, handler.Requests.Count(IsLogin));
        clock.Now = clock.Now.AddMinutes(6);
        await directory.GetCompaniesAsync();
        Assert.AreEqual(2, handler.Requests.Count(IsLogin));
    }

    [TestMethod]
    public async Task MissingClientIdentityDoesNotAuthenticateAndConstructionDoesNoIo()
    {
        var handler = DefaultHandler("{}");
        await using var provider = Build(handler, options: new ProConnectionOptions { ApiUrl = ApiUrl });
        var directory = provider.GetRequiredService<IBusinessDirectory>();
        Assert.AreEqual(0, handler.Requests.Count);
        var error = await Assert.ThrowsExactlyAsync<ProAdapterException>(() => directory.GetCompaniesAsync());
        Assert.AreEqual(ProFailure.ConfigurationRequired, error.Failure);
        Assert.AreEqual(0, handler.Requests.Count);
    }

    [TestMethod]
    public async Task SessionDisposalAwaitsReadAndDisposesPrivateTransport()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new ScriptedHandler(async (request, _) =>
        {
            if (IsLogin(request)) return Json("""{"data":{"token":"token"}}""");
            entered.SetResult(); await finish.Task;
            return Json("""{"data":{"companies":[]}}""");
        });
        await using var provider = Build(handler);
        var session = provider.GetRequiredService<IProApiSession>();
        var read = session.GetCompaniesAsync(CancellationToken.None);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var disposal = session.DisposeAsync().AsTask();
        Assert.IsFalse(disposal.IsCompleted); Assert.IsFalse(handler.Disposed);
        finish.SetResult(); await read; await disposal;
        Assert.IsTrue(handler.Disposed);
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => session.GetCompaniesAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task PropertyStoreRejectsMalformedJsonRatherThanDroppingACompany()
    {
        var store = CreateStore();
        File.WriteAllText(Path.Combine(store, "Company1.ps"), """<ps xmlns:ps="urn:haufe-lexware-de:ps"><Company><ps:data><value>broken-json</value></ps:data></Company></ps>""");
        var error = await Assert.ThrowsExactlyAsync<ProAdapterException>(() => new ProPropertyStore().ReadNamesAsync(store, CancellationToken.None));
        Assert.AreEqual(ProFailure.PropertyStoreInvalid, error.Failure);
    }

    [TestMethod]
    public async Task MissingAndEmptyPropertyStoresDoNotInventNames()
    {
        var reader = new ProPropertyStore();
        Assert.AreEqual(0, (await reader.ReadNamesAsync(null, CancellationToken.None)).Count);
        Assert.AreEqual(0, (await reader.ReadNamesAsync(Path.Combine(_root, "missing"), CancellationToken.None)).Count);
        Assert.AreEqual(0, (await reader.ReadNamesAsync(CreateStore(), CancellationToken.None)).Count);
    }

    [TestMethod]
    public void RegistryInstallPathPrecedesProgramPathAndIdentityIgnoresCompanyNames()
    {
        var install = Path.Combine(_root, "install"); var program = Path.Combine(_root, "program");
        Directory.CreateDirectory(install); Directory.CreateDirectory(program);
        File.WriteAllText(Path.Combine(install, "appsettings.json"), """{"Urls":"http://+:9710;https://localhost:9711"}""");
        File.WriteAllText(Path.Combine(program, "appsettings.json"), """{"Urls":"http://localhost:9999"}""");
        var registry = new FakeRegistry(new ProRegistryPaths(_root, install, program));
        var detected = new ProInstallationSource(registry, new ProConnectionOptions()).Detect();
        Assert.AreEqual("http://localhost:9710/", detected.ApiUrl.AbsoluteUri);
        Assert.AreEqual(Path.Combine(_root, "PropertyStore"), detected.PropertyStorePath);
        Assert.AreEqual(detected.Id, new ProInstallationSource(registry, new ProConnectionOptions { ApiUrl = new Uri("http://127.0.0.1:9710/") }).Detect().Id);
        Assert.AreNotEqual(detected.Id, new ProInstallationSource(registry, new ProConnectionOptions { ApiUrl = new Uri("http://localhost:9711/") }).Detect().Id);
    }

    [TestMethod]
    public void MissingInstallationHasNoInventedLocalhostFallback()
    {
        var error = Assert.ThrowsExactly<ProAdapterException>(() => new ProInstallationSource(new FakeRegistry(new(null, null, null)), new ProConnectionOptions()).Detect());
        Assert.AreEqual(ProFailure.InstallationUnavailable, error.Failure);
    }

    private const string PartyCustomer = """
        {"id":1,"number":"00042","matchcode":"fallback","inactive":1,"modifiedDate":"2026-11-06T12:00:00",
         "invoiceAddress":{"company":"Kunde Ä","email":"customer@example.invalid"},"shippingAddress":null,"additionalAddresses":[],
         "contactPersons":[{"id":7,"firstName":"Ada","lastName":"Kundenkontakt","email1":"ada@example.invalid"}]}
        """;
    private const string PartySupplier = """
        {"id":1,"number":"00042","invoiceAddress":{"firstName":"Ben","lastName":"Lieferant"},"shippingAddress":null,"orderAddress":null,
         "contactPersons":[{"id":7,"firstName":"Ben","lastName":"Lieferantenkontakt","email1":"ben@example.invalid"}]}
        """;

    [TestMethod]
    public async Task PartySnapshotsKeepNativeIdsRolesOwnersAndSourcesSeparate()
    {
        var handler = PartyHandler();
        await using var provider = Build(handler);
        var directory = provider.GetRequiredService<IBusinessDirectory>();
        var company = new CompanyKey(LexwareProductLine.Pro, "isolated-installation", "1");
        var page = await directory.GetPartiesAsync(new PartyQuery(company, new PageRequest()));
        Assert.AreEqual(company, page.Company);
        Assert.HasCount(4, page.Page.Items);
        Assert.IsFalse(page.Page.HasMore);
        Assert.HasCount(4, page.Page.Items.Select(party => party.Key).Distinct().ToArray());
        var customer = page.Page.Items.Single(party => party.Key.Kind == PartyKind.Customer);
        Assert.AreEqual("1", customer.Key.ExternalId);
        Assert.AreEqual("00042", customer.Number);
        Assert.AreEqual(new SourceRecord("fk_kunde", "1"), customer.Source);
        Assert.AreEqual("Kunde Ä", customer.DisplayName);
        Assert.IsTrue(customer.IsInactive);
        Assert.IsNull(customer.SourceUpdatedAtUtc);
        var supplier = page.Page.Items.Single(party => party.Key.Kind == PartyKind.Supplier);
        Assert.AreEqual(new SourceRecord("fk_lieferant", "1"), supplier.Source);
        Assert.AreEqual("Ben Lieferant", supplier.DisplayName);
        foreach (var contact in page.Page.Items.Where(party => party.Key.OwnerExternalId is not null))
        {
            Assert.AreEqual("7", contact.Key.ExternalId);
            Assert.AreEqual("1", contact.Key.OwnerExternalId);
            Assert.AreEqual(new SourceRecord("fk_kontaktperson", "7"), contact.Source);
        }
        Assert.IsTrue(handler.Requests.Where(request => !IsLogin(request)).All(request => request.Headers["CompanyId"] == "1"));
        Assert.IsFalse(handler.Requests.Any(request => request.Query.Contains("bankAccount", StringComparison.Ordinal)));
    }

    [TestMethod]
    [DataRow(PartyKind.Customer)]
    [DataRow(PartyKind.Supplier)]
    [DataRow(PartyKind.CustomerContact)]
    [DataRow(PartyKind.SupplierContact)]
    public async Task PartyKindQueriesReadOnlyTheRequiredOwnerRole(PartyKind kind)
    {
        var handler = PartyHandler();
        await using var provider = Build(handler);
        var page = await provider.GetRequiredService<IBusinessDirectory>().GetPartiesAsync(new PartyQuery(
            new CompanyKey(LexwareProductLine.Pro, "isolated-installation", "2"), new PageRequest(), Kind: kind));
        Assert.HasCount(1, page.Page.Items);
        Assert.AreEqual(kind, page.Page.Items[0].Key.Kind);
        Assert.HasCount(1, handler.Requests.Where(request => !IsLogin(request)).ToArray());
    }

    [TestMethod]
    public async Task PartySearchFindsContactsBeyondTheFirstUnfilteredPageAndPagingIsStable()
    {
        await using var provider = Build(PartyHandler());
        var directory = provider.GetRequiredService<IBusinessDirectory>();
        var company = new CompanyKey(LexwareProductLine.Pro, "isolated-installation", "1");
        var first = await directory.GetPartiesAsync(new PartyQuery(company, new PageRequest(limit: 1)));
        var second = await directory.GetPartiesAsync(new PartyQuery(company, new PageRequest(offset: 1, limit: 1)));
        Assert.IsTrue(first.Page.HasMore);
        Assert.AreEqual(PartyKind.Customer, first.Page.Items.Single().Key.Kind);
        Assert.AreEqual(PartyKind.Supplier, second.Page.Items.Single().Key.Kind);
        var found = await directory.GetPartiesAsync(new PartyQuery(company, new PageRequest(limit: 1), "BEN@EXAMPLE.INVALID"));
        Assert.AreEqual(PartyKind.SupplierContact, found.Page.Items.Single().Key.Kind);
        Assert.IsFalse(found.Page.HasMore);
    }

    [TestMethod]
    public async Task PartyDetailsResolveAnExactContactOwnerAndConfirmedNotFound()
    {
        await using var provider = Build(PartyHandler());
        var directory = provider.GetRequiredService<IBusinessDirectory>();
        var company = new CompanyKey(LexwareProductLine.Pro, "isolated-installation", "1");
        var key = new PartyKey(company, PartyKind.SupplierContact, "7", "1");
        Assert.AreEqual(key, (await directory.GetPartyAsync(key))!.Key);
        Assert.IsNull(await directory.GetPartyAsync(new PartyKey(company, PartyKind.SupplierContact, "8", "1")));
        Assert.IsNull(await directory.GetPartyAsync(new PartyKey(company, PartyKind.Customer, "99")));
        await Assert.ThrowsExactlyAsync<ProAdapterException>(() => directory.GetPartyAsync(new PartyKey(company, PartyKind.Customer, "01")));
    }

    [TestMethod]
    public async Task TheSameContactIdOnDifferentCustomersKeepsBothOwnerKeys()
    {
        var second = PartyCustomer.Replace("\"id\":1,", "\"id\":2,", StringComparison.Ordinal);
        await using var provider = Build(DefaultHandler("{\"data\":{\"parties\":[" + second + "," + PartyCustomer + "]}}"));
        var page = await provider.GetRequiredService<IBusinessDirectory>().GetPartiesAsync(new PartyQuery(
            new CompanyKey(LexwareProductLine.Pro, "isolated-installation", "1"), new PageRequest(), Kind: PartyKind.CustomerContact));
        Assert.HasCount(2, page.Page.Items);
        CollectionAssert.AreEqual(new[] { "1", "2" }, page.Page.Items.Select(party => party.Key.OwnerExternalId).ToArray());
        Assert.IsTrue(page.Page.Items.All(party => party.Key.ExternalId == "7"));
        Assert.AreNotEqual(page.Page.Items[0].Key, page.Page.Items[1].Key);
    }

    [TestMethod]
    [DataRow("{\"data\":{}}")]
    [DataRow("{\"data\":{\"parties\":null}}")]
    [DataRow("{\"data\":{\"parties\":[{\"id\":1,\"number\":\"0001\",\"invoiceAddress\":null,\"shippingAddress\":null,\"additionalAddresses\":[]}]}}")]
    [DataRow("{\"data\":{\"parties\":[{\"id\":0,\"number\":\"0001\",\"invoiceAddress\":null,\"shippingAddress\":null,\"additionalAddresses\":[],\"contactPersons\":[]}]}}")]
    [DataRow("{\"data\":{\"parties\":[{\"id\":1,\"number\":null,\"invoiceAddress\":null,\"shippingAddress\":null,\"additionalAddresses\":[],\"contactPersons\":[]}]}}")]
    [DataRow("{\"data\":{\"parties\":[{\"id\":1,\"number\":\"1\",\"invoiceAddress\":null,\"shippingAddress\":null,\"additionalAddresses\":[],\"contactPersons\":[{\"id\":0}]}]}}")]
    [DataRow("{\"data\":{\"parties\":[null]}}")]
    [DataRow("{\"data\":{\"parties\":[]},\"errors\":[{\"message\":\"private-source-value\"}]}")]
    public async Task IncompletePartyResponsesNeverBecomeSuccessfulEmptySnapshots(string response)
    {
        await using var provider = Build(DefaultHandler(response));
        var error = await Assert.ThrowsExactlyAsync<ProAdapterException>(() => provider.GetRequiredService<IBusinessDirectory>().GetPartiesAsync(
            new PartyQuery(new CompanyKey(LexwareProductLine.Pro, "isolated-installation", "1"), new PageRequest(), Kind: PartyKind.Customer)));
        Assert.AreEqual(ProFailure.InvalidResponse, error.Failure);
        Assert.DoesNotContain("private-source-value", error.ToString());
    }

    [TestMethod]
    public async Task DuplicatePartyOrContactIdsCannotBeSilentlyMerged()
    {
        foreach (var records in new[] { PartyCustomer + "," + PartyCustomer,
            PartyCustomer.Replace("\"id\":7,\"firstName\":\"Ada\",\"lastName\":\"Kundenkontakt\",\"email1\":\"ada@example.invalid\"",
                "\"id\":7},{\"id\":7", StringComparison.Ordinal) })
        {
            await using var provider = Build(DefaultHandler("{\"data\":{\"parties\":[" + records + "]}}"));
            await Assert.ThrowsExactlyAsync<ProAdapterException>(() => provider.GetRequiredService<IBusinessDirectory>().GetPartiesAsync(
                new PartyQuery(new CompanyKey(LexwareProductLine.Pro, "isolated-installation", "1"), new PageRequest(), Kind: PartyKind.Customer)));
        }
    }

    [TestMethod]
    public async Task EmptyPartyListIsSuccessButMissingDetailFieldIsAnError()
    {
        await using (var provider = Build(DefaultHandler("""{"data":{"parties":[]}}""")))
        {
            var page = await provider.GetRequiredService<IBusinessDirectory>().GetPartiesAsync(new PartyQuery(
                new CompanyKey(LexwareProductLine.Pro, "isolated-installation", "1"), new PageRequest(), Kind: PartyKind.Customer));
            Assert.IsEmpty(page.Page.Items);
            Assert.IsFalse(page.Page.HasMore);
        }
        await using var invalid = Build(DefaultHandler("""{"data":{}}"""));
        await Assert.ThrowsExactlyAsync<ProAdapterException>(() => invalid.GetRequiredService<IBusinessDirectory>()
            .GetPartyAsync(new PartyKey(new CompanyKey(LexwareProductLine.Pro, "isolated-installation", "1"), PartyKind.Customer, "1")));
    }

    [TestMethod]
    public async Task PartyReadsObserveCancellationAndCannotCrossProductOrInstallation()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new ScriptedHandler(async (request, token) =>
        {
            if (IsLogin(request)) return Json("""{"data":{"token":"opaque-token"}}""");
            entered.SetResult();
            await Task.Delay(Timeout.Infinite, token);
            throw new InvalidOperationException();
        });
        await using var provider = Build(handler);
        var directory = provider.GetRequiredService<IBusinessDirectory>();
        foreach (var company in new[] { new CompanyKey(LexwareProductLine.FinOffice, "isolated-installation", "1"), new CompanyKey(LexwareProductLine.Pro, "foreign", "1") })
            await Assert.ThrowsExactlyAsync<ProAdapterException>(() => directory.GetPartiesAsync(new PartyQuery(company, new PageRequest())));
        Assert.IsEmpty(handler.Requests);
        using var cancellation = new CancellationTokenSource();
        var read = directory.GetPartiesAsync(new PartyQuery(new CompanyKey(LexwareProductLine.Pro, "isolated-installation", "1"), new PageRequest()), cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => read);
    }

    [TestMethod]
    public async Task SwitchingDuringAnApiReadDropsTheOldResultAndLoadsTheNewCompanyScope()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new ScriptedHandler(async (request, _) =>
        {
            if (IsLogin(request)) return Json("""{"data":{"token":"opaque-token"}}""");
            var company = request.Headers["CompanyId"];
            if (company == "1")
            {
                entered.TrySetResult();
                await release.Task; // Deliberately ignore HTTP cancellation.
            }
            return Json("{\"data\":{\"parties\":[" + PartyCustomer.Replace("Kunde Ä", "Company " + company, StringComparison.Ordinal) + "]}}");
        });
        await using var provider = Build(handler);
        await using var context = new CompanyContext();
        var first = new Company(new CompanyKey(LexwareProductLine.Pro, "isolated-installation", "1"), "First");
        await context.SelectAsync(first);
        using var old = ((ICompanyContext)context).BeginOperation();
        var directory = provider.GetRequiredService<IBusinessDirectory>();
        Task<PartyPage> Read(CompanyKey company, CancellationToken token) =>
            directory.GetPartiesAsync(new PartyQuery(company, new PageRequest(), Kind: PartyKind.Customer), token);
        PartyPage? bound = null;
        var oldRead = old.LoadAndPublishAsync(new InlineUiDispatcher(), Read, _ => Assert.Fail("Stale party data."), _ => Assert.Fail("Stale error view."));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task<bool>? currentRead = null;
        var second = first with { Key = new CompanyKey(LexwareProductLine.Pro, "isolated-installation", "2") };
        await context.SelectAsync(second);
        using var current = ((ICompanyContext)context).BeginOperation();
        try
        {
            currentRead = current.LoadAndPublishAsync(new InlineUiDispatcher(), Read, page => bound = page);
            Assert.IsFalse(oldRead.IsCompleted);
            Assert.IsFalse(currentRead.IsCompleted);
        }
        finally { release.TrySetResult(); }

        await Assert.ThrowsAsync<OperationCanceledException>(() => oldRead);
        Assert.IsTrue(await currentRead!);
        Assert.AreEqual(second.Key, bound!.Company);
        Assert.AreEqual("Company 2", bound.Page.Items.Single().DisplayName);
        Assert.AreEqual(1, handler.MaximumConcurrent);
    }

    private const string AddressCustomer = """
        {"id":1,"number":"00042","invoiceAddress":{"company":"Kunde","street":"Hauptstraße","houseNumber":"3a",
          "extension":"Haus B, 2. OG","postalcode":"01234","city":"Dresden","country":"Deutschland",
          "telephone":" +49 (0)351 / 12-34 ","telephone2":"0351/44","telephone3":" ","cellphone":"0170 123 45","fax":"0351/99"},
         "shippingAddress":{"street":"Lieferweg","houseNumber":"5","postalcode":"00123","country":"de","telephone":"0351/55"},
         "additionalAddresses":[{"id":101,"businessPartnerId":1,"company":"Lager","country":"AT","telephone":"0043 1 234","cellphone":"0043 1 234"}],
         "contactPersons":[{"id":7,"firstName":"Ada","phone1":"030/1","phone2":"030/2","mobil":"0171-1","fax":"030/9",
           "additionalAddressId":101,"contactPersonAddressType":"ADDITIONAL_ADDRESS"}]}
        """;

    [TestMethod]
    public async Task CustomerAddressesKeepOriginalNumbersPostalFieldsAndExactSourceLocations()
    {
        await using var provider = Build(AddressHandler(AddressCustomer));
        var directory = provider.GetRequiredService<IBusinessDirectory>();
        var company = new CompanyKey(LexwareProductLine.Pro, "isolated-installation", "1");
        var page = await directory.GetPartiesAsync(new PartyQuery(company, new PageRequest(), Kind: PartyKind.Customer));
        var customer = page.Page.Items.Single();
        Assert.HasCount(3, customer.Addresses);
        var billing = customer.Addresses.Single(address => address.Kind == AddressKind.Billing);
        Assert.AreEqual("Hauptstraße", billing.Street);
        Assert.AreEqual("3a", billing.HouseNumber);
        Assert.AreEqual("Haus B, 2. OG", billing.AddressLine2);
        Assert.AreEqual("01234", billing.PostalCode);
        Assert.AreEqual("Deutschland", billing.Country);
        Assert.IsNull(billing.CountryCode);
        Assert.AreEqual(new SourceField("fk_kunde", "1", "invoiceAddress"), billing.Source);
        Assert.HasCount(4, billing.Phones);
        Assert.AreEqual(" +49 (0)351 / 12-34 ", billing.Phones[0].OriginalValue);
        Assert.AreEqual(new SourceField("fk_kunde", "1", "invoiceAddress.telephone"), billing.Phones[0].Source);
        Assert.AreEqual(PhoneKind.Mobile, billing.Phones[2].Kind);
        Assert.IsTrue(billing.Phones.All(phone => phone.E164 is null && phone.Extension is null));
        var shipping = customer.Addresses.Single(address => address.Kind == AddressKind.Shipping);
        Assert.AreEqual("00123", shipping.PostalCode);
        Assert.AreEqual("DE", shipping.CountryCode);
        Assert.AreEqual("de", shipping.Country);
        var additional = customer.Addresses.Single(address => address.Kind == AddressKind.Additional);
        Assert.AreEqual("101", additional.ExternalId);
        Assert.AreEqual(new SourceField("fk_anschrift", "101", "additionalAddresses"), additional.Source);
        Assert.HasCount(2, additional.Phones);
        Assert.AreEqual(additional.Phones[0].OriginalValue, additional.Phones[1].OriginalValue);
        Assert.AreNotEqual(additional.Phones[0].Source, additional.Phones[1].Source);
        Assert.IsEmpty(customer.Phones);
        var detail = await directory.GetPartyAsync(customer.Key);
        Assert.AreEqual(billing.Source, detail!.Addresses[0].Source);
        Assert.AreEqual(billing.Phones[0], detail.Addresses[0].Phones[0]);
    }

    [TestMethod]
    public async Task ContactsKeepTheirOwnNumbersAndOnlyExplicitlyReferencedPostalAddresses()
    {
        await using var provider = Build(AddressHandler(AddressCustomer));
        var contact = (await provider.GetRequiredService<IBusinessDirectory>().GetPartiesAsync(new PartyQuery(
            new CompanyKey(LexwareProductLine.Pro, "isolated-installation", "1"), new PageRequest(), Kind: PartyKind.CustomerContact))).Page.Items.Single();
        Assert.HasCount(4, contact.Phones);
        Assert.AreEqual(new SourceField("fk_kontaktperson", "7", "mobil"), contact.Phones.Single(phone => phone.Kind == PhoneKind.Mobile).Source);
        Assert.AreEqual("0171-1", contact.Phones.Single(phone => phone.Kind == PhoneKind.Mobile).OriginalValue);
        var address = contact.Addresses.Single();
        Assert.AreEqual(AddressKind.Contact, address.Kind);
        Assert.AreEqual("101", address.ExternalId);
        Assert.IsEmpty(address.Phones);
        Assert.AreEqual("Lager", address.CompanyName);
    }

    [TestMethod]
    public async Task SupplierOrderAddressIsAnAdditionalLocationAndAbsentShippingDoesNotBecomeAPhantomAddress()
    {
        const string supplier = """
            {"id":1,"number":"00042","invoiceAddress":{"company":"Lieferant","telephone":"040/1"},
             "shippingAddress":{"exists":false,"telephone":"must-not-be-used"},
             "orderAddress":{"street":"Bestellweg","telephone2":"040/2"},"contactPersons":[]}
            """;
        var handler = AddressHandler(supplier);
        await using var provider = Build(handler);
        var party = (await provider.GetRequiredService<IBusinessDirectory>().GetPartiesAsync(new PartyQuery(
            new CompanyKey(LexwareProductLine.Pro, "isolated-installation", "1"), new PageRequest(), Kind: PartyKind.Supplier))).Page.Items.Single();
        Assert.HasCount(2, party.Addresses);
        var order = party.Addresses.Single(address => address.Kind == AddressKind.Additional);
        Assert.AreEqual(new SourceField("fk_lieferant", "1", "orderAddress"), order.Source);
        Assert.AreEqual("040/2", order.Phones.Single().OriginalValue);
        Assert.IsFalse(party.Addresses.SelectMany(address => address.Phones).Any(phone => phone.OriginalValue == "must-not-be-used"));
        var query = handler.Requests.Last().Query;
        Assert.IsFalse(query.Contains("cellphone", StringComparison.Ordinal));
        Assert.IsFalse(query.Contains("additionalAddresses {", StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow("foreign-owner")]
    [DataRow("missing-id")]
    [DataRow("duplicate")]
    [DataRow("null-list")]
    [DataRow("missing-field")]
    [DataRow("unknown-contact-address")]
    public async Task InvalidAddressOwnershipOrIncompleteCollectionsFailInsteadOfDroppingData(string fault)
    {
        var record = fault switch
        {
            "foreign-owner" => AddressCustomer.Replace("\"businessPartnerId\":1", "\"businessPartnerId\":2", StringComparison.Ordinal),
            "missing-id" => AddressCustomer.Replace("\"id\":101,", "\"id\":0,", StringComparison.Ordinal),
            "duplicate" => AddressCustomer.Replace("\"additionalAddresses\":[", "\"additionalAddresses\":[{\"id\":101},", StringComparison.Ordinal),
            "null-list" => PartyCustomer.Replace("\"additionalAddresses\":[]", "\"additionalAddresses\":null", StringComparison.Ordinal),
            "missing-field" => PartyCustomer.Replace("\"additionalAddresses\":[],", "", StringComparison.Ordinal),
            _ => AddressCustomer.Replace("\"additionalAddressId\":101", "\"additionalAddressId\":999", StringComparison.Ordinal)
        };
        await using var provider = Build(AddressHandler(record));
        var error = await Assert.ThrowsExactlyAsync<ProAdapterException>(() => provider.GetRequiredService<IBusinessDirectory>().GetPartiesAsync(
            new PartyQuery(new CompanyKey(LexwareProductLine.Pro, "isolated-installation", "1"), new PageRequest())));
        Assert.AreEqual(ProFailure.InvalidResponse, error.Failure);
    }

    private static ScriptedHandler AddressHandler(string record) => new((request, _) => Task.FromResult(IsLogin(request)
        ? Json("""{"data":{"token":"opaque-token"}}""")
        : Json(request.Query.Contains("party:", StringComparison.Ordinal) ? "{\"data\":{\"party\":" + record + "}}" : "{\"data\":{\"parties\":[" + record + "]}}")));

    private static ScriptedHandler PartyHandler() => new((request, _) =>
    {
        if (IsLogin(request)) return Task.FromResult(Json("""{"data":{"token":"opaque-token"}}"""));
        var supplier = request.Query.Contains("supplier", StringComparison.Ordinal);
        if (request.Query.Contains("party:", StringComparison.Ordinal))
            return Task.FromResult(Json("{\"data\":{\"party\":" + (request.Query.Contains("id: 99", StringComparison.Ordinal) ? "null" : supplier ? PartySupplier : PartyCustomer) + "}}"));
        return Task.FromResult(Json("{\"data\":{\"parties\":[" + (supplier ? PartySupplier : PartyCustomer) + "]}}"));
    });

    private static ServiceProvider Build(ScriptedHandler handler, string? store = null, MutableClock? clock = null, ProConnectionOptions? options = null)
    {
        var services = new ServiceCollection();
        new ProPluginServices().ConfigureServices(services);
        services.AddSingleton(options ?? Credentials);
        services.AddSingleton<TimeProvider>(clock ?? new MutableClock());
        services.AddSingleton<IProRegistry>(new FakeRegistry(new(store is null ? null : Path.GetDirectoryName(store), null, null)));
        services.AddSingleton<IProHttpHandlerFactory>(new HandlerFactory(handler));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    private string CreateStore() { var path = Path.Combine(_root, "PropertyStore"); Directory.CreateDirectory(path); return path; }
    private static void WriteCompany(string store, string id, string name) => File.WriteAllText(Path.Combine(store, "Company" + id + ".ps"),
        "<ps xmlns:ps=\"urn:haufe-lexware-de:ps\"><Company><ps:data><value>" + System.Security.SecurityElement.Escape(JsonSerializer.Serialize(new { name })) + "</value></ps:data></Company></ps>");
    private static bool IsLogin(CapturedRequest request) => request.Query.Contains("token(", StringComparison.Ordinal);
    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    private static ScriptedHandler DefaultHandler(string body) => new((request, _) => Task.FromResult(IsLogin(request) ? Json("""{"data":{"token":"opaque-token"}}""") : Json(body)));

    private sealed class FakeRegistry(ProRegistryPaths paths) : IProRegistry { public ProRegistryPaths ReadPaths() => paths; }
    private sealed class HandlerFactory(ScriptedHandler handler) : IProHttpHandlerFactory { public HttpMessageHandler Create() => handler; }
    private sealed class MutableClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed record CapturedRequest(string Query, Dictionary<string, string> Headers);
    private sealed class ScriptedHandler(Func<CapturedRequest, CancellationToken, Task<HttpResponseMessage>> script) : HttpMessageHandler
    {
        private int _active;
        public ConcurrentQueue<CapturedRequest> Requests { get; } = new();
        public int MaximumConcurrent { get; private set; }
        public bool Disposed { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var active = Interlocked.Increment(ref _active); MaximumConcurrent = Math.Max(active, MaximumConcurrent);
            try
            {
                using var document = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
                var captured = new CapturedRequest(document.RootElement.GetProperty("query").GetString()!, request.Headers.ToDictionary(header => header.Key, header => string.Join(",", header.Value), StringComparer.OrdinalIgnoreCase));
                Requests.Enqueue(captured);
                return await script(captured, cancellationToken);
            }
            finally { Interlocked.Decrement(ref _active); }
        }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }
}

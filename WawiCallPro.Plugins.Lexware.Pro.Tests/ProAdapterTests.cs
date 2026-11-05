using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;

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

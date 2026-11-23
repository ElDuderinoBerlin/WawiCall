using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Quantarix.License.Client;
using Quantarix.License.Client.Keys;
using Quantarix.License.Client.Machine;
using Quantarix.License.Client.Protection;
using Quantarix.License.Client.Validation;
using Quantarix.License.Core.Models;
using Quantarix.License.Core.Signing;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Diagnostics;
using WawiCallPro.Utilities.Hosting;
using WawiCallPro.Utilities.Licensing;
using WawiCallPro.Utilities.Operations;

namespace WawiCallPro.Utilities.Tests;

[TestClass]
public sealed class ProductLicenseTests
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WawiCallPro-license-tests-" + Guid.NewGuid().ToString("N"));
    private readonly RSA _signer = RSA.Create(2048);
    private readonly Guid _licenseId = Guid.NewGuid();
    private readonly Clock _clock = new();
    private readonly LicenseProductIdentity _product = new("WAWICALLPRO_PRO", new Guid(LicenseProductIdentity.ProApplicationId), "0.1.0", true);
    private ApplicationPaths? _paths;
    private ApplicationPaths Paths => _paths ??= new(new ConfigurationLocation(_root));
    [TestCleanup]
    public void Cleanup() { _signer.Dispose(); if (Directory.Exists(_root)) Directory.Delete(_root, true); }

    [TestMethod]
    public async Task SignedActivationIsProductBoundAndPersistedWithCurrentUserDpapi()
    {
        var handler = new Handler((_, _) => Task.FromResult(ActivationResponse(CreateXml())));
        var license = CreateService(handler);
        Assert.AreEqual(LicenseStatus.Missing, (await license.ValidateAsync(CancellationToken.None)).Status);
        var activated = await license.ActivateAsync("SYNTHETIC-TEST-KEY", CancellationToken.None);
        Assert.AreEqual(LicenseStatus.Valid, activated.Status);
        Assert.AreEqual(_licenseId, activated.LicenseId);
        Assert.AreEqual("SYNTHETIC-TEST-KEY", await CreateService(handler).GetStoredLicenseNumberAsync(_licenseId, CancellationToken.None));
        Assert.IsNull(await license.GetStoredLicenseNumberAsync(Guid.NewGuid(), CancellationToken.None));
        var authentication = await license.GetCrashAuthenticationAsync(CancellationToken.None);
        Assert.AreEqual("synthetic-activation-secret", authentication!.ActivationSecret);
        Assert.DoesNotContain(authentication.ActivationSecret, authentication.ToString());
        foreach (var file in Directory.GetFiles(Path.Combine(_root, "license")))
        {
            var bytes = Encoding.UTF8.GetString(File.ReadAllBytes(file));
            Assert.DoesNotContain("synthetic-activation-secret", bytes);
            Assert.DoesNotContain("SYNTHETIC-TEST-KEY", bytes);
            Assert.DoesNotContain("<License", bytes);
        }
        using var sent = JsonDocument.Parse(handler.Bodies.Single());
        Assert.AreEqual(_product.ApplicationGuid.ToString(), sent.RootElement.GetProperty("applicationGuid").GetString());
    }

    [TestMethod]
    public void DemoTrialSurvivesOneMissingCopyAndRejectsRollbackOrExpiry()
    {
        var store = new TrialStore();
        var trial = new DemoTrialService(store, new DpapiLocalLicenseProtector(), new Machine(), _product, _clock);
        var started = _clock.Now;
        var first = trial.Check();
        Assert.AreEqual(30, first.Trial!.RemainingDays);
        Assert.AreEqual(LicenseStatus.Valid, first.Status);
        store.Copies.RemoveAt(0);
        _clock.Now = started.AddDays(3);
        var reopened = new DemoTrialService(store, new DpapiLocalLicenseProtector(), new Machine(), _product, _clock).Check();
        Assert.AreEqual(started, reopened.Trial!.StartUtc);
        Assert.AreEqual(27, reopened.Trial.RemainingDays);
        _clock.Now = started;
        Assert.AreEqual(LicenseStatus.DemoInvalid, trial.Check().Status);
        _clock.Now = started.AddDays(30);
        Assert.AreEqual(LicenseStatus.DemoExpired, trial.Check().Status);
    }

    [TestMethod]
    public void DemoTrialRejectsTamperedOrForeignProductState()
    {
        var store = new TrialStore();
        var protector = new DpapiLocalLicenseProtector();
        var trial = new DemoTrialService(store, protector, new Machine(), _product, _clock);
        Assert.AreEqual(LicenseStatus.Valid, trial.Check().Status);
        var foreign = new DemoTrialService(store, protector, new Machine(),
            _product with { ProductCode = "WAWICALLPRO_FINOFFICE" }, _clock);
        Assert.AreEqual(LicenseStatus.DemoInvalid, foreign.Check().Status);
        store.Copies[0] = [1, 2, 3];
        Assert.AreEqual(LicenseStatus.DemoInvalid, trial.Check().Status);
    }

    [TestMethod]
    [DataRow("product")]
    [DataRow("application")]
    [DataRow("machine")]
    [DataRow("signature")]
    [DataRow("expired")]
    public async Task WrongIdentitySignatureOrValidityCannotReplaceAnExistingLicense(string mismatch)
    {
        var xml = CreateXml();
        var handler = new Handler((_, _) => Task.FromResult(ActivationResponse(xml)));
        var license = CreateService(handler);
        await license.ActivateAsync("SYNTHETIC", CancellationToken.None);
        var file = Path.Combine(_root, "license", "license.xlic");
        var original = SHA256.HashData(File.ReadAllBytes(file));
        xml = CreateXml(mismatch);
        await Assert.ThrowsExactlyAsync<LicenseAccessException>(() => license.ActivateAsync("SYNTHETIC", CancellationToken.None));
        CollectionAssert.AreEqual(original, SHA256.HashData(File.ReadAllBytes(file)));
        Assert.AreEqual(LicenseStatus.Valid, (await license.ValidateAsync(CancellationToken.None)).Status);
    }

    [TestMethod]
    public async Task ActualServerBadRequestWithRevocationRemainsBlockedAfterRestart()
    {
        var handler = new Handler((request, _) => Task.FromResult(request.RequestUri!.AbsolutePath == "/activate"
            ? ActivationResponse(CreateXml())
            : Json(HttpStatusCode.BadRequest, """{"success":false,"isValid":false,"problems":[{"code":"activation_revoked","message":"private-server-message"}]}""")));
        var service = CreateService(handler);
        await service.ActivateAsync("SYNTHETIC", CancellationToken.None);
        Assert.AreEqual(LicenseStatus.Revoked, (await service.RefreshAsync(CancellationToken.None)).Status);
        Assert.AreEqual(LicenseStatus.Revoked, (await CreateService(handler).ValidateAsync(CancellationToken.None)).Status);
        Assert.IsNull(await service.GetCrashAuthenticationAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task ServerOutageRetainsSignedLocalLicenseAndClockRollbackIsRejected()
    {
        var handler = new Handler((request, _) => Task.FromResult(request.RequestUri!.AbsolutePath == "/activate"
            ? ActivationResponse(CreateXml()) : Json(HttpStatusCode.ServiceUnavailable, "private-transport-error")));
        var service = CreateService(handler);
        await service.ActivateAsync("SYNTHETIC", CancellationToken.None);
        var error = await Assert.ThrowsExactlyAsync<LicenseAccessException>(() => service.RefreshAsync(CancellationToken.None));
        Assert.AreEqual(LicenseStatus.ServerUnavailable, error.Status);
        Assert.DoesNotContain("private-transport-error", error.ToString());
        Assert.AreEqual(LicenseStatus.Valid, (await service.ValidateAsync(CancellationToken.None)).Status);
        _clock.Now = _clock.Now.AddHours(-1);
        Assert.AreEqual(LicenseStatus.Invalid, (await service.ValidateAsync(CancellationToken.None)).Status);
    }

    [TestMethod]
    public async Task OfflineLimitRequiresOnlineRefreshAndCannotBeExtendedByAnUnsignedLocalTimestamp()
    {
        var handler = new Handler((request, _) => Task.FromResult(request.RequestUri!.AbsolutePath == "/activate"
            ? ActivationResponse(CreateXml())
            : Json(HttpStatusCode.OK, JsonSerializer.Serialize(new { success = true, isValid = true, licenseXml = CreateXml() }))));
        var service = CreateService(handler);
        await service.ActivateAsync("SYNTHETIC", CancellationToken.None);
        _clock.Now = _clock.Now.AddDays(3);
        Assert.AreEqual(LicenseStatus.OnlineCheckRequired, (await service.ValidateAsync(CancellationToken.None)).Status);
        Assert.AreEqual(LicenseStatus.Valid, (await service.RefreshAsync(CancellationToken.None)).Status);
    }

    [TestMethod]
    [DataRow(200, true)]
    [DataRow(401, false)]
    [DataRow(503, false)]
    public async Task CrashQueueDeletesOnlyAfterAcknowledgedAuthenticatedUpload(int responseStatus, bool deleted)
    {
        var handler = new Handler((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath == "/activate") return Task.FromResult(ActivationResponse(CreateXml()));
            Assert.AreEqual(_licenseId.ToString(), request.Headers.GetValues("X-LicenseId").Single());
            Assert.AreEqual("machine-test", request.Headers.GetValues("X-MachineIdHash").Single());
            Assert.AreEqual("synthetic-activation-secret", request.Headers.GetValues("X-ActivationSecret").Single());
            return Task.FromResult(Json((HttpStatusCode)responseStatus, "{}"));
        });
        var license = CreateService(handler);
        await license.ActivateAsync("SYNTHETIC", CancellationToken.None);
        var queue = new CrashReportQueue(Paths);
        new CrashReporter(queue, new ApplicationDescriptor("WawiCallPro", "Pro"), new("test", "test"), _clock).Capture(new Exception("password"), CrashOrigin.Process);
        using var uploader = new CrashReportUploader(queue, license, Settings, new HttpFactory(handler), new StartupExecutionMode());
        await uploader.FlushAsync(CancellationToken.None);
        Assert.AreEqual(deleted ? 0 : 1, queue.GetPendingFiles().Length);
        Assert.DoesNotContain("password", handler.Bodies.Last());
    }

    [TestMethod]
    public async Task DiagnosticOrDisabledUploaderNeverCreatesHttpClientAndRetainsQueue()
    {
        var handler = new Handler((_, _) => throw new AssertFailedException("Unexpected network operation."));
        var queue = new CrashReportQueue(Paths);
        new CrashReporter(queue, new ApplicationDescriptor("WawiCallPro", "Pro"), new("test", "test"), _clock).Capture(new Exception(), CrashOrigin.Process);
        using var suppressed = new CrashReportUploader(queue, CreateService(handler), Settings, new HttpFactory(handler), new StartupExecutionMode(true));
        await suppressed.FlushAsync(CancellationToken.None);
        using var disabled = new CrashReportUploader(queue, CreateService(handler), new StartupSettings(), new HttpFactory(handler), new StartupExecutionMode());
        await disabled.FlushAsync(CancellationToken.None);
        Assert.AreEqual(1, queue.GetPendingFiles().Length);
        Assert.AreEqual(0, handler.Bodies.Count);
    }

    [TestMethod]
    public async Task NewCrashWakesUploaderBeforeItsNextScheduledRetry()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var posting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new Handler((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath == "/activate") return Task.FromResult(ActivationResponse(CreateXml()));
            Assert.AreEqual("/api/v1/crash", request.RequestUri.AbsolutePath);
            posting.TrySetResult();
            return Task.FromResult(Json(HttpStatusCode.OK, "{}"));
        });
        var license = CreateService(handler);
        await license.ActivateAsync("SYNTHETIC", timeout.Token);
        var queue = new CrashReportQueue(Paths);
        using var uploader = new CrashReportUploader(queue, license, Settings with { CrashFlushIntervalSeconds = 3600 },
            new HttpFactory(handler), new StartupExecutionMode());
        await uploader.StartAsync(timeout.Token);
        try
        {
            await Task.Delay(50, timeout.Token);
            var report = new CrashReporter(queue, new ApplicationDescriptor("WawiCallPro", "Pro"), new("test", "test"), _clock)
                .Capture(new Exception("synthetic sensitive text"), CrashOrigin.UiThread);
            Assert.IsTrue(report.Stored);
            await posting.Task.WaitAsync(timeout.Token);
        }
        finally { await uploader.StopAsync(timeout.Token); }
        Assert.IsEmpty(queue.GetPendingFiles());
        Assert.IsTrue(handler.Bodies.Skip(1).All(body => !body.Contains("synthetic sensitive text", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void MissingTrustAndUnsafeDeploymentSettingsAreRejectedWithoutUsingForeignProducts()
    {
        Assert.ThrowsExactly<LicenseAccessException>(() => new CompiledLicenseKeys().GetPublicKey("synthetic"));
        Directory.CreateDirectory(_root);
        foreach (var json in new[] { """{"licenseServerUrl":"http://untrusted.invalid"}""", """{"requestTimeoutSeconds":0}""", """{"uploadCrashReports":true}""", """{"password":"secret"}""" })
        {
            File.WriteAllText(Path.Combine(_root, "startup.local.json"), json);
            Assert.ThrowsExactly<LicenseAccessException>(() => StartupSettings.Load(Paths));
        }
    }

    private static StartupSettings Settings { get; } = new() { LicenseServerUrl = new Uri("https://isolated.invalid"), UploadCrashReports = true };
    private ProductLicenseService CreateService(Handler handler)
    {
        var keys = new EmbeddedPublicKeyProvider(new Dictionary<string, string> { ["synthetic"] = _signer.ExportSubjectPublicKeyInfoPem() });
        var protector = new DpapiLocalLicenseProtector();
        var validator = new LicenseFileValidator(keys, machineIdProvider: new Machine(), localLicenseProtector: protector);
        var guard = new LicenseGuard(keys, new DpapiLicenseClockStateStore(protector), validator);
        return new ProductLicenseService(Paths, _product, Settings, validator, guard, protector, new Machine(), new LicenseServerSessionFactory(new HttpFactory(handler)), _clock);
    }
    private string CreateXml(string? mismatch = null)
    {
        var document = new LicenseDocument
        {
            LicenseId = _licenseId, KeyId = "synthetic",
            Product = new LicenseProduct { Code = mismatch == "product" ? "LXTRANSLATE_PRO" : _product.ProductCode,
                ApplicationGuid = mismatch == "application" ? Guid.NewGuid() : _product.ApplicationGuid },
            Activation = new LicenseActivation { Mode = ActivationMode.Online, MachineIdHash = mismatch == "machine" ? "other-machine" : "machine-test" },
            Validity = new LicenseValidity { IssuedAtUtc = _clock.Now, ValidFromUtc = _clock.Now.AddDays(-5),
                ValidToUtc = mismatch == "expired" ? _clock.Now.AddDays(-1) : _clock.Now.AddDays(30), MaxOfflineDays = 2, LastOnlineCheckUtc = _clock.Now }
        };
        if (mismatch == "signature") { using var other = RSA.Create(2048); return new LicenseXmlSigner().Sign(document, other); }
        return new LicenseXmlSigner().Sign(document, _signer);
    }
    private static HttpResponseMessage ActivationResponse(string xml) => Json(HttpStatusCode.OK,
        JsonSerializer.Serialize(new { success = true, licenseXml = xml, activationSecret = "synthetic-activation-secret" }));
    private static HttpResponseMessage Json(HttpStatusCode status, string body) => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    private sealed class Clock : TimeProvider { public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow; public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class Machine : IMachineIdProvider { public MachineIdInfo GetMachineId() => new() { Value = "machine-test", Source = "test" }; public string GetMachineIdHash() => "machine-test"; }
    private sealed class TrialStore : IDemoTrialStateStore
    {
        public List<byte[]> Copies { get; } = [];
        public IReadOnlyList<byte[]> Read() => Copies;
        public bool Save(byte[] bytes)
        {
            Copies.Clear();
            Copies.Add(bytes);
            Copies.Add(bytes);
            return true;
        }
    }
    private sealed class HttpFactory(Handler handler) : IStartupHttpClientFactory { public HttpClient Create() => new(handler, false) { BaseAddress = Settings.LicenseServerUrl }; }
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> execute) : HttpMessageHandler
    {
        public List<string> Bodies { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Bodies.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken));
            return await execute(request, cancellationToken);
        }
    }
}

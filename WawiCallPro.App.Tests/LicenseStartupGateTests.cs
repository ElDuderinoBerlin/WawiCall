using WawiCallPro.Utilities.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WawiCallPro.App.Hosting;
using WawiCallPro.Utilities.Diagnostics;
using WawiCallPro.Utilities.Licensing;
using WawiCallPro.Interfaces.Hosting;

namespace WawiCallPro.App.Tests;

[TestClass]
public sealed class LicenseStartupGateTests
{
    [TestMethod]
    public async Task ComStartupValidatesRequiredLicenseWithoutOpeningActivationDialog()
    {
        var licenses = new LicenseProbe();
        var gate = new LicenseStartupGate(licenses, Product, new StartupSettings { LicenseServerUrl = new Uri("https://isolated.invalid") },
            new StartupExecutionMode { ComActivated = true }, new Prompt());
        var error = await Assert.ThrowsExactlyAsync<LicenseAccessException>(() => gate.StartAsync(CancellationToken.None));
        Assert.AreEqual(LicenseStatus.Missing, error.Status); Assert.AreEqual(1, licenses.Calls);
    }
    [TestMethod]
    public async Task MissingLicenseStopsHostBeforeBusinessWorkersStart()
    {
        var worker = new ProbeWorker();
        using var host = new HostBuilder().ConfigureServices(services =>
        {
            services.AddSingleton<IHostedService>(new LicenseStartupGate(new LicenseProbe(), Product, new StartupSettings(), new StartupExecutionMode(), new Prompt()));
            services.AddSingleton<IHostedService>(worker);
        }).Build();
        await Assert.ThrowsExactlyAsync<LicenseAccessException>(() => host.StartAsync());
        Assert.IsFalse(worker.Started);
        await host.StopAsync();
    }

    [TestMethod]
    public async Task DiagnosticsDoNotValidateActivateOrContactServer()
    {
        var license = new LicenseProbe();
        var gate = new LicenseStartupGate(license, Product, new StartupSettings(), new StartupExecutionMode(true), new Prompt());
        await gate.StartAsync(CancellationToken.None);
        Assert.AreEqual(0, license.Calls);
    }

    [TestMethod]
    public async Task SignedLocalLicenseSurvivesTransportOutageButNotExplicitRevocation()
    {
        var license = new LicenseProbe { Local = new(LicenseStatus.Valid, Guid.NewGuid(), Online: true) };
        var gate = new LicenseStartupGate(license, Product, new StartupSettings { LicenseServerUrl = new Uri("https://isolated.invalid") }, new StartupExecutionMode(), new Prompt());
        license.Online = () => throw new LicenseAccessException(LicenseStatus.ServerUnavailable);
        await gate.StartAsync(CancellationToken.None);
        license.Online = () => new(LicenseStatus.Revoked);
        var denied = await Assert.ThrowsExactlyAsync<LicenseAccessException>(() => gate.StartAsync(CancellationToken.None));
        Assert.AreEqual(LicenseStatus.Revoked, denied.Status);
    }

    [TestMethod]
    public async Task OnlineActivationCanRetryWithThePreviousInput()
    {
        var license = new LicenseProbe
        {
            Activate = key => key == "second" ? new(LicenseStatus.Valid) : throw new LicenseAccessException(LicenseStatus.Invalid)
        };
        var prompt = new Prompt { Request = () => "first", Retry = (key, status) =>
        {
            Assert.AreEqual("first", key);
            Assert.AreEqual(LicenseStatus.Invalid, status);
            return "second";
        } };
        var gate = new LicenseStartupGate(license, Product, new StartupSettings { LicenseServerUrl = new Uri("https://isolated.invalid") },
            new StartupExecutionMode(), prompt);
        await gate.StartAsync(default);
        Assert.AreEqual(2, license.ActivationAttempts);
    }

    [TestMethod]
    public async Task OfflineBuildNeverPromptsForOnlineActivation()
    {
        var license = new LicenseProbe();
        var gate = new LicenseStartupGate(license, Product with { OnlineActivation = false },
            new StartupSettings { LicenseServerUrl = new Uri("https://isolated.invalid") }, new StartupExecutionMode(), new Prompt());
        var denied = await Assert.ThrowsExactlyAsync<LicenseAccessException>(() => gate.StartAsync(default));
        Assert.AreEqual(LicenseStatus.Missing, denied.Status);
        Assert.AreEqual(1, license.Calls);
    }

    [TestMethod]
    public async Task ExpiredDemoStopsBeforeActivation()
    {
        var licenses = new LicenseProbe { Local = new(LicenseStatus.DemoExpired) };
        var gate = new LicenseStartupGate(licenses, Product with { Required = false, Demo = true },
            new StartupSettings(), new StartupExecutionMode(), new Prompt());
        var denied = await Assert.ThrowsExactlyAsync<LicenseAccessException>(() => gate.StartAsync(default));
        Assert.AreEqual(LicenseStatus.DemoExpired, denied.Status);
        Assert.AreEqual(0, licenses.ActivationAttempts);
    }

    private static LicenseProductIdentity Product { get; } = new("WAWICALLPRO_PRO", new Guid(LicenseProductIdentity.ProApplicationId), "test", true);
    [TestMethod]
    public async Task ActivationPromptAwaitsUiDispatcherAndObservesCancellationBeforeShowingAnything()
    {
        var dispatcher = new QueuedDispatcher();
        await using var splash = new StartupSplash();
        var prompt = new AppLicensePrompt(dispatcher, splash, new AppUiState(ApplicationDescriptor.FromAssembly(typeof(MainForm).Assembly), new StartupExecutionMode(true)));
        using var cancellation = new CancellationTokenSource();
        var request = Task.Run(() => prompt.RequestKeyAsync("synthetic-machine", cancellation.Token));
        await dispatcher.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsFalse(request.IsCompleted);
        await cancellation.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => request);
    }
    private sealed class QueuedDispatcher : IUiDispatcher
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ValueTask InvokeAsync(Action callback, CancellationToken cancellationToken)
        {
            Entered.SetResult();
            return new ValueTask(_pending.Task.WaitAsync(cancellationToken));
        }
    }
    private sealed class ProbeWorker : IHostedService
    {
        public bool Started { get; private set; }
        public Task StartAsync(CancellationToken cancellationToken) { Started = true; return Task.CompletedTask; }
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
    private sealed class Prompt : IAppLicensePrompt
    {
        public Func<string?> Request { get; init; } = () => throw new AssertFailedException("Unexpected activation prompt.");
        public Func<string, LicenseStatus, string?> Retry { get; init; } = (_, _) => throw new AssertFailedException("Unexpected retry prompt.");
        public Task<string?> RequestKeyAsync(string machineId, CancellationToken cancellationToken) => Task.FromResult(Request());
        public Task<string?> RetryKeyAsync(string machineId, string previousKey, LicenseStatus status, CancellationToken cancellationToken) =>
            Task.FromResult(Retry(previousKey, status));
    }
    private sealed class LicenseProbe : IProductLicenseService
    {
        public int Calls { get; private set; }
        public int ActivationAttempts { get; private set; }
        public ProductLicenseSnapshot Local { get; init; } = new(LicenseStatus.Missing);
        public Func<ProductLicenseSnapshot> Online { get; set; } = () => new(LicenseStatus.Valid);
        public Task<ProductLicenseSnapshot> ValidateAsync(CancellationToken cancellationToken) { Calls++; return Task.FromResult(Local); }
        public Func<string, ProductLicenseSnapshot> Activate { get; init; } = _ => throw new AssertFailedException("Unexpected activation.");
        public Task<ProductLicenseSnapshot> ActivateAsync(string licenseKey, CancellationToken cancellationToken)
        {
            ActivationAttempts++;
            return Task.FromResult(Activate(licenseKey));
        }
        public Task<ProductLicenseSnapshot> RefreshAsync(CancellationToken cancellationToken) { Calls++; return Task.FromResult(Online()); }
        public string GetMachineId() => "test";
        public Task<string?> GetStoredLicenseNumberAsync(Guid licenseId, CancellationToken cancellationToken) => Task.FromResult<string?>(null);
        public Task<CrashAuthentication?> GetCrashAuthenticationAsync(CancellationToken cancellationToken) => Task.FromResult<CrashAuthentication?>(null);
    }
}

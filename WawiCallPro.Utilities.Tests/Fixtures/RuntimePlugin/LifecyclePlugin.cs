using Quantarix.Plugin.Abstractions;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Plugins;
using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Interfaces.Directory;
using Microsoft.Extensions.DependencyInjection;

namespace RuntimePlugin;

[PluginServices(typeof(FixtureServices))]
public sealed class LifecyclePlugin : PrivateDependency.PluginDependencyBase, IWawiCallPlugin, IWawiCallPluginLifetime
{
    private readonly IWawiCallHostServices? _injected;
    public LifecyclePlugin() { }
    public LifecyclePlugin(IWawiCallHostServices injected)
    {
        Fault("constructor"); _injected = injected;
    }
    public LifecyclePlugin(IWawiCallHostServices injected, FixtureService service) : this(injected) => _service = service;
    private readonly FixtureService? _service;
    public bool PrivateServiceInjected => _service is not null;
    public bool PrivateServiceDisposed => _service?.Disposed == true;
    public bool PrivateServiceDisposedAsynchronously => _service?.AsyncDisposed == true;
    public bool PrivateServiceDisposedAfterShutdown => _service?.ShutdownObserved == true;
    public bool ServicesInjected => _injected is not null;
    public bool Configured { get; private set; }
    private IWawiCallHostServices? _configuredServices;
    public LexwareProductLine ProductLine =>
#if LX_PRO
        Environment.GetEnvironmentVariable("WCP_PLUGIN_FIXTURE_FAULT") == "wrong-product" ? LexwareProductLine.FinOffice : LexwareProductLine.Pro;
#else
        Environment.GetEnvironmentVariable("WCP_PLUGIN_FIXTURE_FAULT") == "wrong-product" ? LexwareProductLine.Pro : LexwareProductLine.FinOffice;
#endif
    public IBusinessDirectory Directory => throw new NotSupportedException("Fixture has no ERP access.");
    public void Configure(IWawiCallHostServices services)
    {
        Fault("configure");
        if (_host is null || !ReferenceEquals(services, _injected)) throw new InvalidOperationException("Initialization/injection order");
        Configured = true;
        _configuredServices = services;
        services.Companies.Changed += OnCompanyChanged;
    }
    private IPluginHost? _host;
    public string Name => "private-plugin-sentinel";
    public Version Version => new(1, 0);
    public int Initializations { get; private set; }
    public int Executions { get; private set; }
    public int Shutdowns { get; private set; }
    public CompanyKey EchoCompany(CompanyKey company) => company;
    public Microsoft.Extensions.DependencyInjection.IServiceCollection EchoServices(Microsoft.Extensions.DependencyInjection.IServiceCollection services) => services;
    public System.Reflection.Assembly DapperAssembly => typeof(Dapper.SqlMapper).Assembly;
    public void Initialize(IPluginHost host)
    {
        _host = host;
        Fault("initialize");
        if (Environment.GetEnvironmentVariable("WCP_PLUGIN_FIXTURE_FAULT") == "native") _ = ProbeNative();
        Initializations++;
        host.Logger.LogInfo("private-plugin-sentinel initialized");
    }
    public void Execute()
    {
        (_host ?? throw new InvalidOperationException("Initialize required")).Logger.LogInfo("private-plugin-sentinel executed");
        Executions++;
    }
    public void Shutdown()
    {
        if (_configuredServices is { } services) services.Companies.Changed -= OnCompanyChanged;
        _configuredServices = null;
        (_host ?? throw new InvalidOperationException("Initialize required")).Logger.LogInfo("private-plugin-sentinel stopped");
        _host = null;
        Shutdowns++;
        if (_service is not null) _service.ShutdownObserved = true;
        Fault("shutdown");
    }
    private void OnCompanyChanged(object? sender, CompanySelectionChangedEventArgs args) { }
    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        Fault("stop"); await Task.Delay(1, cancellationToken);
    }
    private static void Fault(string phase)
    {
        if (Environment.GetEnvironmentVariable("WCP_PLUGIN_FIXTURE_FAULT") == phase) throw new InvalidOperationException("fixture-private-secret-" + phase);
    }
}

public sealed class FixtureServices : IPluginServiceModule
{
    public void ConfigureServices(IServiceCollection services) => services.AddScoped<FixtureService>();
}

public sealed class FixtureService : IDisposable, IAsyncDisposable
{
    public bool Disposed { get; private set; }
    public bool AsyncDisposed { get; private set; }
    public bool ShutdownObserved { get; set; }
    public void Dispose() => Disposed = true;
    public async ValueTask DisposeAsync() { await Task.Delay(1).ConfigureAwait(false); Disposed = true; AsyncDisposed = true; }
}

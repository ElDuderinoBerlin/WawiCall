using Quantarix.Plugin.Abstractions;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Plugins;
using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Interfaces.Directory;

namespace RuntimePlugin;

public sealed class LifecyclePlugin : IWawiCallPlugin, IWawiCallPluginLifetime
{
    private readonly IWawiCallHostServices? _injected;
    public LifecyclePlugin() { }
    public LifecyclePlugin(IWawiCallHostServices injected) => _injected = injected;
    public bool ServicesInjected => _injected is not null;
    public bool Configured { get; private set; }
    private IWawiCallHostServices? _configuredServices;
    public LexwareProductLine ProductLine =>
#if LX_PRO
        LexwareProductLine.Pro;
#else
        LexwareProductLine.FinOffice;
#endif
    public IBusinessDirectory Directory => throw new NotSupportedException("Fixture has no ERP access.");
    public void Configure(IWawiCallHostServices services)
    {
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
    }
    private void OnCompanyChanged(object? sender, CompanySelectionChangedEventArgs args) { }
    public async ValueTask StopAsync(CancellationToken cancellationToken = default) => await Task.Delay(1, cancellationToken);
}

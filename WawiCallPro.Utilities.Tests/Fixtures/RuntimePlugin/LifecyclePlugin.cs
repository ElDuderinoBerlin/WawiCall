using Quantarix.Plugin.Abstractions;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Plugins;
using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Interfaces.Directory;

namespace RuntimePlugin;

public sealed class LifecyclePlugin : IWawiCallPlugin
{
    private readonly IWawiCallHostServices? _injected;
    public LifecyclePlugin() { }
    public LifecyclePlugin(IWawiCallHostServices injected) => _injected = injected;
    public bool ServicesInjected => _injected is not null;
    public bool Configured { get; private set; }
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
    }
    private IPluginHost? _host;
    public string Name => "private-plugin-sentinel";
    public Version Version => new(1, 0);
    public int Initializations { get; private set; }
    public int Executions { get; private set; }
    public int Shutdowns { get; private set; }
    public CompanyKey EchoCompany(CompanyKey company) => company;
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
        (_host ?? throw new InvalidOperationException("Initialize required")).Logger.LogInfo("private-plugin-sentinel stopped");
        _host = null;
        Shutdowns++;
    }
}

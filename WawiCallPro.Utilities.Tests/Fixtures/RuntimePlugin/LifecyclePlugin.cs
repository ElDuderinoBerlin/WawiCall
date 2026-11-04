using Quantarix.Plugin.Abstractions;
using WawiCallPro.Interfaces.Companies;

namespace RuntimePlugin;

public sealed class LifecyclePlugin : IPlugin
{
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

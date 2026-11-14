using Quantarix.Plugin.Abstractions;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Interfaces.Plugins;

namespace WawiCallPro.Plugins.Lexware.Pro;

[PluginServices(typeof(ProPluginServices))]
public sealed class LexwarePlugin(IWawiCallHostServices services, IBusinessDirectory directory) : IWawiCallPlugin, IWawiCallPluginWorkspaces
{
    private bool _initialized;
    private bool _configured;
    public string Name => "Lexware Pro";
    public Version Version { get; } = new(1, 0);
    public LexwareProductLine ProductLine => LexwareProductLine.Pro;
    public IBusinessDirectory Directory => _configured ? directory : throw new InvalidOperationException("Plugin is not configured.");
    public void Initialize(IPluginHost host) { ArgumentNullException.ThrowIfNull(host); _initialized = true; }
    public void Configure(IWawiCallHostServices hostServices)
    {
        if (!_initialized || !ReferenceEquals(services, hostServices)) throw new InvalidOperationException("Plugin initialization/injection order.");
        _configured = true;
    }
    public IPluginLifetimeResource CreatePartySearch(IPartySearchWorkspaceFactory factory) => _configured
        ? factory.Create() : throw new InvalidOperationException("Plugin is not configured.");
    public void Execute() => throw new NotSupportedException("Fachoperationen werden über den Datenadapter ausgeführt.");
    public void Shutdown() { _configured = false; _initialized = false; }
}

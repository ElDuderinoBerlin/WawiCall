using Quantarix.Plugin.Abstractions;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Interfaces.Plugins;

namespace WawiCallPro.Plugins.Lexware.Pro;

public sealed class LexwarePlugin(IWawiCallHostServices services) : IWawiCallPlugin
{
    private bool _initialized;
    private bool _configured;
    public string Name => "Lexware Pro";
    public Version Version { get; } = new(1, 0);
    public LexwareProductLine ProductLine => LexwareProductLine.Pro;
    public IBusinessDirectory Directory => _configured ? throw new NotSupportedException("Der Lexware-Pro-Datenadapter folgt in Phase 5.") : throw new InvalidOperationException("Plugin is not configured.");
    public void Initialize(IPluginHost host) { ArgumentNullException.ThrowIfNull(host); _initialized = true; }
    public void Configure(IWawiCallHostServices hostServices)
    {
        if (!_initialized || !ReferenceEquals(services, hostServices)) throw new InvalidOperationException("Plugin initialization/injection order.");
        _configured = true;
    }
    public void Execute() => throw new NotSupportedException("Fachoperationen werden über den Datenadapter ausgeführt.");
    public void Shutdown() { _configured = false; _initialized = false; }
}

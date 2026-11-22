using Quantarix.Plugin.Abstractions;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Interfaces.Plugins;
using WawiCallPro.Plugin.Windows;

namespace WawiCallPro.Plugins.Lexware.Pro;

[PluginServices(typeof(ProPluginServices))]
public sealed class LexwarePlugin(IWawiCallHostServices services, IBusinessDirectory directory) : IWawiCallPlugin, IWawiCallPluginWorkspaces, IWawiCallPluginSettings, IBusinessDirectoryConnectionState
{
    private bool _initialized;
    private bool _configured;
    public string Name => "Lexware Pro";
    public Version Version { get; } = new(1, 0);
    public LexwareProductLine ProductLine => LexwareProductLine.Pro;
    public bool IsConnectionConfigured => directory is not IBusinessDirectoryConnectionState connection || connection.IsConnectionConfigured;
    public IBusinessDirectory Directory => _configured ? directory : throw new InvalidOperationException("Plugin is not configured.");
    public void Initialize(IPluginHost host) { ArgumentNullException.ThrowIfNull(host); _initialized = true; }
    public void Configure(IWawiCallHostServices hostServices)
    {
        if (!_initialized || !ReferenceEquals(services, hostServices)) throw new InvalidOperationException("Plugin initialization/injection order.");
        _configured = true;
    }
    public IPluginLifetimeResource CreatePartySearch(IPartySearchWorkspaceFactory factory) => _configured
        ? factory.Create() : throw new InvalidOperationException("Plugin is not configured.");
    public IPluginSettingsView CreateConnectionSettings(IPluginConnectionEditContext context)
    {
        if (!_configured) throw new InvalidOperationException("Plugin is not configured.");
        var page = new ProConnectionSettingsPage();
        try { page.Reload(context); return new SettingsView(page); }
        catch { page.Dispose(); throw; }
    }
    public string ConnectionSettingsTitle => "Lexware-Pro-Zugang";
    private sealed class SettingsView(ProConnectionSettingsPage page) : IPluginSettingsView
    {
        public System.Windows.Forms.Control View => page;
        public bool HasUnsavedChanges => page.HasChanges();
        public void Reload(IPluginConnectionEditContext context) => page.Reload(context);
        public void ApplyChanges(IPluginConnectionEditContext context) => page.ApplyChanges(context);
        public void Dispose() => page.Dispose();
    }
    public void Execute() => throw new NotSupportedException("Fachoperationen werden über den Datenadapter ausgeführt.");
    public void Shutdown() { _configured = false; _initialized = false; }
}

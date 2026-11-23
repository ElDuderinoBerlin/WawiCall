using Quantarix.Plugin.Abstractions;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Interfaces.Plugins;
using WawiCallPro.Plugin.Windows;

namespace WawiCallPro.Plugins.Lexware.FinOffice;

[PluginServices(typeof(FinOfficePluginServices))]
public sealed class LexwarePlugin(IWawiCallHostServices services, IBusinessDirectory directory) : IWawiCallPlugin, IWawiCallPluginWorkspaces, IWawiCallPluginSettings, IWawiCallPluginShell
{
    private bool _initialized;
    private bool _configured;
    public string Name => "Lexware Financial Office";
    public Version Version { get; } = new(1, 0);
    public LexwareProductLine ProductLine => LexwareProductLine.FinOffice;
    public string ConnectionSettingsTitle => "Lexware";
    public IPluginSettingsView CreateConnectionSettings(IPluginConnectionEditContext context) => _configured
        ? new PluginCompanySettingsView(directory, ProductLine, context) : throw new InvalidOperationException("Plugin is not configured.");
    public IBusinessDirectory Directory => _configured ? directory : throw new InvalidOperationException("Plugin is not configured.");
    public void Initialize(IPluginHost host) { ArgumentNullException.ThrowIfNull(host); _initialized = true; }
    public void Configure(IWawiCallHostServices hostServices)
    {
        if (!_initialized || !ReferenceEquals(services, hostServices)) throw new InvalidOperationException("Plugin initialization/injection order.");
        _configured = true;
    }
    public void RegisterShell(IPluginShellUi host)
    {
        host.RegisterGroup(new("stammdaten", "Stammdaten"));
        host.RegisterGroup(new("lexware-einrichtung", "Lexware-Einrichtung"));
        host.RegisterNavigation(new("geschaeftspartner", "Kunden und Lieferanten",
            "Kunden und Lieferanten im aktiven Lexware-Plugin suchen.", PluginShellIcon.Parties, PluginNavigationTarget.PartySearch));
        host.RegisterNavigation(new("lexware-einrichtung", "Lexware-Mandanten",
            "Den Lexware-Bereich im Einstellungsdialog öffnen.", PluginShellIcon.Settings, PluginNavigationTarget.PluginSettings));
        host.RegisterButton(new("geschaeftspartner", "stammdaten", "geschaeftspartner"));
        host.RegisterButton(new("lexware-einrichtung", "lexware-einrichtung", "lexware-einrichtung"));
    }
    public IPluginLifetimeResource CreatePartySearch(IPartySearchWorkspaceFactory factory) => _configured
        ? factory.Create() : throw new InvalidOperationException("Plugin is not configured.");
    public void Execute() => throw new NotSupportedException("Fachoperationen werden über den Datenadapter ausgeführt.");
    public void Shutdown() { _configured = false; _initialized = false; }
}

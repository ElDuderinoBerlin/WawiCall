using Quantarix.Plugin.Abstractions;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Interfaces.Plugins;
using WawiCallPro.Plugin.Windows;

namespace WawiCallPro.Plugins.Lexware.Pro;

[PluginServices(typeof(ProPluginServices))]
public sealed class LexwarePlugin(
    IWawiCallHostServices services,
    IBusinessDirectory directory,
    IProDirectoryFactory directories) : IWawiCallPlugin, IWawiCallPluginWorkspaces, IWawiCallPluginSettings, IWawiCallPluginShell, IBusinessDirectoryConnectionState
{
    private bool _initialized;
    private bool _configured;
    public string Name => "Lexware Pro";
    public Version Version { get; } = new(1, 0);
    public LexwareProductLine ProductLine => LexwareProductLine.Pro;
    public long ConnectionRevision => (directory as IBusinessDirectoryConnectionState)?.ConnectionRevision ?? 0;
    public bool IsConnectionConfigured => directory is not IBusinessDirectoryConnectionState connection || connection.IsConnectionConfigured;
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
        host.RegisterNavigation(new("geschaeftspartner", "Kunden und Lieferanten suchen",
            "Kunden und Lieferanten im aktiven Lexware-Plugin suchen.", PluginShellIcon.Parties, PluginNavigationTarget.PartySearch));
        host.RegisterNavigation(new("lexware-einrichtung", "Lexware-Zugang und Mandanten",
            "Den Lexware-Bereich im Einstellungsdialog öffnen.", PluginShellIcon.Settings, PluginNavigationTarget.PluginSettings));
        host.RegisterButton(new("geschaeftspartner", "stammdaten", "geschaeftspartner"));
        host.RegisterButton(new("lexware-einrichtung", "lexware-einrichtung", "lexware-einrichtung"));
    }

    public IPluginLifetimeResource CreatePartySearch(IPartySearchWorkspaceFactory factory) => _configured
        ? factory.Create() : throw new InvalidOperationException("Plugin is not configured.");
    public IPluginSettingsView CreateConnectionSettings(IPluginConnectionEditContext context)
    {
        if (!_configured) throw new InvalidOperationException("Plugin is not configured.");
        var page = new LexwareSettingsPage { Directories = directories };
        try { page.Reload(context, directory, () => IsConnectionConfigured); return new SettingsView(page, directory, () => IsConnectionConfigured); }
        catch { page.Dispose(); throw; }
    }
    public string ConnectionSettingsTitle => "Lexware";
    private sealed class SettingsView(LexwareSettingsPage page, IBusinessDirectory directory, Func<bool> connectionReady) : IPluginSettingsView
    {
        public System.Windows.Forms.Control View => page;
        public bool HasUnsavedChanges => page.HasChanges;
        public bool SupportsCompanyValidation => true;
        public Task<Company?> ResolveCompanyAsync(CompanyKey company, CancellationToken token = default) => page.ResolveCompanyAsync(company, token);
        public void Reload(IPluginConnectionEditContext context) => page.Reload(context, directory, connectionReady);
        public void ApplyChanges(IPluginConnectionEditContext context) => page.ApplyChanges(context);
        public Task LoadAsync(CancellationToken cancellationToken = default) => page.LoadAsync(cancellationToken);
        public Task ValidateAsync(CancellationToken cancellationToken = default) => page.ValidateAsync(cancellationToken);
        public Task StopAsync(CancellationToken cancellationToken = default) => page.StopAsync(cancellationToken);
        public void Dispose() => page.Dispose();
    }
    public void Execute() => throw new NotSupportedException("Fachoperationen werden über den Datenadapter ausgeführt.");
    public void Shutdown() { _configured = false; _initialized = false; }
}

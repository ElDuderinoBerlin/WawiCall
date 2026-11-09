using System.Reflection;
using DevExpress.XtraBars;
using DevExpress.XtraBars.Ribbon;
using DevExpress.XtraEditors;
using DevExpress.XtraTab;
using DevExpress.XtraBars.Docking;
using DevExpress.XtraBars.Docking2010.Views;
using WawiCallPro.App.Hosting;
using WawiCallPro.App.Views;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Plugin.Windows;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Hosting;

namespace WawiCallPro.App;

public partial class MainForm : RibbonForm
{
    private readonly ApplicationDescriptor? _application;
    private readonly ApplicationConfigurationService? _configuration;
    private readonly AppWorkspace? _workspace;
    private readonly AppUiState? _state;
    private readonly Dictionary<BaseDocument, IPluginViewHandle> _pluginPages = [];
    private readonly Dictionary<string, BaseDocument> _documents = [];
    private BaseDocument? _lastDocument;
    private PageOperation? _operation;
    private Task _selectionTask = Task.CompletedTask;
    private bool _backgroundRun;
    private bool _startupWithoutActivation;
    private bool _removingDocument;
    private bool _loaded;
    private bool _closing;

    public MainForm() { InitializeComponent(); AppIconHelper.Apply(this); ConfigureIcons(); }
    public MainForm(ApplicationDescriptor application) : this() => _application = application ?? throw new ArgumentNullException(nameof(application));
    public MainForm(ApplicationDescriptor application, ApplicationConfigurationService configuration) : this(application) => _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    public MainForm(ApplicationDescriptor application, ApplicationConfigurationService configuration, AppWorkspace workspace, AppUiState state) : this(application, configuration)
    { _workspace = workspace; _state = state; }

    public MainForm(ApplicationDescriptor application, ApplicationConfigurationService configuration, AppWorkspace workspace, AppUiState state, ILogWindowService logs)
        : this(application, configuration, workspace, state) => logPanel.Bind(logs);

    internal void PrepareForBackgroundRun()
    {
        _backgroundRun = true;
        ShowInTaskbar = false;
        Opacity = 0;
    }
    internal void ShowAfterStartup(bool preserveForeground)
    {
        _startupWithoutActivation = preserveForeground;
        try { Show(); }
        finally { _startupWithoutActivation = false; }
    }

    protected override bool ShowWithoutActivation => _backgroundRun || _startupWithoutActivation || base.ShowWithoutActivation;
    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            if (_backgroundRun) parameters.ExStyle |= 0x08000000;
            return parameters;
        }
    }

    private void MainForm_Load(object? sender, EventArgs e)
    {
        if (_loaded) return;
        _loaded = true;
        var app = _application ?? ApplicationDescriptor.FromAssembly(Assembly.GetExecutingAssembly());
        Text = $"{app.Name} – {app.ProductLine}";
        connectionLabel.Text = _configuration?.Current.IsSipConfigured == true ? "SIP-Konto konfiguriert. Telefonie noch nicht verbunden." : "Kein SIP-Konto konfiguriert.";
        statusItem.Caption = app.ProductLine + " · " + (_workspace?.Plugins.IsLoaded == true ? "Warenwirtschaftsplugin geladen" : "Kein Warenwirtschaftsplugin geladen");
        _operation = new PageOperation(this, connectionLabel);
        _state?.Restore(this);
        _state?.Restore(dockManager);
        Navigate("home");
        AppendOutput("Anwendung gestartet. Verbindungen werden ausschließlich auf Anforderung geprüft.");
    }

    internal void Navigate(string id)
    {
        if (_closing) return;
        if (id is "settings" or "plugin") { OpenSettings(id == "plugin"); return; }
        if (_documents.TryGetValue(id, out var existing)) { ActivateDocument(existing); return; }
        Control content;
        string caption;
        switch (id)
        {
            case "home":
                var dashboard = new HomeDashboard();
                dashboard.Bind(_workspace, _configuration, _application ?? ApplicationDescriptor.FromAssembly(typeof(MainForm).Assembly));
                dashboard.Navigate += Navigate;
                content = dashboard; caption = "Startseite"; break;
            case "database":
                var database = new DatabasePage(); if (_workspace is not null) database.Bind(_workspace);
                content = database; caption = "Datenbank"; break;
            case "history":
                var history = new HistoryPage(); if (_workspace is not null) history.Bind(_workspace, _state);
                content = history; caption = "Gesprächshistorie"; break;
            default:
                var start = new StartPage(); start.Navigate += Navigate;
                if (id == "help") start.SetContent("WawiCallPro verwenden",
                    "1. In den Einstellungen SIP-Konto und Kommunikationsdatenbank konfigurieren.\n2. Nach geänderten Verbindungseinstellungen die Anwendung neu starten.\n3. Unter Datenbank den Status prüfen und bei Bedarf das Schema anlegen oder aktualisieren.\n4. Die Mandanten für den Telefon-Cache unter Einstellungen auswählen.\n5. Gespeicherte Gespräche und Notizen finden Sie in der Gesprächshistorie.",
                    "Telefonie, Audioanbindung und zusätzliche Wawi-Fachtabs folgen in ihren Fachphasen.", false);
                else if (id == "about") start.SetContent("Über WawiCallPro",
                    "WawiCallPro " + typeof(MainForm).Assembly.GetName().Version + "\nProduktlinie: " + (_application?.ProductLine ?? "Designer") + "\nTelefonie: SIPSorcery\nKommunikationsdatenbank: SQLite, PostgreSQL, SQL Server oder MySQL/MariaDB.",
                    "Kunden- und Lieferantenansichten stellt das jeweilige Warenwirtschaftsplugin bereit.", false);
                else start.SetContent("Willkommen bei WawiCallPro",
                    "Öffnen Sie die Arbeitsbereiche über die Navigation. Die individuellen Fach-Tabs stellt das aktive Warenwirtschaftsplugin bereit.", connectionLabel.Text, false);
                content = start; caption = id == "help" ? "Hilfe" : id == "about" ? "Über WawiCallPro" : "Startseite"; break;
        }
        content.Dock = DockStyle.Fill;
        var host = new Panel { Dock = DockStyle.Fill };
        host.Controls.Add(content);
        var document = tabbedView.AddDocument(host);
        document.Caption = caption;
        _documents.Add(id, document);
        ActivateDocument(document);
        AppendOutput(caption + " geöffnet.");
    }

    internal async Task OpenPartyAsync(PartyKey party, CancellationToken token = default)
    {
        if (_workspace is null || _closing) return;
        if (_workspace.Companies.Current.Company?.Key != party.Company) throw new InvalidOperationException("Der Geschäftspartner gehört nicht zum gewählten Mandanten.");
        using var operation = _workspace.Companies.BeginOperation(token);
        var plugin = await _workspace.GetPluginAsync(operation.CancellationToken);
        await ClosePluginPagesAsync();
        foreach (var descriptor in plugin.Views)
        {
            var handle = await _workspace.Views.OpenAsync(descriptor.Id, party, operation.CancellationToken);
            try
            {
                var published = await operation.PublishAsync(_workspace.Dispatcher, () =>
                {
                    handle.View.Dock = DockStyle.Fill;
                    var host = new Panel { Dock = DockStyle.Fill };
                    host.Controls.Add(handle.View);
                    var document = tabbedView.AddDocument(host);
                    document.Caption = descriptor.DisplayName;
                    _pluginPages.Add(document, handle);
                    ActivateDocument(document);
                });
                if (!published) await handle.DisposeAsync();
            }
            catch { await handle.DisposeAsync(); throw; }
        }
        if (plugin.Views.IsEmpty) connectionLabel.Text = "Das aktive Warenwirtschaftsplugin stellt noch keine Fachansichten bereit.";
    }

    private async Task ClosePluginPagesAsync()
    {
        foreach (var page in _pluginPages.Keys.ToArray()) await CloseDocumentAsync(page);
    }
    internal async Task PrepareToCloseAsync()
    {
        _closing = true; tray.Visible = false;
        if (_operation is not null) await _operation.StopAsync();
        await _selectionTask;
        foreach (var page in _documents.Values.ToArray()) await StopPageAsync(page.Control);
        foreach (var settings in OwnedForms.OfType<FrmSettings>()) await settings.StopAsync();
        await ClosePluginPagesAsync();
        if (!_backgroundRun) { _state?.Save(this); _state?.Save(dockManager); }
    }
    private static Task StopPageAsync(Control? control) => (control is Panel { Controls.Count: > 0 } host ? host.Controls[0] : control) switch
    {
        SettingsPage page => page.StopAsync(), DatabasePage page => page.StopAsync(), PluginPage page => page.StopAsync(), HistoryPage page => page.StopAsync(), _ => Task.CompletedTask
    };
    private async void ClosePage_Click(object sender, ItemClickEventArgs e)
    {
        if (tabbedView.ActiveDocument is not { } page) return;
        try { await CloseDocumentAsync(page); }
        catch (Exception exception) { connectionLabel.Text = PageOperation.Describe(exception); }
    }
    private async Task CloseDocumentAsync(BaseDocument page)
    {
        await StopPageAsync(page.Control);
        if (_pluginPages.Remove(page, out var handle)) await handle.DisposeAsync();
        foreach (var key in _documents.Where(pair => ReferenceEquals(pair.Value, page)).Select(pair => pair.Key).ToArray()) _documents.Remove(key);
        var control = page.Control;
        if (ReferenceEquals(_lastDocument, page)) _lastDocument = null;
        _removingDocument = true;
        try { tabbedView.Controller.Close(page); }
        finally { _removingDocument = false; }
        control?.Dispose();
    }
    private async void Document_Closing(object sender, DocumentCancelEventArgs e)
    {
        if (_removingDocument) return;
        e.Cancel = true;
        try { await CloseDocumentAsync(e.Document); }
        catch (Exception exception) { connectionLabel.Text = PageOperation.Describe(exception); }
    }
    private void MainForm_SizeChanged(object? sender, EventArgs e)
    {
        ribbon.Minimized = ClientSize.Height < 900 && Font.Size > 13;
    }
    private void AppendOutput(string message) => logPanel.Append(message);
    private void Home_Click(object sender, ItemClickEventArgs e) => Navigate("home");
    private void Database_Click(object sender, ItemClickEventArgs e) => Navigate("database");
    private void Plugin_Click(object sender, ItemClickEventArgs e) => Navigate("plugin");
    private void History_Click(object sender, ItemClickEventArgs e) => Navigate("history");
    private void Settings_Click(object sender, ItemClickEventArgs e) => OpenSettings();
    private void Help_Click(object sender, ItemClickEventArgs e) => ToggleHelp();
    private void About_Click(object sender, ItemClickEventArgs e) => XtraMessageBox.Show(this,
        "WawiCallPro " + typeof(MainForm).Assembly.GetName().Version + "\nProduktlinie: "
        + (_application?.ProductLine ?? ApplicationDescriptor.FromAssembly(typeof(MainForm).Assembly).ProductLine),
        "Über WawiCallPro", MessageBoxButtons.OK, MessageBoxIcon.Information);
    private void ToggleHelp()
    {
        if (helpDock.Visibility == DockVisibility.Visible) helpDock.Hide();
        else helpDock.Show();
    }
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData != Keys.F1) return base.ProcessCmdKey(ref msg, keyData);
        ToggleHelp();
        return true;
    }
    private void HomeNavigation_Click(object? sender, EventArgs e) => Navigate("home");
    private void DatabaseNavigation_Click(object? sender, EventArgs e) => Navigate("database");
    private void PluginNavigation_Click(object? sender, EventArgs e) => Navigate("plugin");
    private void HistoryNavigation_Click(object? sender, EventArgs e) => Navigate("history");
    private void SettingsNavigation_Click(object? sender, EventArgs e) => OpenSettings();
    private void HelpNavigation_Click(object? sender, EventArgs e) => helpDock.Show();
    internal FrmSettings CreateSettingsDialog(bool plugins = false)
    {
        var settings = _workspace is null ? new FrmSettings() : new FrmSettings(_workspace, PreparePluginSwitchAsync);
        settings.ConfigureLayoutPersistence(_state);
        if (plugins) settings.SelectPluginsTab();
        return settings;
    }
    private async Task PreparePluginSwitchAsync()
    {
        if (_workspace is not null) await _workspace.Companies.SelectAsync(null);
        await _selectionTask;
        await ClosePluginPagesAsync();
    }
    private void OpenSettings(bool plugins = false)
    {
        using var settings = CreateSettingsDialog(plugins);
        settings.ShowDialog(this);
        foreach (var document in _documents.Values)
            if (document.Control is Panel { Controls.Count: > 0 } host && host.Controls[0] is HomeDashboard dashboard)
                dashboard.RefreshStatus();
        statusItem.Caption = (_application?.ProductLine ?? _workspace?.Application.ProductLine ?? "") + " · "
            + (_workspace?.Plugins.IsLoaded == true ? "Warenwirtschaftsplugin geladen" : "Kein Warenwirtschaftsplugin geladen");
    }
    private void ShowNavigation_Click(object sender, ItemClickEventArgs e) => navigationDock.Show();
    private void ShowOutput_Click(object sender, ItemClickEventArgs e) => outputDock.Show();
    private void Exit_Click(object sender, ItemClickEventArgs e) => Close();
    private void Tray_Click(object sender, ItemClickEventArgs e)
    {
        if (_backgroundRun) return;
        tray.Icon = Icon ?? SystemIcons.Application; tray.Visible = true; Hide();
    }
    private void Tray_DoubleClick(object? sender, EventArgs e) { Show(); WindowState = FormWindowState.Normal; Activate(); tray.Visible = false; }
    private void Font_Click(object sender, ItemClickEventArgs e)
    {
        using var dialog = new FontDialog { Font = WindowsFormsSettings.DefaultFont, MinSize = 8, MaxSize = 24 };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        WindowsFormsSettings.DefaultFont = DevExpress.Utils.AppearanceObject.DefaultFont = dialog.Font; Font = dialog.Font; PerformLayout();
    }
    private void Reset_Click(object sender, ItemClickEventArgs e)
    {
        _state?.Reset(); WindowState = FormWindowState.Normal; ClientSize = new Size(1240, 780);
        navigationDock.DockTo(DockingStyle.Left);
        outputDock.DockTo(DockingStyle.Bottom);
        navigationDock.Show(); outputDock.Show(); helpDock.Hide();
        connectionLabel.Text = "Gespeichertes Fenster- und Spaltenlayout zurückgesetzt.";
    }
    private void ConfigureIcons()
    {
        homeNav.ImageOptions.SvgImage = AppIconHelper.ApplicationSvg;
        foreach (var (item, file) in new[] { (homeItem,"Home"), (pluginItem,"Plugin"), (historyItem,"History"), (closeItem,"Close"), (trayItem,"Tray"), (exitItem,"Close"), (databaseItem,"Database"), (settingsItem,"Settings"), (resetItem,"Reset"), (fontItem,"Font"), (helpItem,"Help"), (aboutItem,"About"), (navigationItem,"Home"), (outputItem,"History") })
        {
            using var stream = typeof(MainForm).Assembly.GetManifestResourceStream("WawiCallPro.App.Icons." + file + ".svg");
            if (stream is not null) item.ImageOptions.SvgImage = DevExpress.Utils.Svg.SvgImage.FromStream(stream);
        }
        databaseNav.ImageOptions.SvgImage = databaseItem.ImageOptions.SvgImage;
        historyNav.ImageOptions.SvgImage = historyItem.ImageOptions.SvgImage;
        pluginNav.ImageOptions.SvgImage = pluginItem.ImageOptions.SvgImage;
        settingsNav.ImageOptions.SvgImage = settingsItem.ImageOptions.SvgImage;
        helpNav.ImageOptions.SvgImage = helpItem.ImageOptions.SvgImage;
    }
    private void DisposePage() { tray.Visible = false; tray.Dispose(); _operation?.Dispose(); }
    internal Control? GetActiveDocumentView() => (tabbedView.ActiveDocument ?? _lastDocument)?.Control is Panel { Controls.Count: > 0 } host ? host.Controls[0] : null;
    private void ActivateDocument(BaseDocument document)
    {
        tabbedView.Controller.Activate(document);
        if (_backgroundRun && document is DevExpress.XtraBars.Docking2010.Views.Tabbed.Document tab)
            foreach (DevExpress.XtraBars.Docking2010.Views.Tabbed.DocumentGroup group in tabbedView.DocumentGroups)
                if (group.Items.Contains(tab)) group.SetSelected(tab);
        _lastDocument = document;
    }
}

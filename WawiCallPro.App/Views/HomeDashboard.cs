using DevExpress.XtraEditors;
using WawiCallPro.App.Hosting;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Hosting;

namespace WawiCallPro.App.Views;

/// <summary>Start workspace with configuration status and shortcuts, following the LxTranslate layout.</summary>
public partial class HomeDashboard : XtraUserControl
{
    private AppWorkspace? _workspace;
    private ApplicationConfigurationService? _configuration;
    private ApplicationDescriptor? _application;
    private bool _arranging;
    internal event Action<string>? Navigate;

    public HomeDashboard()
    {
        InitializeComponent();
        UiButtonSizing.Attach(this);
        logo.SvgImage = AppIconHelper.ApplicationSvg;
        logo.SvgImageColorizationMode = DevExpress.Utils.SvgImageColorizationMode.None;
        SizeChanged += Dashboard_SizeChanged;
        FontChanged += Dashboard_SizeChanged;
        body.SizeChanged += Body_SizeChanged;
        var appearance = LookAndFeel;
        appearance.StyleChanged += Dashboard_SizeChanged;
        Disposed += (_, _) => appearance.StyleChanged -= Dashboard_SizeChanged;
        ArrangeDashboard();
    }

    internal void Bind(AppWorkspace? workspace, ApplicationConfigurationService? configuration, ApplicationDescriptor application)
    {
        _workspace = workspace; _configuration = configuration; _application = application;
        RefreshStatus();
    }

    internal void RefreshStatus()
    {
        var options = _configuration?.Current.Options;
        version.Text = "WawiCallPro " + ApplicationVersion.Display;
        databaseValue.Text = options?.Database.Provider.ToString() ?? "Nicht konfiguriert";
        databaseDetail.Text = "Verbindung und Schema im Ribbon-Tab Datenbank prüfen.";
        sipValue.Text = _configuration?.Current.IsSipConfigured == true ? "SIP-Konto konfiguriert" : "Nicht konfiguriert";
        var registration = _workspace?.Telephony?.Registration.State ?? WawiCallPro.Interfaces.Telephony.RegistrationState.Stopped;
        sipDetail.Text = registration switch
        {
            WawiCallPro.Interfaces.Telephony.RegistrationState.Registered => "An der Telefonanlage angemeldet.",
            WawiCallPro.Interfaces.Telephony.RegistrationState.Registering => "Anmeldung läuft …",
            WawiCallPro.Interfaces.Telephony.RegistrationState.Reconnecting => "Verbindung unterbrochen; erneute Anmeldung …",
            WawiCallPro.Interfaces.Telephony.RegistrationState.Failed => "Anmeldung fehlgeschlagen.",
            _ => "Nicht an der Telefonanlage angemeldet."
        };
        pluginValue.Text = _workspace?.Plugins.IsLoaded == true ? "Plugin geladen" : "Kein Plugin geladen";
        if (_workspace?.Plugins.IsLoaded == true && !_workspace.CanUseBusinessDirectory) pluginValue.Text = "Lexware nicht verfügbar";
        var selection = _workspace?.Plugins as WawiCallPro.Interfaces.Plugins.IProductPluginSelection;
        pluginDetail.Text = selection?.GetAvailablePlugins().FirstOrDefault(plugin => plugin.Id == selection.ActivePluginId)?.DisplayName
            ?? "Plugin in den Einstellungen auswählen.";
        editionValue.Text = _application?.ProductLine ?? "WawiCallPro";
        cacheValue.Text = options?.Companies.Length > 0 ? options.Companies.Length + " Mandanten konfiguriert" : "Mandanten auswählen";
        ArrangeDashboard();
    }

    private void Dashboard_SizeChanged(object? sender, EventArgs e) => ArrangeDashboard();
    private void Body_SizeChanged(object? sender, EventArgs e) => scroll.AutoScrollMinSize = new Size(0, body.Height);
    private void ArrangeDashboard()
    {
        if (_arranging) return;
        _arranging = true;
        try
        {
            var narrow = Width < 1000 * Font.Size / 10F;
            body.SuspendLayout(); toolbar.SuspendLayout();
            body.ColumnStyles[0].Width = narrow ? 100 : 67;
            body.ColumnStyles[1].Width = narrow ? 0 : 33;
            body.SetCellPosition(steps, narrow ? new TableLayoutPanelCellPosition(0, 1) : new TableLayoutPanelCellPosition(1, 0));
            body.RowCount = narrow ? 2 : 1;
            var singleCardColumn = Width < 800 * Font.Size / 10F;
            var cardControls = new[] { databaseCard, sipCard, pluginCard, journalCard, cacheCard, editionCard };
            var cardColumns = singleCardColumn ? 1 : 2;
            cards.SuspendLayout();
            cards.ColumnCount = cardColumns; cards.RowCount = cardControls.Length / cardColumns;
            cards.ColumnStyles.Clear(); cards.RowStyles.Clear();
            for (var i = 0; i < cardColumns; i++) cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / cardColumns));
            for (var i = 0; i < cards.RowCount; i++) cards.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            for (var i = 0; i < cardControls.Length; i++) cards.SetCellPosition(cardControls[i], new(i % cardColumns, i / cardColumns));
            cards.ResumeLayout(true);
            var buttons = new[] { refreshButton, historyButton, settingsButton };
            var buttonWidth = buttons.Max(button => button.GetPreferredSize(Size.Empty).Width);
            var cellWidth = buttonWidth + buttons.Max(button => button.Margin.Horizontal);
            var columns = Math.Clamp((toolbar.ClientSize.Width - toolbar.Padding.Horizontal) / Math.Max(1, cellWidth), 1, buttons.Length);
            toolbar.ColumnCount = columns + 1;
            toolbar.RowCount = (buttons.Length + columns - 1) / columns;
            toolbar.ColumnStyles.Clear(); toolbar.RowStyles.Clear();
            for (var i = 0; i < columns; i++) toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, cellWidth));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            for (var i = 0; i < toolbar.RowCount; i++) toolbar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            for (var i = 0; i < buttons.Length; i++) toolbar.SetCellPosition(buttons[i], new(i % columns, i / columns));
            toolbar.ResumeLayout(true); body.ResumeLayout(true);
        }
        finally { _arranging = false; }
    }

    private void Refresh_Click(object? sender, EventArgs e) => RefreshStatus();
    private void History_Click(object? sender, EventArgs e) => Navigate?.Invoke("history");
    private void Settings_Click(object? sender, EventArgs e) => Navigate?.Invoke("settings");
}

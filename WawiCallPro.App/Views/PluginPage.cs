using DevExpress.XtraEditors;
using DevExpress.Skins;
using DevExpress.Utils.Html;
using DevExpress.Utils.Svg;
using DevExpress.LookAndFeel;
using DevExpress.Utils.Colors;
using DevExpress.XtraGrid.Views.Items;
using System.Net;
using WawiCallPro.App.Hosting;
using WawiCallPro.Interfaces.Capabilities;
using WawiCallPro.Interfaces.Plugins;

namespace WawiCallPro.App.Views;

public partial class PluginPage : XtraUserControl
{
    private AppWorkspace? _workspace;
    private AppUiState? _state;
    private PageOperation? _operation;
    private Func<string, CancellationToken, Task>? _activate;
    private bool _switching;
    internal void ReportSwitchStatus(string text)
    {
        if (!_switching) return;
        statusLabel.Text = text;
        layout.PerformLayout();
        statusLabel.Update();
    }
    private readonly HtmlTemplate _activeCard = new();
    private static readonly Lazy<SvgImage> PluginIcon = new(() => LoadIcon("Plugin"));
    private static readonly Lazy<SvgImage> DummyIcon = new(() => LoadIcon("Settings"));
    public PluginPage()
    {
        InitializeComponent();
        _activeCard.Template = CardResource("PluginCard.ActiveTemplate");
        ApplyCardStyle();
        var appearance = LookAndFeel;
        appearance.StyleChanged += Appearance_Changed;
        FontChanged += Appearance_Changed;
        Disposed += (_, _) => appearance.StyleChanged -= Appearance_Changed;
    }
    private static string CardResource(string name) => new System.ComponentModel.ComponentResourceManager(typeof(PluginPage)).GetString(name) ?? "";
    private static SvgImage LoadIcon(string name)
    {
        using var stream = typeof(PluginPage).Assembly.GetManifestResourceStream("WawiCallPro.App.Icons." + name + ".svg")!;
        return SvgImage.FromStream(stream);
    }
    private void Appearance_Changed(object? sender, EventArgs e) => ApplyCardStyle();
    private void ApplyCardStyle()
    {
        if (catalogView is null) return;
        var colors = CommonSkins.GetSkin(LookAndFeel.ActiveLookAndFeel).Colors;
        var surface = colors.GetColor(CommonColors.Control);
        if (surface.IsEmpty) surface = SystemColors.Control;
        var text = colors.GetColor(CommonColors.ControlText);
        if (text.IsEmpty) text = SystemColors.ControlText;
        var accent = DXSkinColorHelper.GetDXSkinColor(DXSkinColors.FillColors.Primary,
            LookAndFeel.ActiveSkinName, LookAndFeel.ActiveSvgPaletteName);
        if (accent.IsEmpty || Math.Abs(accent.R - surface.R) + Math.Abs(accent.G - surface.G) + Math.Abs(accent.B - surface.B) < 120)
            accent = surface.GetBrightness() < 0.5F ? Color.FromArgb(110, 190, 250) : Color.FromArgb(0, 102, 180);
        var css = CardResource("PluginCard.Styles")
            .Replace("@surface", ColorTranslator.ToHtml(surface))
            .Replace("@text", ColorTranslator.ToHtml(text))
            .Replace("@border", ColorTranslator.ToHtml(Blend(surface, text, 0.22)))
            .Replace("@muted", ColorTranslator.ToHtml(Blend(surface, text, 0.78)))
            .Replace("@badge", ColorTranslator.ToHtml(Blend(surface, accent, 0.12)))
            .Replace("@success", surface.GetBrightness() < 0.5F ? "#7AD28F" : "#247A40")
            .Replace("@accent", ColorTranslator.ToHtml(accent));
        catalogView.HtmlTemplate.Styles = _activeCard.Styles = css;
        catalogView.EmptyViewHtmlTemplate.Styles = css;
    }
    private static Color Blend(Color first, Color second, double amount) => Color.FromArgb(
        (int)(first.R + (second.R - first.R) * amount), (int)(first.G + (second.G - first.G) * amount), (int)(first.B + (second.B - first.B) * amount));
    internal void Bind(AppWorkspace workspace, AppUiState? state)
    {
        _workspace = workspace;
        _state = state;
        _operation = new PageOperation(this, statusLabel);
    }
    internal void ConfigureSelection(Func<string, CancellationToken, Task> activate) => _activate = activate;
    internal void ConfigureLayoutPersistence(AppUiState? state) => _state = state;
    internal void RefreshCatalog()
    {
        if (_workspace?.Plugins is not IProductPluginSelection selection) { catalogGrid.DataSource = Array.Empty<PluginRow>(); return; }
        var focused = (catalogView.GetFocusedRow() as PluginRow)?.Id;
        catalogGrid.DataSource = selection.GetAvailablePlugins().Select(choice => new PluginRow(choice.Id, choice.DisplayName,
            choice.Version, choice.Id == selection.ActivePluginId && _workspace.Plugins.IsLoaded)).ToArray();
        catalogView.PopulateColumns();
        if (catalogGrid.DataSource is PluginRow[] rows)
        {
            var index = Array.FindIndex(rows, row => row.Id == focused);
            catalogView.FocusedRowHandle = index >= 0 ? index : Array.FindIndex(rows, row => row.IsActive);
            detailsLabel.Text = rows.Length == 0 ? "Keine Plugins installiert." : "Warenwirtschaftsplugin auswählen";
        }
        UpdateActivation();
    }
    private void Catalog_FocusedRowChanged(object sender, DevExpress.XtraGrid.Views.Base.FocusedRowChangedEventArgs e) => UpdateActivation();
    private void UpdateActivation()
    {
        var row = catalogView.GetFocusedRow() as PluginRow;
        capabilitiesButton.Enabled = row?.IsActive == true && _operation?.IsRunning != true;
        if (detailsGroup.Visible && row is not null)
            pluginDetailLabel.Text = row.Name + " · Version " + row.RawVersion + "\n" + row.Description
                + (row.IsActive ? "\nDie Fähigkeiten werden für die ausgewählten Cache-Mandanten geprüft." : "\nZum Prüfen der Fähigkeiten dieses Plugin zuerst aktivieren.");
    }
    private sealed record PluginRow(string Id, string Name, string RawVersion, bool IsActive)
    {
        public string Plugin => WebUtility.HtmlEncode(Name);
        public string Version => WebUtility.HtmlEncode("Version " + RawVersion);
        public string Status => IsActive ? "Aktiv" : IsDummy ? "Testbetrieb" : "Verfügbar";
        private bool IsDummy => Id.Contains("Dummy", StringComparison.OrdinalIgnoreCase);
        public string Description => IsDummy ? "Mit Beispieldaten testen – ohne Zugriff auf eine Warenwirtschaft." : "Kunden, Lieferanten und Mandanten aus der Warenwirtschaft verwenden.";
        public SvgImage Icon => IsDummy ? DummyIcon.Value : PluginIcon.Value;
    }
    private void Catalog_QueryItemTemplate(object sender, QueryItemTemplateEventArgs e)
    {
        if (e.Row is PluginRow { IsActive: true }) e.Template = _activeCard;
    }
    private void CatalogBack_Click(object? sender, EventArgs e) => pluginTabs.SelectedTabPage = installedTab;
    private void DetailsGroup_SizeChanged(object? sender, EventArgs e) => detailsScroll.AutoScrollMinSize = new Size(0, detailsGroup.Height);
    private async void Catalog_ElementMouseClick(object sender, ItemsViewHtmlElementMouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        await HandleCardActionAsync(e.ElementId, e.RowHandle);
    }
    private async void Catalog_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Enter) return;
        e.Handled = e.SuppressKeyPress = true;
        await HandleCardActionAsync(e.Control ? "activate" : "details", catalogView.FocusedRowHandle);
    }
    internal async Task HandleCardActionAsync(string action, int rowHandle)
    {
        if (_operation?.IsRunning == true || catalogView.GetRow(rowHandle) is not PluginRow row) return;
        catalogView.FocusedRowHandle = rowHandle;
        if (action == "details")
        {
            detailsTab.PageVisible = true;
            pluginTabs.SelectedTabPage = detailsTab;
            UpdateActivation();
            return;
        }
        if (action == "activate" && !row.IsActive) await ActivateRowAsync(row);
    }
    private async Task ActivateRowAsync(PluginRow row)
    {
        if (_operation is null || _activate is null) return;
        await _operation.RunAsync(async token =>
        {
            _switching = true;
            switchProgress.Visible = true;
            catalogGrid.Enabled = false; capabilitiesButton.Enabled = false; refreshButton.Enabled = false;
            ReportSwitchStatus("Wechsel zu " + row.Name + " wird vorbereitet …");
            switchProgress.Update();
            try
            {
                await Task.Yield();
                await _activate(row.Id, token);
                grid.DataSource = null;
                capabilitiesTab.PageVisible = false;
                detailsTab.PageVisible = false;
                pluginTabs.SelectedTabPage = installedTab;
                await UpdateStatusAsync(token);
                statusLabel.Text = "Plugin gewechselt und Auswahl gespeichert.";
            }
            finally
            {
                _switching = false;
                switchProgress.Visible = false;
                RefreshCatalog(); catalogGrid.Enabled = true; refreshButton.Enabled = true; UpdateActivation();
            }
        });
        if (!IsDisposed) UpdateActivation();
    }
    internal Task StopAsync() => _operation?.StopAsync() ?? Task.CompletedTask;
    private void DisposePage()
    {
        _operation?.Dispose();
        if (view.Columns.Count != 0) _state?.Save(view, "PluginCapabilities");
    }
    private async void Refresh_Click(object? sender, EventArgs e)
    {
        if (_operation is null) return;
        await _operation.RunAsync(async token =>
        {
            RefreshCatalog();
            await UpdateStatusAsync(token);
        });
    }
    private async Task UpdateStatusAsync(CancellationToken token)
    {
        if (_workspace?.Plugins.IsLoaded != true) { detailsLabel.Text = "Kein Plugin aktiv. Bitte einen Listeneintrag aktivieren."; return; }
        var plugin = await _workspace.GetPluginAsync(token);
        detailsLabel.Text = $"Aktiv: {plugin.Name}";
        statusLabel.Text = "Plugins verwalten Sie hier. Fachansichten gehören zum Arbeitsbereich des aktiven Plugins.";
    }
    private async void Capabilities_Click(object? sender, EventArgs e)
    {
        if (_operation is null || catalogView.GetFocusedRow() is not PluginRow { IsActive: true }) return;
        await _operation.RunAsync(async token =>
        {
            var settings = await _workspace!.Settings.LoadAsync(token);
            var authorized = (await _workspace.Directory.GetCompaniesAsync(token)).Select(company => company.Key).ToHashSet();
            var companies = settings.Options.Companies.Where(company => company.IncludeInPhoneCache && authorized.Contains(company.Company)).ToArray();
            if (companies.Length == 0)
            {
                statusLabel.Text = "Bitte zuerst in den Einstellungen Mandanten für den Telefon-Cache auswählen.";
                return;
            }
            var rows = new List<CapabilityRow>();
            foreach (var company in companies)
            {
                var report = await _workspace.Directory.GetCapabilitiesAsync(company.Company, token);
                rows.AddRange(Enum.GetValues<BusinessFeature>().Select(feature => new CapabilityRow(company.DisplayName ?? "Gespeicherter Mandant", FeatureName(feature),
                    report.GetAvailability(feature) switch { FeatureAvailability.Supported => "Unterstützt", FeatureAvailability.Partial => "Teilweise", _ => "Nicht verfügbar" }, report.Entries.GetValueOrDefault(feature)?.Details ?? "")));
            }
            grid.DataSource = rows;
            capabilitiesTab.PageVisible = true;
            pluginTabs.SelectedTabPage = capabilitiesTab;
            _state?.Restore(view, "PluginCapabilities");
            statusLabel.Text = "Fähigkeiten der konfigurierten Cache-Mandanten gelesen.";
        });
    }

    private sealed record CapabilityRow(string Mandant, string Bereich, string Verfügbarkeit, string Erläuterung);
    private static string FeatureName(BusinessFeature feature) => feature switch
    {
        BusinessFeature.Customers => "Kunden", BusinessFeature.Suppliers => "Lieferanten", BusinessFeature.ContactPersons => "Ansprechpartner",
        BusinessFeature.BillingAddresses => "Rechnungsanschriften", BusinessFeature.ShippingAddresses => "Lieferanschriften", BusinessFeature.AdditionalAddresses => "Zusatzanschriften",
        BusinessFeature.UpdateAddresses => "Anschriften ändern", BusinessFeature.OrdersByArticle => "Belege eines Artikels", BusinessFeature.ArticlesByOrder => "Artikel eines Belegs",
        BusinessFeature.OpenItems => "Offene Posten", BusinessFeature.PaymentHistory => "Zahlungsverhalten", BusinessFeature.Contacts => "Kontaktvorgänge",
        BusinessFeature.Documents => "Dokumente", BusinessFeature.Appointments => "Termine", BusinessFeature.Projects => "Projekte", BusinessFeature.MasterData => "Stammdaten", BusinessFeature.Orders => "Aufträge / Bestellungen",
        BusinessFeature.CustomerPriceLists => "Kundenpreislisten", BusinessFeature.TopArticles => "Top-Artikel", BusinessFeature.RevenueDevelopment => "Umsatzentwicklung",
        BusinessFeature.CreateParties => "Geschäftspartner anlegen", BusinessFeature.UpdateMasterData => "Stammdaten ändern", BusinessFeature.UpdateContactPersons => "Ansprechpartner ändern",
        BusinessFeature.UpdatePrices => "Preise ändern", BusinessFeature.CreateOrders => "Belege anlegen", BusinessFeature.CreateOrderNotes => "Belegnotizen anlegen",
        BusinessFeature.AssignDocuments => "Dokumente zuordnen", BusinessFeature.CreateProjects => "Projekte anlegen", _ => "Unbekannt"
    };
}

using DevExpress.XtraEditors;
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
    public PluginPage() { InitializeComponent(); }
    internal void Bind(AppWorkspace workspace, AppUiState? state)
    {
        _workspace = workspace;
        _state = state;
        _operation = new PageOperation(this, statusLabel);
    }
    internal void ConfigureSelection(Func<string, CancellationToken, Task> activate) => _activate = activate;
    internal void ConfigureLayoutPersistence(AppUiState? state) { _state = state; _state?.Restore(catalogView, "PluginCatalog-v1"); }
    internal void RefreshCatalog()
    {
        if (_workspace?.Plugins is not IProductPluginSelection selection) return;
        catalogGrid.DataSource = selection.GetAvailablePlugins().Select(choice => new PluginRow(choice.Id, choice.DisplayName,
            choice.Version, choice.Id == selection.ActivePluginId ? "Aktiv" : "Inaktiv")).ToArray();
        if (catalogGrid.DataSource is PluginRow[] rows) catalogView.FocusedRowHandle = Array.FindIndex(rows, row => row.Id == selection.ActivePluginId);
        UpdateActivation();
    }
    private void Catalog_FocusedRowChanged(object sender, DevExpress.XtraGrid.Views.Base.FocusedRowChangedEventArgs e) => UpdateActivation();
    private void UpdateActivation() => activateButton.Enabled = catalogView.GetFocusedRow() is PluginRow { Status: "Inaktiv" };
    private sealed record PluginRow(string Id, string Plugin, string Version, string Status);
    private async void Activate_Click(object? sender, EventArgs e)
    {
        if (_operation is null || _activate is null || catalogView.GetFocusedRow() is not PluginRow row) return;
        await _operation.RunAsync(async token =>
        {
            actions.Enabled = false; catalogGrid.Enabled = false;
            try
            {
                await _activate(row.Id, token);
                grid.DataSource = null;
                await UpdateStatusAsync(token);
                statusLabel.Text = "Plugin aktiviert und Auswahl gespeichert.";
            }
            finally { RefreshCatalog(); actions.Enabled = true; catalogGrid.Enabled = true; UpdateActivation(); }
        });
    }
    internal Task StopAsync() => _operation?.StopAsync() ?? Task.CompletedTask;
    private void DisposePage()
    {
        _operation?.Dispose();
        _state?.Save(catalogView, "PluginCatalog-v1");
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
        if (_operation is null) return;
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

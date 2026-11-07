using DevExpress.XtraEditors;
using WawiCallPro.App.Hosting;
using WawiCallPro.Interfaces.Capabilities;

namespace WawiCallPro.App.Views;

public partial class PluginPage : XtraUserControl
{
    private AppWorkspace? _workspace;
    private AppUiState? _state;
    private PageOperation? _operation;
    public PluginPage() { InitializeComponent(); }
    internal void Bind(AppWorkspace workspace, AppUiState? state)
    {
        _workspace = workspace;
        _state = state;
        _operation = new PageOperation(this, statusLabel);
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
            var plugin = await _workspace!.GetPluginAsync(token);
            detailsLabel.Text = $"Geladen: {plugin.Name}\nVersion: {plugin.Version} · Produktlinie: {plugin.ProductLine}\nBereitgestellte Fachansichten: {plugin.Views.Length}";
            statusLabel.Text = plugin.Views.IsEmpty
                ? "Das Plugin stellt noch keine Fach-Tabs bereit. Stammdatenabrufe und Mandantenauswahl sind getrennt verfügbar."
                : "Die Fachansichten werden für einen ausgewählten Kunden oder Lieferanten als Tabs geöffnet.";
        });
    }
    private async void Capabilities_Click(object? sender, EventArgs e)
    {
        if (_operation is null) return;
        await _operation.RunAsync(async token =>
        {
            var settings = await _workspace!.Settings.LoadAsync(token);
            var companies = settings.Options.Companies.Where(company => company.IncludeInPhoneCache).ToArray();
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
        BusinessFeature.Documents => "Dokumente", BusinessFeature.Appointments => "Termine", BusinessFeature.Projects => "Projekte", _ => "Unbekannt"
    };
}

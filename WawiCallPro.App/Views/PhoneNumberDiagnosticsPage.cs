using System.Collections.Immutable;
using DevExpress.XtraEditors;
using WawiCallPro.App.Hosting;
using WawiCallPro.Interfaces.Common;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.PhoneNumbers;

namespace WawiCallPro.App.Views;

/// <summary>Read-only local source observations. Each request owns its company, query and cancellation lifetime.</summary>
public partial class PhoneNumberDiagnosticsPage : XtraUserControl
{
    private IPhoneNumberDiagnostics? _diagnostics;
    private AppUiState? _state;
    private ImmutableArray<Company> _companies = [];
    private ImmutableArray<PhoneNumberDiagnostic> _items = [];
    private readonly List<Task> _requests = [];
    private Task _pending = Task.CompletedTask;
    private CancellationTokenSource? _request;
    private long _revision;
    private int _offset;
    private string _search = "";
    private bool _binding;
    private bool _stopping;
    private bool _arranging;
    internal Task PendingRequests => _pending;

    public PhoneNumberDiagnosticsPage()
    {
        InitializeComponent();
        FontChanged += Layout_Changed; SizeChanged += Layout_Changed; DpiChangedAfterParent += Layout_Changed;
        var appearance = LookAndFeel; appearance.StyleChanged += Layout_Changed;
        Disposed += (_, _) => appearance.StyleChanged -= Layout_Changed;
        Layout_Changed(this, EventArgs.Empty);
    }
    internal void Bind(IPhoneNumberDiagnostics diagnostics, AppUiState? state)
    {
        _diagnostics = diagnostics;
        ConfigureLayoutPersistence(state);
    }
    internal void ConfigureLayoutPersistence(AppUiState? state)
    {
        _state = state;
        _state?.Restore(view, "PhoneNumberDiagnostics-v1");
    }
    private void Layout_Changed(object? sender, EventArgs e)
    {
        if (_arranging || loadButton is null || IsDisposed) return;
        _arranging = true;
        try
        {
            foreach (var button in new[] { loadButton, refreshButton, previousButton, nextButton })
                button.Padding = new Padding(button.LogicalToDeviceUnits(12), button.LogicalToDeviceUnits(6), button.LogicalToDeviceUnits(12), button.LogicalToDeviceUnits(6));
            UiButtonSizing.Apply(refreshButton, previousButton, nextButton);
        }
        finally { _arranging = false; }
    }
    private void LoadCompanies_Click(object? sender, EventArgs e) => QueueRequest(true, 0);
    private void Refresh_Click(object? sender, EventArgs e) => QueueRequest(false, 0);
    private void Previous_Click(object? sender, EventArgs e) => QueueRequest(false, Math.Max(0, _offset - 100));
    private void Next_Click(object? sender, EventArgs e) => QueueRequest(false, checked(_offset + 100));
    private void Company_Changed(object? sender, EventArgs e)
    {
        companyEdit.ToolTip = companyEdit.Text;
        if (!_binding) QueueRequest(false, 0);
    }
    private void Find_Parse(object? sender, DevExpress.Data.ParseFindPanelTextEventArgs e)
    {
        // The service searches every stored page. Applying a second local filter would hide valid remote matches.
        e.Handled = true;
        e.SetFindCriteria(null);
        var search = e.FindPanelText?.Trim() ?? "";
        if (_binding || _search == search) return;
        _search = search;
        QueueRequest(false, 0);
    }
    private void QueueRequest(bool loadCompanies, int offset)
    {
        if (_diagnostics is null || _stopping) return;
        _request?.Cancel();
        var request = new CancellationTokenSource(); _request = request;
        var revision = ++_revision;
        var company = companyEdit.EditValue as CompanyKey;
        _offset = offset; ClearRows();
        statusLabel.Text = "Diagnose wird geladen …";
        _requests.RemoveAll(task => task.IsCompleted);
        _requests.Add(LoadAsync(loadCompanies, company, offset, _search, revision, request));
        _pending = Task.WhenAll(_requests.ToArray());
    }
    private bool IsCurrent(long revision) => !_stopping && !IsDisposed && revision == _revision;
    private async Task LoadAsync(bool loadCompanies, CompanyKey? company, int offset, string search, long revision, CancellationTokenSource request)
    {
        try
        {
            if (loadCompanies)
            {
                var companies = await _diagnostics!.GetCompaniesAsync(request.Token);
                if (!IsCurrent(revision) || request.IsCancellationRequested) return;
                _companies = companies;
                _binding = true;
                try
                {
                    companyEdit.Properties.DataSource = companies.ToArray();
                    companyEdit.EditValue = companies.Any(item => item.Key == company) ? company : null;
                    company = companyEdit.EditValue as CompanyKey;
                }
                finally { _binding = false; }
            }
            if (company is null)
            {
                if (IsCurrent(revision)) statusLabel.Text = _companies.IsEmpty
                    ? "Keine lokalen Mandanten geladen. Zuerst den Telefon-Cache einrichten und aktualisieren."
                    : "Mandant auswählen. Auch abgewählte Cachequellen bleiben für die Diagnose verfügbar.";
                return;
            }
            var page = await _diagnostics!.GetPageAsync(company, new PageRequest(offset), search, request.Token);
            if (!IsCurrent(revision) || request.IsCancellationRequested || companyEdit.EditValue as CompanyKey != company) return;
            _items = page.Items;
            grid.DataSource = page.Items.Select(item => new DiagnosticRow(item.Id, item.OccurredAtUtc.LocalDateTime,
                item.OriginalValue, item.Source.RecordType + " · " + item.Source.FieldName, item.Reason, item.Code)).ToArray();
            previousButton.Enabled = offset > 0; nextButton.Enabled = page.HasMore;
            statusLabel.Text = page.Items.IsEmpty ? "Keine gespeicherten Beobachtungen für diese Auswahl."
                : $"{offset + 1}–{offset + page.Items.Length} · Historische Beobachtungen; eine Quellkorrektur entfernt frühere Einträge nicht.";
            ShowFocused();
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested) { }
        catch (Exception exception) { if (IsCurrent(revision)) statusLabel.Text = PageOperation.Describe(exception); }
        finally { if (ReferenceEquals(_request, request)) _request = null; request.Dispose(); }
    }
    private void ClearRows()
    {
        _items = []; grid.DataSource = Array.Empty<DiagnosticRow>();
        detailsEdit.Text = "Eine Beobachtung auswählen, um den vollständigen Quellbezug zu sehen und zu kopieren.";
        previousButton.Enabled = nextButton.Enabled = false;
    }
    private void View_FocusedRowChanged(object sender, DevExpress.XtraGrid.Views.Base.FocusedRowChangedEventArgs e) => ShowFocused();
    private void ShowFocused()
    {
        if (view.GetFocusedRow() is not DiagnosticRow row || _items.FirstOrDefault(item => item.Id == row.Id) is not { } item) return;
        var company = _companies.FirstOrDefault(value => value.Key == item.Party.Company);
        detailsEdit.Text = $"Mandant: {company?.DisplayName}\r\nEdition: {item.Party.Company.ProductLine} · Installation: {item.Party.Company.InstallationId} · Firmen-ID: {item.Party.Company.ExternalId}\r\n"
            + $"Geschäftspartner: {item.Party.Kind} · ID: {item.Party.ExternalId} · Besitzer-ID: {item.Party.OwnerExternalId ?? "–"}\r\n"
            + $"Quelle: {item.Source.RecordType} · Datensatz: {item.Source.RecordId} · Feld: {item.Source.FieldName} · Nummernart: {item.Kind}\r\n"
            + $"Originalwert: {item.OriginalValue}\r\nGrund: {item.Reason} ({item.Code})\r\nBeobachtet: {item.OccurredAtUtc.LocalDateTime:g}";
    }
    internal async Task StopAsync()
    {
        _stopping = true; ++_revision; _request?.Cancel();
        await _pending;
    }
    private void DisposePage()
    {
        _stopping = true; ++_revision; _request?.Cancel();
        _state?.Save(view, "PhoneNumberDiagnostics-v1");
    }
    private sealed record DiagnosticRow(Guid Id, DateTime Beobachtet, string Originalwert, string Quelle, string Grund, string Code)
    { public override string ToString() => nameof(DiagnosticRow); }
}

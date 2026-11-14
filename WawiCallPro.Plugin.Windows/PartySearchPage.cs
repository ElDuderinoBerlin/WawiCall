using System.Collections.Immutable;
using DevExpress.XtraEditors;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Parties;

namespace WawiCallPro.Plugin.Windows;

/// <summary>Plugin-owned, detached party search and read-only master data. Never changes the global company selection.</summary>
public partial class PartySearchPage : XtraUserControl
{
    private readonly IBusinessDirectory? _directory;
    private readonly IPartySearch? _search;
    private readonly IPartySearchPresentation? _presentation;
    private ImmutableArray<Company> _companies = [];
    private readonly List<Task> _tasks = [];
    private CancellationTokenSource? _catalogCancellation, _searchCancellation, _detailCancellation;
    private bool _stopped, _disposed, _binding;
    private Task? _stopTask;
    private PartySearchQuery? _resultQuery;
    private CompanyKey? _lastCompany;
    private string _lastText = "";
    private int _lastRole = -1;

    public PartySearchPage()
    {
        InitializeComponent();
        FontChanged += Appearance_Changed; DpiChangedAfterParent += Appearance_Changed;
        LookAndFeel.StyleChanged += Appearance_Changed;
    }
    public PartySearchPage(IBusinessDirectory directory, IPartySearch search, IPartySearchPresentation presentation) : this()
    {
        _directory = directory; _search = search; _presentation = presentation;
        presentation.Restore(view); Appearance_Changed(this, EventArgs.Empty);
    }
    private void Appearance_Changed(object? sender, EventArgs e) => _presentation?.SizeButtons(reloadButton, openButton);
    private async void Page_Load(object? sender, EventArgs e) { searchTimer.Start(); await ReloadAsync(); }
    private Task Track(Func<Task> action)
    {
        if (_stopped || _disposed) return Task.CompletedTask;
        _tasks.RemoveAll(task => task.IsCompleted);
        var task = action(); _tasks.Add(task); return task;
    }
    private static CancellationTokenSource Renew(ref CancellationTokenSource? lane)
    { Cancel(lane); return lane = new(); }
    private static void Cancel(CancellationTokenSource? lane) { if (lane is not null) { lane.Cancel(); lane.Dispose(); } }
    private void ClearResults()
    { _resultQuery = null; grid.DataSource = Array.Empty<SearchRow>(); openButton.Enabled = false; }

    /// <summary>Reloads permitted companies, keeping only an explicitly chosen identity still present in the catalog.</summary>
    public Task ReloadAsync() => Track(ReloadCoreAsync);
    private async Task ReloadCoreAsync()
    {
        if (_directory is null) return;
        var request = Renew(ref _catalogCancellation); var token = request.Token;
        Cancel(_searchCancellation); _searchCancellation = null;
        Cancel(_detailCancellation); _detailCancellation = null;
        var selected = companyEdit.EditValue as CompanyKey;
        ClearResults(); companyEdit.Enabled = roleEdit.Enabled = false;
        statusLabel.Text = "Mandanten werden geladen …";
        try
        {
            var companies = await _directory.GetCompaniesAsync(token);
            token.ThrowIfCancellationRequested();
            if (_stopped || request != _catalogCancellation) return;
            if (companies.IsDefault || companies.Any(company => company?.Key is null || string.IsNullOrWhiteSpace(company.DisplayName))
                || companies.Select(company => company.Key).Distinct().Count() != companies.Length)
                throw new InvalidOperationException("Invalid company catalog.");
            _binding = true;
            try
            {
                _companies = companies;
                companyEdit.Properties.DataSource = companies.ToArray();
                companyEdit.EditValue = companies.Any(company => company.Key == selected) ? selected : null;
            }
            finally { _binding = false; }
            _lastRole = -1;
            statusLabel.Text = companies.IsEmpty ? "Keine freigegebenen Mandanten verfügbar." : "Mandant auswählen. Strg+F blendet die Suche ein.";
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception)
        {
            if (_stopped || token.IsCancellationRequested || request != _catalogCancellation) return;
            _binding = true;
            try { _companies = []; companyEdit.Properties.DataSource = null; companyEdit.EditValue = null; }
            finally { _binding = false; }
            statusLabel.Text = "Mandanten konnten nicht geladen werden. Erreichbarkeit und Lexware-Freigabe prüfen und erneut laden.";
        }
        finally { if (!_stopped && request == _catalogCancellation) companyEdit.Enabled = roleEdit.Enabled = true; }
    }

    /// <summary>Searches the selected company using the grid's built-in find text; no client-side result truncation.</summary>
    public Task SearchAsync() => Track(SearchCoreAsync);
    private async Task SearchCoreAsync()
    {
        if (_search is null) return;
        var request = Renew(ref _searchCancellation); var token = request.Token;
        Cancel(_detailCancellation); _detailCancellation = null;
        _lastCompany = companyEdit.EditValue as CompanyKey; _lastText = view.FindFilterText ?? ""; _lastRole = roleEdit.SelectedIndex;
        ClearResults();
        if (_lastCompany is null || !_companies.Any(company => company.Key == _lastCompany)) { statusLabel.Text = "Einen Mandanten auswählen."; return; }
        if (string.IsNullOrWhiteSpace(_lastText)) { statusLabel.Text = "Strg+F drücken und Name, Nummer oder E-Mail eingeben."; return; }
        try
        {
            var query = new PartySearchQuery(_lastCompany, _lastText, _lastRole == 0 ? null : (PartyKind)_lastRole);
            statusLabel.Text = "Geschäftspartner werden gesucht …";
            var result = await _search.SearchAsync(query, token);
            token.ThrowIfCancellationRequested();
            if (_stopped || request != _searchCancellation || !CurrentQueryMatches(query)) return;
            if (result is null || result.Query != query || result.Items.IsDefault
                || result.Items.Any(party => party?.Key is null || party.Key.Company != query.Company
                    || query.Kind is { } kind && party.Key.Kind != kind || string.IsNullOrWhiteSpace(party.DisplayName))
                || result.Items.Select(party => party.Key).Distinct().Count() != result.Items.Length)
                throw new PartySearchException(PartySearchFailure.InvalidResponse);
            _resultQuery = query;
            grid.DataSource = result.Items.Select(party => new SearchRow(party)).ToArray();
            statusLabel.Text = result.Items.IsEmpty ? "Keine passenden Geschäftspartner gefunden." : result.Items.Length + " Treffer. Stammdaten per Doppelklick oder Button öffnen.";
            UpdateOpen();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error)
        {
            if (_stopped || token.IsCancellationRequested || request != _searchCancellation) return;
            statusLabel.Text = error is PartySearchException ? error.Message : error is ArgumentException
                ? "Suchtext prüfen: höchstens 200 Zeichen ohne Steuerzeichen." : "Die Suche ist fehlgeschlagen. Erreichbarkeit und Lexware-Freigabe prüfen.";
        }
    }
    private bool CurrentQueryMatches(PartySearchQuery query) => companyEdit.EditValue as CompanyKey == query.Company
        && roleEdit.SelectedIndex == (query.Kind is { } role ? (int)role : 0) && (view.FindFilterText ?? "").Trim() == query.Text;
    private void UpdateOpen() => openButton.Enabled = !_stopped && _resultQuery is { } query && CurrentQueryMatches(query) && view.GetFocusedRow() is SearchRow;

    /// <summary>Opens one exact source record in the read-only master-data tab without rebinding other workspaces.</summary>
    public Task OpenFocusedAsync() => Track(OpenCoreAsync);
    private async Task OpenCoreAsync()
    {
        if (_directory is null || _resultQuery is not { } query || !CurrentQueryMatches(query) || view.GetFocusedRow() is not SearchRow row) return;
        var key = row.Party.Key;
        var companyName = _companies.Single(company => company.Key == key.Company).DisplayName;
        var request = Renew(ref _detailCancellation); var token = request.Token;
        detailsTab.PageEnabled = false; detailText.Text = ""; openButton.Enabled = false;
        try
        {
            var party = await _directory.GetPartyAsync(key, token);
            token.ThrowIfCancellationRequested();
            if (_stopped || request != _detailCancellation || !CurrentQueryMatches(query)) return;
            if (party is null) { statusLabel.Text = "Der gewählte Datensatz ist nicht mehr vorhanden. Bitte erneut suchen."; return; }
            if (party.Key != key || string.IsNullOrWhiteSpace(party.DisplayName) || party.Addresses.IsDefault || party.Phones.IsDefault
                || party.Addresses.Any(address => address is null || address.Phones.IsDefault) || party.Phones.Any(phone => phone is null))
                throw new InvalidOperationException("Foreign or invalid party details.");
            detailTitle.Text = companyName + " · " + RoleText(key.Kind) + " · " + party.DisplayName;
            var lines = new List<string> { "Nummer: " + (party.Number ?? "–"), "E-Mail: " + (party.Email ?? "–"), "Status: " + (party.IsInactive ? "Inaktiv" : "Aktiv") };
            lines.AddRange(party.Phones.Select(phone => PhoneText(phone.Kind) + ": " + phone.OriginalValue));
            foreach (var address in party.Addresses)
            {
                lines.Add(""); lines.Add(AddressText(address.Kind));
                lines.AddRange(new[] { address.CompanyName, string.Join(" ", new[] { address.FirstName, address.LastName }.Where(value => !string.IsNullOrWhiteSpace(value))),
                    string.Join(" ", new[] { address.Street, address.HouseNumber }.Where(value => !string.IsNullOrWhiteSpace(value))), address.AddressLine2,
                    string.Join(" ", new[] { address.PostalCode, address.City }.Where(value => !string.IsNullOrWhiteSpace(value))), address.Country ?? address.CountryCode, address.Email }.OfType<string>().Where(value => !string.IsNullOrWhiteSpace(value)));
                lines.AddRange(address.Phones.Where(phone => phone is not null).Select(phone => PhoneText(phone.Kind) + ": " + phone.OriginalValue));
            }
            detailText.Lines = lines.ToArray(); detailsTab.PageEnabled = true; tabs.SelectedTabPage = detailsTab;
            statusLabel.Text = "Stammdaten gelesen. Die Ansicht verändert keine Quelldaten.";
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception) { if (_stopped || token.IsCancellationRequested || request != _detailCancellation) return; statusLabel.Text = "Stammdaten konnten nicht geladen werden. Bitte erneut öffnen."; }
        finally { if (!_stopped && request == _detailCancellation) UpdateOpen(); }
    }
    private static string PhoneText(PhoneKind kind) => kind switch { PhoneKind.Mobile => "Mobil", PhoneKind.Fax => "Fax", _ => "Telefon" };
    private static string AddressText(AddressKind kind) => kind switch { AddressKind.Billing => "Rechnungsanschrift", AddressKind.Shipping => "Lieferanschrift", AddressKind.Additional => "Zusatzanschrift", _ => "Kontaktanschrift" };
    private static string RoleText(PartyKind kind) => kind switch
    { PartyKind.Customer => "Kunde", PartyKind.Supplier => "Lieferant", PartyKind.CustomerContact => "Kundenkontakt", _ => "Lieferantenkontakt" };
    private sealed record SearchRow(Party Party)
    { public string Name => Party.DisplayName; public string? Number => Party.Number; public string? Email => Party.Email; public string Role => RoleText(Party.Key.Kind); }
    private void Selection_Changed(object? sender, EventArgs e)
    {
        if (_binding || _stopped) return;
        Cancel(_searchCancellation); _searchCancellation = null; Cancel(_detailCancellation); _detailCancellation = null;
        ClearResults(); _lastRole = -1;
    }
    private async void Search_Tick(object? sender, EventArgs e)
    {
        if (!_stopped && companyEdit.Enabled && (_lastCompany != companyEdit.EditValue as CompanyKey || _lastRole != roleEdit.SelectedIndex || _lastText != (view.FindFilterText ?? "")))
            await SearchAsync();
    }
    private async void Reload_Click(object? sender, EventArgs e) => await ReloadAsync();
    private async void Open_Click(object? sender, EventArgs e) => await OpenFocusedAsync();
    private async void View_DoubleClick(object? sender, EventArgs e)
    { var point = grid.PointToClient(Control.MousePosition); if (view.CalcHitInfo(point).InRow) await OpenFocusedAsync(); }
    private void View_FocusedRowChanged(object sender, DevExpress.XtraGrid.Views.Base.FocusedRowChangedEventArgs e) => UpdateOpen();
    protected override bool ProcessCmdKey(ref Message message, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.F)) { tabs.SelectedTabPage = resultsTab; if (view.IsFindPanelVisible) view.HideFindPanel(); else view.ShowFindPanel(); return true; }
        return base.ProcessCmdKey(ref message, keyData);
    }
    /// <summary>Stops new UI actions and waits for all source operations, including non-cooperative adapters.</summary>
    public Task StopAsync()
    {
        if (_stopTask is not null) return _stopTask;
        if (_disposed) return Task.WhenAll(_tasks.ToArray());
        _stopped = true; searchTimer.Stop(); selectors.Enabled = actions.Enabled = false;
        Cancel(_catalogCancellation); _catalogCancellation = null; Cancel(_searchCancellation); _searchCancellation = null;
        Cancel(_detailCancellation); _detailCancellation = null;
        return _stopTask = Task.WhenAll(_tasks.ToArray());
    }
    private void DisposePage()
    {
        if (_disposed) return;
        _disposed = true; _stopped = true;
        Cancel(_catalogCancellation); Cancel(_searchCancellation); Cancel(_detailCancellation);
        LookAndFeel.StyleChanged -= Appearance_Changed;
        _presentation?.Save(view);
    }
}

using DevExpress.XtraTab;
using WawiCallPro.App.Hosting;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Parties;

namespace WawiCallPro.App.Views;

public partial class FrmDial
{
    private IPartySearch? _phoneSearch;
    private AppUiState? _searchLayoutState;
    private CancellationTokenSource? _phoneSearchCancellation;
    private readonly List<Task> _phoneSearchTasks = [];
    private PartySearchQuery? _lastPhoneQuery;
    private PartySearchQuery? _displayedPhoneQuery;
    private bool _phoneSearchChecked;
    private bool _restoredDialTab;
    private CompanyKey? _lastPhoneCompany;
    private string? _lastPhoneText;

    internal void ConfigurePhoneSearch(IPartySearch? search, AppUiState? state)
    {
        _phoneSearch = search;
        _searchLayoutState = state;
        searchTab.PageVisible = search is not null;
        _restoredDialTab = state?.AttachTabs(dialTabs, "Dial") == true;
        if (search is null) return;
        searchGrid.ForceInitialize();
        state?.Restore(searchView, "DialPhoneSearch_v1");
    }

    internal void SelectPhoneSearch()
    {
        if (!_restoredDialTab && _phoneSearch is not null && CurrentConnectedCall() is null) dialTabs.SelectedTabPage = searchTab;
    }

    private void DialTab_Changed(object? sender, TabPageChangedEventArgs e)
    {
        AcceptButton = e.Page == searchTab ? null : dialButton;
        if (e.Page == searchTab) phoneSearchTimer.Start();
        else
        {
            phoneSearchTimer.Stop();
            CancelPhoneSearch();
            _phoneSearchChecked = false;
        }
        Layout_Changed(this, EventArgs.Empty);
    }

    private void PhoneSearchTimer_Tick(object? sender, EventArgs e) => _ = SearchPhoneNumbersAsync();

    internal Task SearchPhoneNumbersAsync()
    {
        if (_disposed || _stopping || _phoneSearch is null || dialTabs.SelectedTabPage != searchTab) return Task.CompletedTask;
        var company = SelectedCompany;
        var text = (searchView.FindFilterText ?? "").Trim();
        var hasCompany = company is not null && _companies.Any(item => item.Key == company);
        var query = company is not null && hasCompany
            && text.Length is >= 2 and <= 200 && !text.Any(char.IsControl) ? new PartySearchQuery(company, text) : null;
        if (_phoneSearchChecked && _lastPhoneCompany == company && _lastPhoneText == text && _lastPhoneQuery == query)
            return Task.CompletedTask;
        _phoneSearchChecked = true;
        _lastPhoneCompany = company;
        _lastPhoneText = text;
        CancelPhoneSearch();
        _lastPhoneQuery = query;
        _displayedPhoneQuery = null;
        searchGrid.DataSource = null;
        choosePhoneButton.Enabled = false;
        if (query is null)
        {
            searchStatus.Text = !hasCompany ? "Für die Suche genau einen Mandanten auswählen." : "Mindestens zwei Zeichen der Rufnummer oder des Namens eingeben.";
            return Task.CompletedTask;
        }
        var request = _phoneSearchCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _phoneSearchTasks.RemoveAll(task => task.IsCompleted);
        var task = LoadPhoneNumbersAsync(query, request);
        _phoneSearchTasks.Add(task);
        return task;
    }

    private async Task LoadPhoneNumbersAsync(PartySearchQuery query, CancellationTokenSource request)
    {
        using var waiting = new UiWaitCursor(searchGrid);
        var token = request.Token;
        searchStatus.Text = "Rufnummern werden gesucht …";
        try
        {
            var result = await _phoneSearch!.SearchAsync(query, token);
            token.ThrowIfCancellationRequested();
            if (!PhoneQueryIsCurrent(query) || request != _phoneSearchCancellation) return;
            if (result.Query != query || result.Items.IsDefault || result.Items.Any(party => party.Key.Company != query.Company))
                throw new PartySearchException(PartySearchFailure.InvalidResponse);
            var rows = result.Items.Where(party => !party.IsInactive).SelectMany(party =>
            {
                var phones = PartyPhoneSearch.DialablePhones(party).ToArray();
                var matching = phones.Where(phone => PartyPhoneSearch.Matches(phone, query.Text)).ToArray();
                return (matching.Length == 0 ? phones : matching).Select(phone => new PhoneRow(party.DisplayName, phone.OriginalValue,
                    party.Key.Kind switch
                    {
                        PartyKind.Customer => "Kunde", PartyKind.Supplier => "Lieferant",
                        PartyKind.CustomerContact or PartyKind.SupplierContact => "Kontakt", _ => ""
                    }));
            }).Distinct().ToArray();
            _displayedPhoneQuery = query;
            searchGrid.DataSource = rows;
            searchStatus.Text = rows.Length switch
            {
                0 => "Keine wählbaren Rufnummern gefunden.",
                1 => "1 Rufnummer",
                _ => rows.Length + " Rufnummern"
            };
            UpdatePhoneChoice();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception)
        {
            if (!token.IsCancellationRequested && PhoneQueryIsCurrent(query))
                searchStatus.Text = "Rufnummernsuche fehlgeschlagen. Suchtext oder Lexware-Zugang prüfen.";
        }
    }

    private bool PhoneQueryIsCurrent(PartySearchQuery query) => !_disposed && !_stopping
        && dialTabs.SelectedTabPage == searchTab && SelectedCompany == query.Company
        && (searchView.FindFilterText ?? "").Trim() == query.Text;

    private void CancelPhoneSearch()
    {
        _phoneSearchCancellation?.Cancel();
        _phoneSearchCancellation?.Dispose();
        _phoneSearchCancellation = null;
    }

    private void SearchView_FocusedRowChanged(object sender, DevExpress.XtraGrid.Views.Base.FocusedRowChangedEventArgs e) => UpdatePhoneChoice();
    private void SearchView_CustomRowFilter(object sender, DevExpress.XtraGrid.Views.Base.RowFilterEventArgs e)
    {
        // The source has already filtered the rows, including matches across phone-number separators.
        e.Visible = _displayedPhoneQuery is { } query && PhoneQueryIsCurrent(query);
        e.Handled = true;
    }
    private void UpdatePhoneChoice() => choosePhoneButton.Enabled = _displayedPhoneQuery is { } query
        && PhoneQueryIsCurrent(query) && searchView.GetFocusedRow() is PhoneRow;
    private void ChoosePhone_Click(object? sender, EventArgs e) => ChoosePhone();
    private void SearchView_DoubleClick(object? sender, EventArgs e)
    {
        if (searchView.CalcHitInfo(searchGrid.PointToClient(Cursor.Position)).InRowCell) ChoosePhone();
    }

    internal void ChoosePhone()
    {
        if (_displayedPhoneQuery is not { } query || !PhoneQueryIsCurrent(query) || searchView.GetFocusedRow() is not PhoneRow row) return;
        destinationEdit.Text = row.Number;
        dialTabs.SelectedTabPage = dialTab;
        destinationEdit.Focus();
    }

    private sealed record PhoneRow(string Name, string Number, string Role);
}

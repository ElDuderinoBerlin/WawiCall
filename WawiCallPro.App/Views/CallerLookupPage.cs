using System.Collections.Immutable;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid.Views.Base;
using WawiCallPro.App.Hosting;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Interfaces.PhoneNumbers;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.App.Views;

/// <summary>Explicit number lookup and detached selection. No change of the active company or plugin documents.</summary>
public partial class CallerLookupPage : XtraUserControl
{
    private CallSnapshot? _boundCall;
    private AppWorkspace? _workspace;
    private AppUiState? _state;
    private PageOperation? _operation;
    private CancellationTokenSource? _request;
    private CallerMatchContext _context = new();
    private CallerMatchDecision? _decision;
    private string? _lastNumber;
    private bool _binding;
    private bool _arrangingButtons;
    private Func<CallerMatchDecision, CancellationToken, Task>? _accept;
    private bool _callEnded;
    private PhoneLineContext? _boundLine;
    public CallerLookupPage()
    {
        InitializeComponent();
        UiButtonSizing.Attach(this);
        FontChanged += Layout_Changed;
        SizeChanged += Layout_Changed;
        actions.SizeChanged += Layout_Changed;
        DpiChangedAfterParent += Layout_Changed;
        var appearance = LookAndFeel;
        appearance.StyleChanged += Layout_Changed;
        Disposed += (_, _) => appearance.StyleChanged -= Layout_Changed;
        SizeButtons();
    }
    private void Layout_Changed(object? sender, EventArgs e) => SizeButtons();
    private void SizeButtons()
    {
        if (_arrangingButtons || searchButton is null || actions.ColumnStyles.Count != 3 || IsDisposed) return;
        _arrangingButtons = true;
        try
        {
            var buttons = new[] { searchButton, cancelButton, chooseButton };
            var size = UiButtonSizing.Apply(buttons);
            foreach (ColumnStyle column in actions.ColumnStyles) { column.SizeType = SizeType.Absolute; column.Width = size.Width + searchButton.Margin.Horizontal; }
        }
        finally { _arrangingButtons = false; }
    }

    internal void Bind(AppWorkspace workspace, AppUiState? state, CallerMatchContext? context = null)
    {
        _workspace = workspace; _state = state;
        _context = (context ?? new(workspace.Companies.Current.Company?.Key)) with { LineAssignment = null };
        _operation = new(this, statusLabel);
        state?.AttachTabs(tabs, "CallerLookup");
        _state?.Restore(view, "CallerLookup-v1");
        view.ClearGrouping();
        groupColumn.GroupIndex = 0;
        groupColumn.Visible = false;
        searchButton.Enabled = workspace.CanUseBusinessDirectory;
    }
    internal CallerMatchDecision? Decision => _decision;
    internal void BindNotes(CallNotesSession notes)
    {
        notesPanel.Bind(notes);
        notesTab.PageVisible = true;
        _state?.RestoreTab(tabs, "CallerLookup");
    }
    internal void BindCall(CallSnapshot call, Func<CallerMatchDecision, CancellationToken, Task> accept)
    {
        _boundCall = call;
        _accept = accept;
        _boundLine = (_workspace?.Telephony as ICallLineContextProvider)?.GetLineContext(call.Id);
        filters.Visible = false;
        statusLabel.Text = "";
    }

    internal void ObserveCallAddress(CallSnapshot call)
    {
        if (_boundCall?.Id == call.Id) _boundCall = call;
    }

    internal PhoneNumberInterpretation? InterpretAddress(CallAddress address)
    {
        if (address.Presentation == NumberPresentation.Available && string.IsNullOrWhiteSpace(address.OriginalValue))
            address = address with { OriginalValue = address.E164 };
        return _workspace?.PhoneInterpreter.InterpretIncoming(address, _boundLine);
    }

    internal void DisplayCallStatus(string status)
    {
        callStatusLabel.Text = status;
        callStatusLabel.Visible = status.Length != 0;
    }

    internal async Task EndCallAsync()
    { _callEnded = true; await StopAsync(); chooseButton.Enabled = searchButton.Enabled = cancelButton.Enabled = false; statusLabel.Text = "Die bisherige Auswahl bleibt in der Historie erhalten."; }
    internal Task StopAsync() => _operation?.StopAsync() ?? Task.CompletedTask;
    private void DisposePage()
    {
        _request?.Cancel(); _request?.Dispose();
        _operation?.Dispose();
        _state?.Save(view, "CallerLookup-v1");
    }
    private async void Search_Click(object? sender, EventArgs e) => await SearchAsync();
    internal async Task SearchAsync()
    {
        if (_operation is null || _workspace is null || _operation.IsRunning || _callEnded) return;
        if (!_workspace.CanUseBusinessDirectory)
        {
            searchButton.Enabled = chooseButton.Enabled = cancelButton.Enabled = false;
            statusLabel.Text = "Rufnummernsuche ist gesperrt. Telefonie bleibt verfügbar; Details stehen im Log-Fenster.";
            return;
        }
        var address = _boundCall?.RemoteAddress ?? new(NumberPresentation.Available, numberEdit.Text.Trim());
        var incoming = InterpretAddress(address);
        if (incoming is not { Kind: PhoneNumberAddressKind.Public })
        {
            _decision = null; grid.DataSource = Array.Empty<CandidateRow>();
            selectionLabel.Text = "Keine Zuordnung."; chooseButton.Enabled = false;
            statusLabel.Text = _boundCall is null
                ? "Bitte eine gültige Rufnummer mit Ländervorwahl eingeben."
                : "Für diese Rufnummer ist keine öffentliche Anrufzuordnung möglich.";
            return;
        }
        var number = incoming.Address.OriginalValue ?? "";
        await _operation.RunAsync(async token =>
        {
            using var request = CancellationTokenSource.CreateLinkedTokenSource(token);
            _request = request;
            searchButton.Enabled = numberEdit.Enabled = chooseButton.Enabled = false;
            cancelButton.Enabled = true;
            var previous = number == _lastNumber ? _decision?.Selection : null;
            _decision = null; grid.DataSource = Array.Empty<CandidateRow>();
            selectionLabel.Text = "Suchstand wird geprüft …";
            try
            {
                await _workspace.PhoneSearch.RefreshAsync(request.Token);
                var result = await Task.Run(() => _workspace.PhoneSearch.Find(incoming, request.Token), request.Token);
                request.Token.ThrowIfCancellationRequested();
                _lastNumber = number;
                var context = _context with { ActiveCompany = _workspace.Companies.Current.Company?.Key };
                var decision = _workspace.CallerDecisions.Evaluate(result, context, previous);
                Display(_boundCall is { } boundCall ? AppCallAssociation.RespectDialCompany(decision, boundCall) : decision);
                if (_accept is not null) await _accept(_decision!, request.Token);
            }
            catch (OperationCanceledException) when (request.IsCancellationRequested)
            { statusLabel.Text = "Suche abgebrochen. Keine neue Zuordnung übernommen."; selectionLabel.Text = "Keine Zuordnung."; }
            finally
            {
                _request = null;
                searchButton.Enabled = numberEdit.Enabled = true;
                cancelButton.Enabled = false;
            }
        });
        if (!IsDisposed && !Disposing) UpdateFocusedDetails();
    }
    private void Cancel_Click(object? sender, EventArgs e) => _request?.Cancel();
    private async void Choose_Click(object? sender, EventArgs e)
    {
        if (_workspace is null || !_workspace.CanUseBusinessDirectory || _decision is null || _callEnded || _operation?.IsRunning == true || view.GetFocusedRow() is not CandidateRow row) return;
        var current = _workspace.PhoneSearch.GetStatus();
        if (current.Generation != _decision.IndexGeneration || current.State is not (PhoneSearchScopeState.Ready or PhoneSearchScopeState.Partial))
        { statusLabel.Text = "Suchstand geändert oder abgelaufen. Bitte erneut suchen, bevor ein Datensatz übernommen wird."; chooseButton.Enabled = false; return; }
        _decision = _decision with { Lookup = _decision.Lookup with { IsComplete = current.IsComplete }, Groups = _decision.Groups.Select(group =>
            group with { Company = current.Companies.Single(company => company.Company == group.Company.Company) }).ToImmutableArray() };
        Display(_workspace.CallerDecisions.Choose(_decision, row.Candidate.Party));
        if (_accept is not null && _operation is not null)
        {
            chooseButton.Enabled = false;
            await _operation.RunAsync(async token => { await _accept(_decision!, token); if (!_callEnded) statusLabel.Text = "Zuordnung für diesen Anruf gespeichert."; });
            if (!IsDisposed && !Disposing) UpdateFocusedDetails();
        }
    }
    internal void Display(CallerMatchDecision decision)
    {
        _decision = decision;
        _binding = true;
        try
        {
            var rows = decision.Groups.SelectMany((group, index) => group.Candidates.Select(candidate => new CandidateRow(index,
                group.Company.DisplayName, candidate, group.Company))).ToArray();
            grid.DataSource = rows;
            view.ExpandAllGroups();
            if (decision.Selection is { } selected)
                for (var handle = 0; handle < view.DataRowCount; handle++)
                    if (view.GetRow(handle) is CandidateRow row && row.Candidate.Party == selected.Party) { view.FocusedRowHandle = handle; break; }
            statusLabel.Text = DecisionText(decision.Kind);
            selectionLabel.Text = decision.Selection is { } selection
                ? "Zuordnung: " + string.Join(" · ", rows.Where(row => row.Candidate.Party == selection.Party).Select(row => row.Firma + " · " + row.Name + " · " + row.Rolle))
                    + (selection.Kind == CallerMatchSelectionKind.Manual ? " (manuell" : " (automatisch")
                    + (selection.IsProvisional ? ", vorläufig)" : ")")
                : "Keine Zuordnung übernommen. Einen Datensatz markieren und ausdrücklich übernehmen.";
            if (decision.Selection is not null) statusLabel.Text = selectionLabel.Text + "\n" + statusLabel.Text;
            scopeLabel.Text = string.Join("\n", decision.Groups.Select(group => group.Company.DisplayName + ": " + CacheText(group.Company.State)));
        }
        finally { _binding = false; }
        UpdateFocusedDetails();
    }

    private void View_FocusedRowChanged(object sender, FocusedRowChangedEventArgs e) { if (!_binding) UpdateFocusedDetails(); }
    private void View_CustomColumnDisplayText(object sender, CustomColumnDisplayTextEventArgs e)
    {
        if (e.Column == groupColumn && e.Value is int index && _decision is not null && index >= 0 && index < _decision.Groups.Length)
            e.DisplayText = _decision.Groups[index].Company.DisplayName + " · Quelle " + (index + 1);
    }
    private void UpdateFocusedDetails()
    {
        chooseButton.Enabled = _workspace?.CanUseBusinessDirectory == true && !_callEnded && _decision is not null && _operation?.IsRunning != true && view.GetFocusedRow() is CandidateRow;
        if (view.GetFocusedRow() is not CandidateRow row) { detailsLabel.Text = "Treffer markieren, um Rufnummer, Durchwahl, Herkunft und Cachestand zu sehen."; return; }
        detailsLabel.Text = "Installation: " + row.Candidate.Party.Company.InstallationId + " · " + row.Rolle + " · " + row.Name
            + (row.Candidate.OwnerDisplayName is { } owner ? " · Kontakt gehört zu: " + owner + " (" + row.Candidate.OwnerNumber + ")" : "")
            + "\n" + string.Join("\n", row.Candidate.Evidence.Select(item => MatchText(item.Match.Kind) + ": " + (item.Match.MatchedE164 ?? "Nummernblock")
                + " · Apparat: " + (item.Match.ApparatusNumber ?? "–") + " · Extension: " + (item.RegisteredExtension ?? "–")
                + " · Quelle: " + (item.Match.Source?.FieldName ?? "Regel")
                + " · Cache-/Regelrevision: " + item.Match.CacheRevision + "/" + item.Match.RuleRevision));
    }
    private static string DecisionText(CallerMatchDecisionKind kind) => kind switch
    {
        CallerMatchDecisionKind.Unique => "Ein belastbarer Treffer im vollständigen Suchstand wurde automatisch übernommen.",
        CallerMatchDecisionKind.LineUnique => "Eindeutiger Treffer der zugeordneten Leitung übernommen. Andere Firmen bleiben als Alternativen erhalten.",
        CallerMatchDecisionKind.Incomplete => "Suchstand unvollständig oder veraltet. Vorhandene Treffer können nur ausdrücklich übernommen werden.",
        CallerMatchDecisionKind.LineConflict => "Gesprächsmandant und Rufnummerntreffer widersprechen sich. Bitte Firma und Datensatz ausdrücklich wählen.",
        CallerMatchDecisionKind.Manual => "Auswahl manuell übernommen.",
        CallerMatchDecisionKind.SelectionUnavailable => "Die manuelle Auswahl ist nicht mehr verfügbar. Bitte erneut entscheiden; es wurde kein Ersatz gewählt.",
        CallerMatchDecisionKind.NoMatch => "Kein Treffer im vollständigen Suchstand.",
        CallerMatchDecisionKind.NoSearchCompanies => "Keine Suchfirmen ausgewählt. Unter Einstellungen → Telefon-Cache Firmen auswählen und ihren Cache aktualisieren.",
        _ => "Zuordnung bitte ausdrücklich bestätigen. Firmen, Rollen und Vorschläge prüfen."
    };
    private static string CacheText(PhoneSearchCompanyState state) => state switch
    {
        PhoneSearchCompanyState.Ready => "aktuell und vollständig", PhoneSearchCompanyState.CacheMissing => "Cache fehlt",
        PhoneSearchCompanyState.NotAuthorized => "nicht verfügbar/berechtigt", PhoneSearchCompanyState.CacheStale => "Cache veraltet",
        PhoneSearchCompanyState.NormalizationOutdated => "Normalisierung veraltet", PhoneSearchCompanyState.ContextChanged => "Nummernkontext geändert",
        PhoneSearchCompanyState.Incomplete => "Cache unvollständig", _ => "nicht verfügbar"
    };
    private static string MatchText(PhoneNumberMatchKind kind) => kind switch
    { PhoneNumberMatchKind.Exact => "Vollnummer", PhoneNumberMatchKind.ConfirmedBlock => "Bestätigter Block", PhoneNumberMatchKind.StemPrefix => "Stammnummer", _ => "Unbestätigter Vorschlag" };
    private sealed record CandidateRow(int Gruppe, string Firma, PhoneMatchCandidate Candidate, PhoneSearchCompanyStatus Cache)
    {
        public string Rolle => Candidate.Party.Kind switch
        { PartyKind.Customer => "Kunde", PartyKind.Supplier => "Lieferant", PartyKind.CustomerContact => "Kundenkontakt", _ => "Lieferantenkontakt" };
        public string Name => Candidate.DisplayName;
        public string Nummer => Candidate.Number ?? "–";
        public string Organisation => Candidate.OwnerDisplayName is { } owner ? owner + (Candidate.OwnerNumber is { } number ? " (" + number + ")" : "") : "–";
        public string Trefferart => string.Join(", ", Candidate.Evidence.Select(item => MatchText(item.Match.Kind)).Distinct());
        public string Cachestand => CacheText(Cache.State) + (Cache.RefreshedAtUtc is { } time ? " · " + time.LocalDateTime.ToString("g") : "");
    }
}

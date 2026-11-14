using DevExpress.XtraEditors;
using WawiCallPro.App.Hosting;
using WawiCallPro.Interfaces.Common;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Interfaces.Parties;
using System.Collections.Immutable;
using System.Text;

namespace WawiCallPro.App.Views;

public partial class HistoryPage : XtraUserControl
{
    private ICallHistoryService? _history;
    private CallNotesSessionFactory? _notesFactory;
    private AppUiState? _state;
    private PageOperation? _operation;
    private bool _arranging;
    private Guid? _selected;
    private bool _binding;
    private readonly Dictionary<Guid, CallNotesSession> _notes = [];
    private CallNotesSession? _activeNotes;
    private bool _syncingNotes;
    private bool _stopping;
    private Task? _stopTask;
    private CallNotesSession? _renderedNotes;
    private CallSnapshot? _renderedCall;
    private ImmutableArray<CallNote> _renderedNoteList;
    private ImmutableArray<CallPartySnapshot> _renderedParties;
    internal bool HasUnsavedNotes => _notes.Values.Any(notes => notes.HasUnsavedChanges);
    public HistoryPage()
    {
        InitializeComponent();
        UiButtonSizing.Attach(this);
        fromEdit.DateTime = DateTime.Today.AddDays(-30);
        toEdit.DateTime = DateTime.Today;
        saveNoteButton.Enabled = false;
        FontChanged += Layout_Changed;
        SizeChanged += Layout_Changed;
        filters.SizeChanged += Layout_Changed;
        DpiChangedAfterParent += Layout_Changed;
        var appearance = LookAndFeel;
        appearance.StyleChanged += Layout_Changed;
        Disposed += (_, _) => appearance.StyleChanged -= Layout_Changed;
        Layout_Changed(this, EventArgs.Empty);
    }
    private void Layout_Changed(object? sender, EventArgs e)
    {
        if (_arranging || IsDisposed) return;
        _arranging = true;
        filters.SuspendLayout();
        try
        {
            var editorWidth = TextRenderer.MeasureText("00.00.0000", Font).Width + LogicalToDeviceUnits(40);
            fromEdit.MinimumSize = toEdit.MinimumSize = new Size(editorWidth, 0);
            UiButtonSizing.Apply(refreshButton);
            UiButtonSizing.Apply(saveNoteButton);
            var fromWidth = fromLabel.GetPreferredSize(Size.Empty).Width + fromLabel.Margin.Horizontal + editorWidth + fromEdit.Margin.Horizontal + fromGroup.Margin.Horizontal;
            var toWidth = toLabel.GetPreferredSize(Size.Empty).Width + toLabel.Margin.Horizontal + editorWidth + toEdit.Margin.Horizontal + toGroup.Margin.Horizontal;
            var required = fromWidth + toWidth + refreshButton.GetPreferredSize(Size.Empty).Width + LogicalToDeviceUnits(8);
            var columns = filters.ClientSize.Width >= required ? 3 : filters.ClientSize.Width >= fromWidth + toWidth ? 2 : 1;
            fromGroup.ColumnStyles[0].SizeType = toGroup.ColumnStyles[0].SizeType = columns == 1 ? SizeType.Absolute : SizeType.AutoSize;
            if (columns == 1)
                fromGroup.ColumnStyles[0].Width = toGroup.ColumnStyles[0].Width = Math.Max(fromLabel.GetPreferredSize(Size.Empty).Width, toLabel.GetPreferredSize(Size.Empty).Width) + fromLabel.Margin.Horizontal;
            refreshButton.Margin = new Padding(columns == 1 ? (int)fromGroup.ColumnStyles[0].Width + fromEdit.Margin.Left : LogicalToDeviceUnits(4),
                LogicalToDeviceUnits(6), LogicalToDeviceUnits(4), LogicalToDeviceUnits(6));
            filters.ColumnCount = columns + 1;
            filters.RowCount = (3 + columns - 1) / columns;
            filters.ColumnStyles.Clear();
            filters.RowStyles.Clear();
            for (var column = 0; column < columns; column++) filters.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            for (var row = 0; row < filters.RowCount; row++) filters.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            filters.SetCellPosition(fromGroup, new(0, 0));
            filters.SetCellPosition(toGroup, new(1 % columns, 1 / columns));
            filters.SetCellPosition(refreshButton, new(2 % columns, 2 / columns));
        }
        finally { filters.ResumeLayout(true); _arranging = false; }
    }
    internal void Bind(AppWorkspace workspace, AppUiState? state)
    {
        _state = state;
        BindHistory(workspace.History, workspace.CallNotes);
    }
    public HistoryPage(ICallHistoryService history, CallNotesSessionFactory notesFactory) : this() => BindHistory(history, notesFactory);
    private void BindHistory(ICallHistoryService history, CallNotesSessionFactory notesFactory)
    {
        if (_history is not null) throw new InvalidOperationException("The history page is already bound.");
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _notesFactory = notesFactory ?? throw new ArgumentNullException(nameof(notesFactory));
        _operation = new PageOperation(this, statusLabel);
    }
    internal Task StopAsync() => _stopTask ??= StopCoreAsync();
    private async Task StopCoreAsync()
    {
        _stopping = true;
        UpdateNotes();
        var notes = _notes.Values.Select(session => session.StopAsync()).ToArray();
        if (_operation is not null) await _operation.StopAsync();
        await Task.WhenAll(notes);
    }
    private void DisposePage()
    {
        _stopping = true;
        foreach (var notes in _notes.Values) { notes.Changed -= Notes_Changed; notes.Dispose(); }
        _notes.Clear(); _activeNotes = null;
        _operation?.Dispose();
        if (view.Columns.Count != 0) _state?.Save(view, "CallHistory");
    }

    private async Task LoadCallsAsync(CancellationToken token)
    {
        if (_stopping) return;
        if (fromEdit.DateTime.Date > toEdit.DateTime.Date)
        {
            statusLabel.Text = "Der Beginn muss vor dem Ende des Zeitraums liegen.";
            return;
        }
        var start = new DateTimeOffset(DateTime.SpecifyKind(fromEdit.DateTime.Date, DateTimeKind.Local)).ToUniversalTime();
        var end = new DateTimeOffset(DateTime.SpecifyKind(toEdit.DateTime.Date.AddDays(1), DateTimeKind.Local)).ToUniversalTime();
        var calls = new List<CallSnapshot>();
        while (true)
        {
            token.ThrowIfCancellationRequested();
            var batch = await _history!.GetCallsAsync(new CallHistoryQuery(new PageRequest(calls.Count, 500), StartedFromUtc: start, StartedToUtc: end), token);
            calls.AddRange(batch.Items);
            if (!batch.HasMore) break;
            if (batch.Items.IsEmpty) throw new InvalidOperationException("Call history did not advance.");
        }
        token.ThrowIfCancellationRequested();
        var loadedIds = calls.Select(call => call.Id).ToHashSet();
        if (_notes.Values.Any(notes => notes.HasUnsavedChanges && !loadedIds.Contains(notes.CallId)))
        {
            statusLabel.Text = "Der Zeitraum würde Gespräche mit ungespeicherten Notizen ausblenden. Bitte zuerst speichern oder die Entwürfe leeren.";
            return;
        }
        var previous = (view.GetFocusedRow() as HistoryRow)?.Id;
        _binding = true;
        try
        {
            _selected = null;
            detailsEdit.Text = "";
            ActivateNotes(null);
            saveNoteButton.Enabled = false;
            grid.DataSource = calls.Select(call => new HistoryRow(call.Id, call.StartedAtUtc.LocalDateTime,
                call.Direction == CallDirection.Incoming ? "Eingehend" : "Ausgehend", DisplayNumber(call.RemoteAddress), HistoryState(call),
                call.AnsweredAtUtc is { } answered && call.EndedAtUtc is { } ended ? (ended - answered).ToString(@"hh\:mm\:ss") : "")).ToArray();
            view.Columns[nameof(HistoryRow.Id)].Visible = false;
            _state?.Restore(view, "CallHistory");
            if (previous is { } id && view.LocateByValue(nameof(HistoryRow.Id), id) is var handle && handle >= 0)
                view.FocusedRowHandle = handle;
            statusLabel.Text = calls.Count == 0 ? "Keine Gespräche im gewählten Zeitraum." : $"{calls.Count} Gespräche · Alle Mandanten. Zeiten werden lokal angezeigt.";
        }
        finally { _binding = false; }
        await LoadFocusedDetailsAsync(token);
    }

    private async void Refresh_Click(object? sender, EventArgs e)
    {
        if (_operation is null || _operation.IsRunning) return;
        await _operation.RunAsync(LoadCallsAsync);
    }
    private async void View_FocusedRowChanged(object sender, DevExpress.XtraGrid.Views.Base.FocusedRowChangedEventArgs e)
    {
        if (_binding || _operation is null) return;
        _selected = null;
        ActivateNotes(null);
        saveNoteButton.Enabled = false;
        detailsEdit.Text = "";
        if (_operation.IsRunning) return;
        await _operation.RunAsync(LoadFocusedDetailsAsync);
    }
    private async Task LoadFocusedDetailsAsync(CancellationToken token)
    {
        while (!_stopping && view.GetFocusedRow() is HistoryRow row)
        {
            token.ThrowIfCancellationRequested();
            if (!_notes.TryGetValue(row.Id, out var notes))
            {
                notes = _notesFactory!.Create(row.Id);
                notes.Changed += Notes_Changed;
                _notes.Add(row.Id, notes);
            }
            ActivateNotes(notes);
            await notes.LoadAsync();
            token.ThrowIfCancellationRequested();
            if (view.GetFocusedRow() is not HistoryRow current) return;
            if (current.Id != row.Id) continue;
            UpdateNotes();
            return;
        }
    }
    private void ActivateNotes(CallNotesSession? notes)
    {
        if (!ReferenceEquals(notes, _activeNotes)) _renderedNotes = null;
        _activeNotes = notes;
        _syncingNotes = true;
        try { noteEdit.Text = notes?.Draft ?? ""; }
        finally { _syncingNotes = false; }
        UpdateNotes();
    }
    private void Notes_Changed(object? sender, EventArgs e)
    {
        if (ReferenceEquals(sender, _activeNotes) && !IsDisposed) UpdateNotes();
    }
    private void Note_EditValueChanged(object? sender, EventArgs e)
    {
        if (_syncingNotes || _stopping || _activeNotes is null) return;
        if (noteEdit.Text.Length == 0) _activeNotes.DiscardDraft();
        else _activeNotes.SetDraft(noteEdit.Text);
    }
    private void UpdateNotes()
    {
        var notes = _activeNotes;
        var row = view.GetFocusedRow() as HistoryRow;
        var current = notes is not null && row?.Id == notes.CallId;
        _selected = current ? notes!.StoredCall?.Id : null;
        noteEdit.Properties.ReadOnly = _stopping || !current || notes!.IsBusy || notes.IsStopping;
        saveNoteButton.Enabled = !noteEdit.Properties.ReadOnly && _selected.HasValue && !string.IsNullOrWhiteSpace(notes!.Draft);
        if (!current) return;
        if (noteEdit.Text != notes!.Draft)
        {
            _syncingNotes = true;
            try { noteEdit.Text = notes.Draft; }
            finally { _syncingNotes = false; }
        }
        var call = notes!.StoredCall;
        if (!ReferenceEquals(_renderedNotes, notes) || !ReferenceEquals(_renderedCall, call)
            || !_renderedNoteList.Equals(notes.Notes) || !_renderedParties.Equals(notes.StoredParties))
        {
            detailsEdit.Text = call is null ? "Das Gespräch ist noch nicht verfügbar." : FormatDetails(call, notes.StoredParties, notes.Notes);
            _renderedNotes = notes; _renderedCall = call; _renderedNoteList = notes.Notes; _renderedParties = notes.StoredParties;
        }
        statusLabel.Text = notes.Status;
    }

    private static string FormatDetails(CallSnapshot call, ImmutableArray<CallPartySnapshot> parties, ImmutableArray<CallNote> notes)
    {
        var text = new StringBuilder();
        text.AppendLine(DisplayNumber(call.RemoteAddress) + " · " + (call.Direction == CallDirection.Incoming ? "Eingehend" : "Ausgehend") + " · " + HistoryState(call));
        text.AppendLine("Beginn: " + call.StartedAtUtc.LocalDateTime.ToString("g"));
        text.AppendLine("Angenommen: " + (call.AnsweredAtUtc?.LocalDateTime.ToString("g") ?? "Nicht angenommen"));
        text.AppendLine("Ende: " + (call.EndedAtUtc?.LocalDateTime.ToString("g") ?? "Noch offen"));
        if (call.EndReason is { } reason) text.AppendLine("Gesprächsende: " + EndReasonName(reason));
        text.AppendLine("Arbeitsplatz: " + (call.Origin?.MachineName ?? "Nicht gespeichert") + " · Benutzer: " + (call.Origin?.WindowsUserName ?? "Nicht gespeichert"));
        text.AppendLine(); text.AppendLine("Gespeicherte Zuordnung und Treffer:");
        if (parties.IsEmpty) text.AppendLine("Keine Geschäftspartnerzuordnung gespeichert.");
        else
        {
            foreach (var party in parties.OrderByDescending(party => party.IsSelected).ThenBy(party => party.Party.Kind).ThenBy(party => party.DisplayName, StringComparer.CurrentCultureIgnoreCase))
            {
                var product = party.Party.Company.ProductLine == LexwareProductLine.Pro ? "Lexware Pro" : "Financial Office";
                text.AppendLine((party.IsSelected ? "Ausgewählt: " : "Weiterer Treffer: ") + PartyRole(party.Party.Kind)
                    + (string.IsNullOrWhiteSpace(party.Number) ? "" : " " + party.Number) + " · " + party.DisplayName
                    + " · " + (string.IsNullOrWhiteSpace(party.CompanyDisplayName) ? "Mandantenname nicht gespeichert" : party.CompanyDisplayName) + " · " + product);
            }
        }
        text.AppendLine(); text.AppendLine("Gespeicherte Notizen:");
        if (notes.IsEmpty) text.AppendLine("Keine Notizen gespeichert.");
        else foreach (var note in notes) text.AppendLine(note.CreatedAtUtc.LocalDateTime.ToString("g") + ": " + note.Text);
        return text.ToString().TrimEnd();
    }

    private static string PartyRole(PartyKind kind) => kind switch
    {
        PartyKind.Customer => "Kunde", PartyKind.Supplier => "Lieferant", PartyKind.CustomerContact => "Kundenkontakt",
        PartyKind.SupplierContact => "Lieferantenkontakt", _ => "Geschäftspartner"
    };
    private static string EndReasonName(CallEndReason reason) => reason switch
    {
        CallEndReason.Completed => "Regulär beendet", CallEndReason.Missed => "Verpasst", CallEndReason.Rejected => "Abgelehnt",
        CallEndReason.Busy => "Besetzt", CallEndReason.Cancelled => "Abgebrochen", CallEndReason.RemoteHangup => "Gegenstelle hat aufgelegt",
        CallEndReason.NetworkFailure => "Verbindungsfehler", CallEndReason.Error => "Fehler", CallEndReason.Interrupted => "Unterbrochen",
        CallEndReason.Transferred => "Weitergeleitet", _ => "Unbekannt"
    };
    private static string HistoryState(CallSnapshot call) => (call.State is CallState.Ended or CallState.Failed) && call.EndReason is { } reason
        ? reason switch
        {
            CallEndReason.Missed or CallEndReason.Rejected or CallEndReason.Busy or CallEndReason.Cancelled or CallEndReason.Interrupted or CallEndReason.Transferred => EndReasonName(reason),
            _ => StateName(call.State)
        } : StateName(call.State);
    private async void SaveNote_Click(object? sender, EventArgs e)
    {
        if (_operation is null || _stopping || _activeNotes is not { } notes || _selected != notes.CallId) return;
        await _operation.RunAsync(async token =>
        {
            if (string.IsNullOrWhiteSpace(notes.Draft)) { statusLabel.Text = "Bitte einen Notiztext eingeben."; return; }
            await notes.SaveAsync();
            token.ThrowIfCancellationRequested();
            if (view.GetFocusedRow() is not HistoryRow current || current.Id != notes.CallId)
            {
                await LoadFocusedDetailsAsync(token);
                return;
            }
            UpdateNotes();
        });
    }

    private static string DisplayNumber(CallAddress address) => address.Presentation == NumberPresentation.Withheld ? "Unterdrückt" : address.OriginalValue ?? address.E164 ?? "Unbekannt";
    private static string StateName(CallState state) => state switch { CallState.Created => "Angelegt", CallState.Dialing => "Wählt", CallState.Ringing => "Klingelt", CallState.Connected => "Verbunden", CallState.Held => "Gehalten", CallState.Ended => "Beendet", _ => "Fehlgeschlagen" };
    private sealed record HistoryRow(Guid Id, DateTime Beginn, string Richtung, string Rufnummer, string Zustand, string Dauer);
}

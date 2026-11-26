using DevExpress.XtraEditors;
using WawiCallPro.Audio;
using WawiCallPro.App.Hosting;
using WawiCallPro.Database.Contracts;
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
    private const int PlaybackColumnLogicalWidth = 28;
    private ICallHistoryService? _history;
    private IHistoryRecordingPlayback? _playback;
    private HistoryRecordingFiles? _recordingFiles;
    private IReadOnlySet<Guid> _playable = new HashSet<Guid>();
    private Guid? _playingCall;
    private CancellationTokenSource? _playbackCancellation;
    private Task _playing = Task.CompletedTask;
    private CallNotesSessionFactory? _notesFactory;
    private AppUiState? _state;
    private ILogWindowService? _log;
    private ICompanyRepository? _callCompanies;
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
    internal async Task ShowCallAsync(Guid id, CancellationToken token)
    {
        if (_stopping || _history is null) throw new InvalidOperationException("History unavailable.");
        var history = await _history.GetCallAsync(id, token);
        if (history?.Call.Id != id) throw new Hosting.AutomationCallNotFoundException();
        var call = history.Call;
        var companyName = history.Parties.FirstOrDefault(party => party.IsSelected)?.CompanyDisplayName
            ?? await ReadCompanyNameAsync(call.Company, token);
        await ReadRecordingAvailabilityAsync([id], token);
        _binding = true;
        try
        {
            ClosePlayer();
            grid.DataSource = new System.ComponentModel.BindingList<HistoryRow>([ToRow(new(call, history.Storage), companyName)]);
            grid.ForceInitialize();
            view.FocusedRowHandle = 0;
        }
        finally { _binding = false; }
        await LoadFocusedDetailsAsync(token);
    }
    public HistoryPage()
    {
        InitializeComponent();
        ConfigurePlayer();
        playEditor.Buttons[0].ImageOptions.SvgImage = RibbonIconHelper.Load("PlaybackStart");
        stopEditor.Buttons[0].ImageOptions.SvgImage = RibbonIconHelper.Load("PlaybackStop");
        grid.DataSource = Array.Empty<HistoryRow>();
        UiButtonSizing.Attach(this);
        UiButtonSizing.UseNativeHeight(refreshButton);
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
            UiButtonSizing.Apply(exportRecordingButton, convertRecordingButton);
            FitColumns();
            var fromWidth = fromLabel.GetPreferredSize(Size.Empty).Width + fromLabel.Margin.Horizontal
                + Math.Max(editorWidth, fromEdit.Width) + fromEdit.Margin.Horizontal + fromGroup.Margin.Horizontal;
            var toWidth = toLabel.GetPreferredSize(Size.Empty).Width + toLabel.Margin.Horizontal
                + Math.Max(editorWidth, toEdit.Width) + toEdit.Margin.Horizontal + toGroup.Margin.Horizontal;
            var required = fromWidth + toWidth + refreshButton.Width + 2 * LogicalToDeviceUnits(4);
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
    private void FitColumns()
    {
        SetMinimum(startedColumn, new DateTime(2000, 12, 28, 23, 59, 0).ToString("g"));
        SetMinimum(directionColumn, "Ausgehend");
        SetMinimum(numberColumn, "+49 30 123456789");
        SetMinimum(companyColumn, "Beispielmandant");
        SetMinimum(machineColumn, "DESKTOP-1234567");
        SetMinimum(stateColumn, "Verbunden");
        SetMinimum(durationColumn, "00:00:00");
        var playbackWidth = LogicalToDeviceUnits(PlaybackColumnLogicalWidth);
        playbackColumn.MinWidth = Math.Min(playbackColumn.MinWidth, playbackWidth);
        playbackColumn.MaxWidth = playbackWidth;
        playbackColumn.MinWidth = playbackWidth;
        playbackColumn.Width = playbackWidth;

        void SetMinimum(DevExpress.XtraGrid.Columns.GridColumn column, string sample)
        {
            var header = TextRenderer.MeasureText(column.Caption, view.Appearance.HeaderPanel.Font).Width;
            var cell = TextRenderer.MeasureText(sample, view.Appearance.Row.Font).Width;
            column.MinWidth = Math.Max(header, cell) + LogicalToDeviceUnits(24);
            if (column.Width < column.MinWidth) column.Width = column.MinWidth;
        }
    }

    internal void Bind(AppWorkspace workspace, AppUiState? state)
    {
        _state = state;
        _log = workspace.LogWindow;
        _callCompanies = workspace.CallCompanies;
        _playback = workspace.RecordingPlayback;
        _recordingFiles = workspace.RecordingFiles;
        BindHistory(workspace.History, workspace.CallNotes);
        state?.AttachTabs(historyTabs, "History");
        _state?.Restore(view, "CallHistory-v2");
        playbackColumn.Fixed = DevExpress.XtraGrid.Columns.FixedStyle.Right;
        FitColumns();
    }
    public HistoryPage(ICallHistoryService history, CallNotesSessionFactory notesFactory) : this() => BindHistory(history, notesFactory);
    internal HistoryPage(ICallHistoryService history, CallNotesSessionFactory notesFactory, ILogWindowService log)
        : this(history, notesFactory) => _log = log;

    internal HistoryPage(ICallHistoryService history, CallNotesSessionFactory notesFactory, IHistoryRecordingPlayback playback)
        : this(history, notesFactory) => _playback = playback;

    internal HistoryPage(ICallHistoryService history, CallNotesSessionFactory notesFactory,
        IHistoryRecordingPlayback playback, HistoryRecordingFiles recordingFiles)
        : this(history, notesFactory, playback) => _recordingFiles = recordingFiles;

    private void BindHistory(ICallHistoryService history, CallNotesSessionFactory notesFactory)
    {
        if (_history is not null) throw new InvalidOperationException("The history page is already bound.");
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _notesFactory = notesFactory ?? throw new ArgumentNullException(nameof(notesFactory));
        _operation = new PageOperation(_ => _log?.LogWarning("Die Aktion in der Gesprächshistorie ist fehlgeschlagen."), this, showWaiting: false);
    }
    internal Task StopAsync() => _stopTask ??= StopCoreAsync();
    private async Task StopCoreAsync()
    {
        _stopping = true;
        ClosePlayer();
        UpdateNotes();
        var notes = _notes.Values.Select(session => session.StopAsync()).ToArray();
        if (_operation is not null) await _operation.StopAsync();
        await Task.WhenAll(notes.Append(_playing));
    }
    private void DisposePage()
    {
        _stopping = true;
        ClosePlayer();
        foreach (var notes in _notes.Values) { notes.Changed -= Notes_Changed; notes.Dispose(); }
        _notes.Clear(); _activeNotes = null;
        _operation?.Dispose();
        _recordingSpectrum.Dispose();
        _spectrumImage?.Dispose();
        if (view.Columns.Count != 0) _state?.Save(view, "CallHistory-v2");
    }

    private async Task LoadCallsAsync(CancellationToken token)
    {
        if (_stopping) return;
        if (fromEdit.DateTime.Date > toEdit.DateTime.Date)
        {
            _log?.LogWarning("Der Beginn muss vor dem Ende des Zeitraums liegen.");
            return;
        }
        var start = new DateTimeOffset(DateTime.SpecifyKind(fromEdit.DateTime.Date, DateTimeKind.Local)).ToUniversalTime();
        var end = new DateTimeOffset(DateTime.SpecifyKind(toEdit.DateTime.Date.AddDays(1), DateTimeKind.Local)).ToUniversalTime();
        var calls = new List<CallHistoryEntry>();
        while (true)
        {
            token.ThrowIfCancellationRequested();
            var batch = await _history!.GetEntriesAsync(new CallHistoryQuery(new PageRequest(calls.Count, 500), StartedFromUtc: start, StartedToUtc: end), token);
            calls.AddRange(batch.Items);
            if (!batch.HasMore) break;
            if (batch.Items.IsEmpty) throw new InvalidOperationException("Call history did not advance.");
        }
        token.ThrowIfCancellationRequested();
        var loadedIds = calls.Select(entry => entry.Call.Id).ToHashSet();
        if (_notes.Values.Any(notes => notes.HasUnsavedChanges && !loadedIds.Contains(notes.CallId)))
        {
            _log?.LogWarning("Der Zeitraum würde Gespräche mit ungespeicherten Notizen ausblenden. Bitte zuerst speichern oder die Entwürfe leeren.");
            return;
        }
        var companyNames = new Dictionary<CompanyKey, string?>();
        foreach (var key in calls.Select(entry => entry.Call.Company).OfType<CompanyKey>().Distinct())
            companyNames.Add(key, await ReadCompanyNameAsync(key, token));
        await ReadRecordingAvailabilityAsync(loadedIds, token);
        var previous = (view.GetFocusedRow() as HistoryRow)?.Id;
        _binding = true;
        try
        {
            _selected = null;
            detailsEdit.Text = "";
            ActivateNotes(null);
            saveNoteButton.Enabled = false;
            ClosePlayer();
            grid.DataSource = new System.ComponentModel.BindingList<HistoryRow>(calls.Select(entry =>
                ToRow(entry, entry.Call.Company is { } key ? companyNames.GetValueOrDefault(key) : null)).ToList());
            if (previous is { } id && view.LocateByValue(nameof(HistoryRow.Id), id) is var handle && handle >= 0)
                view.FocusedRowHandle = handle;
        }
        finally { _binding = false; }
        await LoadFocusedDetailsAsync(token);
    }

    private async Task ReadRecordingAvailabilityAsync(IReadOnlyCollection<Guid> ids, CancellationToken token)
    {
        _playable = _playback is null ? new HashSet<Guid>() : await _playback.FindAsync(ids, token);
        token.ThrowIfCancellationRequested();
    }

    private async Task<string?> ReadCompanyNameAsync(CompanyKey? key, CancellationToken token) =>
        key is not null && _callCompanies is not null ? (await _callCompanies.GetAsync(key, token))?.Company.DisplayName : null;

    private void View_PlaybackEditor(object? sender, DevExpress.XtraGrid.Views.Grid.CustomRowCellEditEventArgs e)
    {
        if (e.Column == playbackColumn && view.GetRow(e.RowHandle) is HistoryRow row && _playable.Contains(row.Id))
            e.RepositoryItem = _playingCall == row.Id ? stopEditor : playEditor;
    }

    private void View_ShowingEditor(object? sender, System.ComponentModel.CancelEventArgs e) =>
        e.Cancel = view.FocusedColumn != playbackColumn || view.GetFocusedRow() is not HistoryRow row || !_playable.Contains(row.Id);

    private void RefreshPlaybackRow(Guid callId)
    {
        view.CloseEditor();
        var handle = view.LocateByValue(nameof(HistoryRow.Id), callId);
        if (handle >= 0) view.RefreshRow(handle);
    }

    internal Task LoadAsync() => _operation?.RunAsync(LoadCallsAsync) ?? Task.CompletedTask;

    private async void Refresh_Click(object? sender, EventArgs e) => await LoadAsync();

    private async void View_FocusedRowChanged(object sender, DevExpress.XtraGrid.Views.Base.FocusedRowChangedEventArgs e)
    {
        if (_binding || _operation is null) return;
        UpdateRecordingActions();
        _selected = null;
        ActivateNotes(null);
        saveNoteButton.Enabled = false;
        detailsEdit.Text = "";
        if (_operation.IsRunning) return;
        await _operation.RunAsync(LoadFocusedDetailsAsync);
    }
    private async Task LoadFocusedDetailsAsync(CancellationToken token)
    {
        UpdateRecordingActions();
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
        statusLabel.Text = current ? notes!.Status : "Wählen Sie ein Gespräch aus.";
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

    private HistoryRow ToRow(CallHistoryEntry entry, string? companyName) =>
        new(entry, companyName ?? "", _playable.Contains(entry.Call.Id) ? [new(entry.Call.Id)] : []);

    private static string DisplayNumber(CallAddress address) => address.Presentation == NumberPresentation.Withheld ? "Unterdrückt" : address.OriginalValue ?? address.E164 ?? "Unbekannt";
    private static string StateName(CallState state) => state switch { CallState.Created => "Angelegt", CallState.Dialing => "Wählt", CallState.Ringing => "Klingelt", CallState.Connected => "Verbunden", CallState.Held => "Gehalten", CallState.Ended => "Beendet", _ => "Fehlgeschlagen" };
    private sealed class HistoryRow(CallHistoryEntry entry, string companyName, List<PlayerRow> playerDetails)
    {
        private CallSnapshot Call => entry.Call;
        public Guid Id => Call.Id;
        public DateTime Beginn => Call.StartedAtUtc.LocalDateTime;
        public string Richtung => Call.Direction == CallDirection.Incoming ? "Eingehend" : "Ausgehend";
        public string Rufnummer => DisplayNumber(Call.RemoteAddress);
        public string Zustand => HistoryState(Call);
        public string Dauer => Call.AnsweredAtUtc is { } answered && Call.EndedAtUtc is { } ended
            ? (ended - answered).ToString(@"hh\:mm\:ss") : "";
        public string Mandant => companyName;
        public string Rechner => Call.Origin?.MachineName ?? "";
        public string WindowsBenutzer => Call.Origin?.WindowsUserName ?? "";
        public DateTime? Angenommen => Call.AnsweredAtUtc?.LocalDateTime;
        public DateTime? Ende => Call.EndedAtUtc?.LocalDateTime;
        public string Endgrund => Call.EndReason is { } reason ? EndReasonName(reason) : "";
        public Guid? CompanyId => entry.Storage?.CompanyId;
        public long? WriteVersion => entry.Storage?.WriteVersion;
        public long Revision => Call.Revision;
        public Guid? WorkstationId => Call.Origin?.WorkstationId;
        public string LocalPresentation => PresentationName(Call.LocalAddress.Presentation);
        public string? LocalOriginalValue => Call.LocalAddress.OriginalValue;
        public string? LocalE164 => Call.LocalAddress.E164;
        public string? LocalExtension => Call.LocalAddress.Extension;
        public string RemotePresentation => PresentationName(Call.RemoteAddress.Presentation);
        public string? RemoteOriginalValue => Call.RemoteAddress.OriginalValue;
        public string? RemoteE164 => Call.RemoteAddress.E164;
        public string? RemoteExtension => Call.RemoteAddress.Extension;
        public DateTime StateChangedAt => Call.StateChangedAtUtc.LocalDateTime;
        public List<PlayerRow> PlayerDetails => playerDetails;
        public string Playback => "";

        private static string PresentationName(NumberPresentation presentation) => presentation switch
        {
            NumberPresentation.Available => "Verfügbar",
            NumberPresentation.Withheld => "Unterdrückt",
            _ => "Unbekannt"
        };
    }
}

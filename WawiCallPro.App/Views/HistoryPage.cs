using DevExpress.XtraEditors;
using WawiCallPro.App.Hosting;
using WawiCallPro.Interfaces.Common;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Interfaces.Telephony;

namespace WawiCallPro.App.Views;

public partial class HistoryPage : XtraUserControl
{
    private AppWorkspace? _workspace;
    private AppUiState? _state;
    private PageOperation? _operation;
    private bool _arranging;
    private Guid? _selected;
    private bool _binding;
    public HistoryPage()
    {
        InitializeComponent();
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
            refreshButton.Padding = new Padding(LogicalToDeviceUnits(12), LogicalToDeviceUnits(2), LogicalToDeviceUnits(12), LogicalToDeviceUnits(2));
            saveNoteButton.Padding = new Padding(LogicalToDeviceUnits(12), LogicalToDeviceUnits(6), LogicalToDeviceUnits(12), LogicalToDeviceUnits(6));
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
        _workspace = workspace; _state = state;
        _operation = new PageOperation(this, statusLabel);
    }
    internal Task StopAsync() => _operation?.StopAsync() ?? Task.CompletedTask;
    private void DisposePage()
    {
        _operation?.Dispose();
        if (view.Columns.Count != 0) _state?.Save(view, "CallHistory");
    }

    private async Task LoadCallsAsync(CancellationToken token)
    {
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
            var batch = await _workspace!.History.GetCallsAsync(new CallHistoryQuery(new PageRequest(calls.Count, 500), StartedFromUtc: start, StartedToUtc: end), token);
            calls.AddRange(batch.Items);
            if (!batch.HasMore) break;
            if (batch.Items.IsEmpty) throw new InvalidOperationException("Call history did not advance.");
        }
        token.ThrowIfCancellationRequested();
        _binding = true;
        try
        {
            _selected = null;
            detailsEdit.Text = "";
            noteEdit.Text = "";
            saveNoteButton.Enabled = false;
            grid.DataSource = calls.Select(call => new HistoryRow(call.Id, call.StartedAtUtc.LocalDateTime,
                call.Direction == CallDirection.Incoming ? "Eingehend" : "Ausgehend", DisplayNumber(call.RemoteAddress), StateName(call.State),
                call.AnsweredAtUtc is { } answered && call.EndedAtUtc is { } ended ? (ended - answered).ToString(@"hh\:mm\:ss") : "")).ToArray();
            view.Columns[nameof(HistoryRow.Id)].Visible = false;
            _state?.Restore(view, "CallHistory");
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
        saveNoteButton.Enabled = false;
        detailsEdit.Text = "";
        if (_operation.IsRunning) return;
        await _operation.RunAsync(LoadFocusedDetailsAsync);
    }
    private async Task LoadFocusedDetailsAsync(CancellationToken token)
    {
        while (view.GetFocusedRow() is HistoryRow row)
        {
            var history = await _workspace!.History.GetCallAsync(row.Id, token);
            token.ThrowIfCancellationRequested();
            if (view.GetFocusedRow() is not HistoryRow current) return;
            if (current.Id != row.Id) continue;
            _selected = history?.Call.Id;
            saveNoteButton.Enabled = history is not null;
            detailsEdit.Text = history is null ? "Das Gespräch ist nicht mehr vorhanden." :
                $"{row.Rufnummer} · {row.Richtung} · {row.Zustand}\r\nArbeitsplatz: {history.Call.Origin?.MachineName} · Benutzer: {history.Call.Origin?.WindowsUserName}\r\n" +
                string.Join("\r\n", history.Notes.Select(note => note.CreatedAtUtc.LocalDateTime.ToString("g") + ": " + note.Text));
            noteEdit.Text = "";
            statusLabel.Text = "Gesprächsdetails geladen.";
            return;
        }
    }
    private async void SaveNote_Click(object? sender, EventArgs e)
    {
        if (_operation is null || _selected is not { } id) return;
        var text = noteEdit.Text;
        await _operation.RunAsync(async token =>
        {
            if (string.IsNullOrWhiteSpace(text)) { statusLabel.Text = "Bitte einen Notiztext eingeben."; return; }
            var note = await _workspace!.History.SaveNoteAsync(id, Guid.NewGuid(), text, token);
            if (view.GetFocusedRow() is not HistoryRow current || current.Id != id)
            {
                await LoadFocusedDetailsAsync(token);
                return;
            }
            detailsEdit.Text += "\r\n" + note.CreatedAtUtc.LocalDateTime.ToString("g") + ": " + note.Text;
            noteEdit.Text = "";
            statusLabel.Text = "Gesprächsnotiz gespeichert.";
        });
    }

    private static string DisplayNumber(CallAddress address) => address.Presentation == NumberPresentation.Withheld ? "Unterdrückt" : address.OriginalValue ?? address.E164 ?? "Unbekannt";
    private static string StateName(CallState state) => state switch { CallState.Created => "Angelegt", CallState.Dialing => "Wählt", CallState.Ringing => "Klingelt", CallState.Connected => "Verbunden", CallState.Held => "Gehalten", CallState.Ended => "Beendet", _ => "Fehlgeschlagen" };
    private sealed record HistoryRow(Guid Id, DateTime Beginn, string Richtung, string Rufnummer, string Zustand, string Dauer);
}

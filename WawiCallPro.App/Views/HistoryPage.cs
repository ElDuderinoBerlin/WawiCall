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
    private int _offset;
    private Guid? _selected;
    private bool _binding;
    public HistoryPage()
    {
        InitializeComponent();
        fromEdit.DateTime = DateTime.Today.AddDays(-30);
        toEdit.DateTime = DateTime.Today;
        previousButton.Enabled = nextButton.Enabled = saveNoteButton.Enabled = false;
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
        var result = await _workspace!.History.GetCallsAsync(new CallHistoryQuery(new PageRequest(_offset), StartedFromUtc: start, StartedToUtc: end), token);
        _binding = true;
        try
        {
            _selected = null;
            detailsEdit.Text = "";
            noteEdit.Text = "";
            saveNoteButton.Enabled = false;
            grid.DataSource = result.Items.Select(call => new HistoryRow(call.Id, call.StartedAtUtc.LocalDateTime,
                call.Direction == CallDirection.Incoming ? "Eingehend" : "Ausgehend", DisplayNumber(call.RemoteAddress), StateName(call.State),
                call.AnsweredAtUtc is { } answered && call.EndedAtUtc is { } ended ? (ended - answered).ToString(@"hh\:mm\:ss") : "")).ToArray();
            view.Columns[nameof(HistoryRow.Id)].Visible = false;
            _state?.Restore(view, "CallHistory");
            previousButton.Enabled = _offset > 0;
            nextButton.Enabled = result.HasMore;
            statusLabel.Text = result.Items.IsEmpty ? "Keine Gespräche im gewählten Zeitraum." : $"{_offset + 1}–{_offset + result.Items.Length} · Alle Mandanten. Zeiten werden lokal angezeigt.";
        }
        finally { _binding = false; }
        await LoadFocusedDetailsAsync(token);
    }

    private async void Refresh_Click(object? sender, EventArgs e)
    {
        if (_operation is null || _operation.IsRunning) return;
        _offset = 0;
        await _operation.RunAsync(LoadCallsAsync);
    }
    private async void Previous_Click(object? sender, EventArgs e)
    {
        if (_operation is null || _operation.IsRunning) return;
        _offset = Math.Max(0, _offset - 100);
        await _operation.RunAsync(LoadCallsAsync);
    }
    private async void Next_Click(object? sender, EventArgs e)
    {
        if (_operation is null || _operation.IsRunning) return;
        _offset += 100;
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

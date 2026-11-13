using DevExpress.XtraEditors;
using WawiCallPro.App.Hosting;

namespace WawiCallPro.App.Views;

/// <summary>A view of the shared call draft; closing this view does not discard or stop the call-owned session.</summary>
public partial class CallNotesPanel : XtraUserControl
{
    private CallNotesSession? _session;
    private bool _binding;
    private bool _arranging;
    public CallNotesPanel()
    {
        InitializeComponent(); UiButtonSizing.Attach(this);
        FontChanged += Layout_Changed; SizeChanged += Layout_Changed; actions.SizeChanged += Layout_Changed;
        DpiChangedAfterParent += Layout_Changed;
        var appearance = LookAndFeel; appearance.StyleChanged += Layout_Changed;
        Disposed += (_, _) => appearance.StyleChanged -= Layout_Changed;
        Layout_Changed(this, EventArgs.Empty);
    }
    private void Layout_Changed(object? sender, EventArgs e)
    {
        if (_arranging || IsDisposed || actions is null) return;
        _arranging = true; actions.SuspendLayout();
        try
        {
            var size = UiButtonSizing.Apply(saveButton, discardButton, reloadButton);
            var columns = Math.Clamp(actions.ClientSize.Width / Math.Max(1, size.Width + saveButton.Margin.Horizontal), 1, 3);
            actions.ColumnCount = columns + 1; actions.RowCount = (3 + columns - 1) / columns;
            actions.ColumnStyles.Clear(); actions.RowStyles.Clear();
            for (var column = 0; column < columns; column++) actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            for (var row = 0; row < actions.RowCount; row++) actions.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            actions.SetCellPosition(saveButton, new(0, 0));
            actions.SetCellPosition(discardButton, new(1 % columns, 1 / columns));
            actions.SetCellPosition(reloadButton, new(2 % columns, 2 / columns));
        }
        finally { actions.ResumeLayout(true); _arranging = false; }
    }
    internal void Bind(CallNotesSession session)
    {
        if (_session is not null) _session.Changed -= Session_Changed;
        _session = session;
        session.Changed += Session_Changed;
        ShowSession();
    }
    private void Session_Changed(object? sender, EventArgs e) => ShowSession();
    private void ShowSession()
    {
        if (_session is null || IsDisposed || Disposing) return;
        _binding = true;
        try
        {
            if (draftEdit.Text != _session.Draft) draftEdit.Text = _session.Draft;
            var notes = _session.Notes.IsEmpty ? "Noch keine Gesprächsnotizen gespeichert." : string.Join("\r\n\r\n", _session.Notes.Select(note =>
                note.CreatedAtUtc.LocalDateTime.ToString("g") + "\r\n" + note.Text));
            if (notesEdit.Text != notes) notesEdit.Text = notes;
            draftEdit.Properties.ReadOnly = _session.IsBusy || _session.IsStopping;
            saveButton.Enabled = !_session.IsBusy && !_session.IsStopping && !string.IsNullOrWhiteSpace(_session.Draft);
            discardButton.Enabled = !_session.IsBusy && !_session.IsStopping && _session.Draft.Length != 0;
            reloadButton.Enabled = !_session.IsBusy && !_session.IsStopping;
            statusLabel.Text = _session.Status;
        }
        finally { _binding = false; }
    }
    private void Draft_Changed(object? sender, EventArgs e) { if (!_binding) _session?.SetDraft(draftEdit.Text); }
    private async void Save_Click(object? sender, EventArgs e) { if (_session is not null) await _session.SaveAsync(); }
    private async void Reload_Click(object? sender, EventArgs e) { if (_session is not null) await _session.LoadAsync(); }
    private void Discard_Click(object? sender, EventArgs e) => _session?.DiscardDraft();
    private void DisposePage() { if (_session is not null) _session.Changed -= Session_Changed; }
}

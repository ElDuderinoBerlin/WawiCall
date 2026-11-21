using DevExpress.XtraEditors;
using WawiCallPro.App.Hosting;

namespace WawiCallPro.App.Views;

public partial class SettingsPage
{
    private void ArrangeTransferActions()
    {
        var available = Math.Max(1, footer.ClientSize.Width - footer.Padding.Horizontal);
        foreach (var (table, buttons) in new[] { (transferActions, new[] { importButton, exportButton }), (dialogActions, new[] { saveButton, cancelButton }) })
        {
            table.SuspendLayout();
            var cell = buttons.Max(button => button.Width + button.Margin.Horizontal);
            var columns = Math.Clamp(available / Math.Max(1, cell), 1, buttons.Length);
            table.ColumnCount = columns; table.RowCount = (buttons.Length + columns - 1) / columns;
            table.ColumnStyles.Clear(); table.RowStyles.Clear();
            for (var column = 0; column < columns; column++) table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            for (var row = 0; row < table.RowCount; row++) table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            for (var index = 0; index < buttons.Length; index++) table.SetCellPosition(buttons[index], new(index % columns, index / columns));
            table.ResumeLayout(true);
        }
        var wrap = transferActions.GetPreferredSize(Size.Empty).Width + dialogActions.GetPreferredSize(Size.Empty).Width
            > footer.ClientSize.Width - footer.Padding.Horizontal;
        footer.SetColumnSpan(transferActions, wrap ? 2 : 1);
        footer.SetColumnSpan(dialogActions, wrap ? 2 : 1);
        footer.RowCount = wrap ? 2 : 1;
        footer.RowStyles.Clear(); footer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        if (wrap) footer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        footer.SetCellPosition(dialogActions, wrap ? new(0, 1) : new(1, 0));
    }

    private async void ImportSettings_Click(object? sender, EventArgs e) => await BrowseSettingsTransferAsync(true);
    private async void ExportSettings_Click(object? sender, EventArgs e) => await BrowseSettingsTransferAsync(false);

    private Task BrowseSettingsTransferAsync(bool import) => _operation?.RunAsync(async token =>
    {
        await Task.Yield(); token.ThrowIfCancellationRequested();
        var owner = (Control?)FindForm() ?? this;
        string path;
        using (new UiWaitCursor(owner))
        {
            using FileDialogBase dialog = import ? new XtraOpenFileDialog { Multiselect = false, CheckFileExists = true }
                : new XtraSaveFileDialog { FileName = "WawiCallPro-Einstellungen-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json", OverwritePrompt = true };
            dialog.Title = import ? "Einstellungen importieren" : "Einstellungen exportieren";
            dialog.Filter = "JSON-Dateien (*.json)|*.json"; dialog.FilterIndex = 1;
            dialog.DefaultExt = "json"; dialog.AddExtension = true; dialog.CheckPathExists = true;
            dialog.ValidateNames = true; dialog.RestoreDirectory = true;
            dialog.StartPosition = FormStartPosition.CenterParent;
            if (dialog.ShowDialog(owner) != DialogResult.OK) { statusLabel.Text = "Vorgang abgebrochen."; return; }
            path = dialog.FileName;
        }
        if (import && XtraMessageBox.Show(owner,
            "Die allgemeinen Einstellungen und Audiooptionen werden importiert. Ungespeicherte Eingaben in diesem Dialog werden verworfen.\n\n" +
            "Zugangsdaten, Datenbank-/ERP-/SIP-Verbindungen, Geräte-IDs und Mandanten-/Leitungszuordnungen bleiben lokal erhalten.\n\nFortfahren?",
            "Einstellungen importieren", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        { statusLabel.Text = "Import abgebrochen."; return; }
        using (new UiWaitCursor(owner))
        {
            if (import) await ImportSettingsCoreAsync(path, token);
            else await ExportSettingsCoreAsync(path, token);
        }
        if (import && XtraMessageBox.Show(owner,
            "Einstellungen importiert und gespeichert. Möchten Sie WawiCallPro jetzt neu starten, um alle Änderungen zu übernehmen?",
            "Einstellungen importieren", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
            RestartRequested?.Invoke(this, EventArgs.Empty);
    }) ?? Task.CompletedTask;

    internal Task ExportSettingsAsync(string path) => _operation?.RunAsync(token => ExportSettingsCoreAsync(path, token)) ?? Task.CompletedTask;
    internal Task ImportSettingsAsync(string path) => _operation?.RunAsync(token => ImportSettingsCoreAsync(path, token)) ?? Task.CompletedTask;
    private async Task ExportSettingsCoreAsync(string path, CancellationToken token)
    {
        await _workspace!.Settings.ExportPortableAsync(path, token);
        statusLabel.Text = "Gespeicherte Einstellungen als JSON exportiert. Zugangsdaten, Verbindungen und lokale Zuordnungen sind nicht enthalten.";
    }
    private async Task ImportSettingsCoreAsync(string path, CancellationToken token)
    {
        RequireCleanPluginDraft();
        Apply(await _workspace!.Settings.ImportPortableAsync(path, _snapshot!, token));
        LoadAutoStart();
        statusLabel.Text = "Einstellungen importiert und gespeichert. Lokale Verbindungen und Zuordnungen bleiben erhalten. Bitte WawiCallPro neu starten.";
    }
}

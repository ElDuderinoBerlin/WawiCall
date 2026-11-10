using DevExpress.XtraBars;
using DevExpress.XtraEditors;
using WawiCallPro.Database;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.App;

public partial class MainForm
{
    private bool _databaseBusy;

    private void UpdateDatabaseActions()
    {
        var enabled = _workspace is not null && !_closing && !_databaseBusy;
        databaseStatusItem.Enabled = databaseSetupItem.Enabled = enabled;
        var sqlite = _workspace?.Configuration.Current.Options.Database.Provider == CommunicationDatabaseProvider.Sqlite;
        databaseBackupItem.Enabled = databaseRestoreItem.Enabled = enabled && sqlite;
    }

    private async Task RunDatabaseAsync(Func<CancellationToken, Task> action)
    {
        if (_workspace is null || _operation is null || _closing || _databaseBusy || _operation.IsRunning) return;
        _databaseBusy = true;
        outputDock.Show();
        UpdateDatabaseActions();
        try { await _operation.RunAsync(action); }
        finally
        {
            _databaseBusy = false;
            if (!IsDisposed)
            {
                UpdateDatabaseActions();
                AppendOutput(connectionLabel.Text);
            }
        }
    }

    private void ShowDatabaseStatus(DatabaseMigrationStatus status)
    {
        var caption = status.State switch
        {
            DatabaseSchemaState.MissingDatabase => "Die Datenbank wurde noch nicht angelegt.",
            DatabaseSchemaState.EmptyDatabase => "Die vorhandene Datenbank ist leer. Sie kann ausdrücklich freigegeben werden.",
            DatabaseSchemaState.Current => "Die Datenbank ist betriebsbereit. Das Schema ist aktuell.",
            DatabaseSchemaState.MigrationRequired => "Eine Aktualisierung des Schemas ist erforderlich.",
            DatabaseSchemaState.NewerSchema => "Das Schema ist neuer als diese Anwendung. Bitte die Anwendung aktualisieren.",
            DatabaseSchemaState.ForeignDatabase => "Dies ist keine WawiCallPro-Kommunikationsdatenbank.",
            DatabaseSchemaState.MissingOrInaccessible => "Die Datenbank fehlt oder ist mit diesen Zugangsdaten nicht erreichbar.",
            _ => "Die Datenbank ist nicht betriebsbereit. Bitte Verbindung und Zugriffsrechte prüfen."
        };
        connectionLabel.Text = caption + Environment.NewLine
            + $"Angewendet: {string.Join(", ", status.AppliedVersions)}. Ausstehend: {string.Join(", ", status.PendingVersions)}.";
    }

    private async void DatabaseStatus_Click(object sender, ItemClickEventArgs e) =>
        await RunDatabaseAsync(async token => ShowDatabaseStatus(await _workspace!.Database.GetStatusAsync(token)));

    internal Views.FrmDatabaseSetup CreateDatabaseSetupDialog() => _workspace is null
        ? new Views.FrmDatabaseSetup() : new Views.FrmDatabaseSetup(_workspace.DatabaseSetup, _state);

    private void DatabaseSetup_Click(object sender, ItemClickEventArgs e)
    {
        if (_workspace is null || _closing || _databaseBusy || _operation?.IsRunning == true) return;
        using var dialog = CreateDatabaseSetupDialog();
        if (_backgroundRun) dialog.PrepareForBackgroundRun();
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            connectionLabel.Text = "Datenbankverbindung gespeichert. Bitte WawiCallPro neu starten, um sie in allen Arbeitsbereichen zu verwenden.";
            AppendOutput(connectionLabel.Text);
        }
    }

    private async void DatabaseBackup_Click(object sender, ItemClickEventArgs e)
    {
        if (!databaseBackupItem.Enabled) return;
        using var dialog = new SaveFileDialog
        {
            Filter = "SQLite-Sicherung (*.db)|*.db",
            FileName = "WawiCallPro-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".db",
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        await RunDatabaseAsync(async token =>
        {
            await _workspace!.Database.BackupSqliteAsync(dialog.FileName, token);
            connectionLabel.Text = "SQLite-Sicherung wurde erstellt.";
        });
    }

    private async void DatabaseRestore_Click(object sender, ItemClickEventArgs e)
    {
        if (!databaseRestoreItem.Enabled) return;
        using var dialog = new OpenFileDialog { Filter = "SQLite-Sicherung (*.db)|*.db|Alle Dateien (*.*)|*.*", CheckFileExists = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        await RunDatabaseAsync(async token =>
        {
            await _workspace!.Database.RestoreSqliteNewAsync(dialog.FileName, token);
            ShowDatabaseStatus(await _workspace.Database.GetStatusAsync(token));
        });
    }
}

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
        databaseStatusItem.Enabled = databaseCreateItem.Enabled = databaseMigrateItem.Enabled = databaseApproveItem.Enabled = enabled;
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

    private IProgress<DatabaseMigrationProgress> DatabaseProgress() => new Progress<DatabaseMigrationProgress>(progress =>
    {
        // Queued progress must not overwrite a completed operation or disposed controls.
        if (IsDisposed || !_databaseBusy) return;
        connectionLabel.Text = progress.Phase switch
        {
            DatabaseMigrationPhase.WaitingForLock => "Warte auf die Freigabe der Datenbank …",
            DatabaseMigrationPhase.CheckingPermissions => "Zugriffsrechte werden geprüft …",
            DatabaseMigrationPhase.Applying => $"Schemaänderung {progress.Version} wird angewendet ({progress.CompletedSteps}/{progress.TotalSteps}) …",
            DatabaseMigrationPhase.Verifying => "Schema wird überprüft …",
            DatabaseMigrationPhase.Completed => "Schemaaktualisierung abgeschlossen.",
            DatabaseMigrationPhase.Cancelled => "Vorgang abgebrochen.",
            DatabaseMigrationPhase.Failed => "Die Schemaaktualisierung ist fehlgeschlagen.",
            _ => "Datenbank wird geprüft …"
        };
    });

    private async void DatabaseStatus_Click(object sender, ItemClickEventArgs e) =>
        await RunDatabaseAsync(async token => ShowDatabaseStatus(await _workspace!.Database.GetStatusAsync(token)));

    private async void DatabaseCreate_Click(object sender, ItemClickEventArgs e) =>
        await RunDatabaseAsync(async token => ShowDatabaseStatus(await _workspace!.Database.CreateAndMigrateAsync(DatabaseProgress(), token)));

    private async void DatabaseMigrate_Click(object sender, ItemClickEventArgs e) =>
        await RunDatabaseAsync(async token => ShowDatabaseStatus(await _workspace!.Database.MigrateAsync(progress: DatabaseProgress(), cancellationToken: token)));

    private async void DatabaseApprove_Click(object sender, ItemClickEventArgs e)
    {
        if (_closing || _databaseBusy || _workspace is null || _operation?.IsRunning == true) return;
        if (XtraMessageBox.Show(this,
                "Die konfigurierte, bereits vorhandene Datenbank als leeres WawiCallPro-Ziel übernehmen und das Schema anlegen?",
                "Leere Datenbank freigeben", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        await RunDatabaseAsync(async token => ShowDatabaseStatus(await _workspace.Database.MigrateAsync(true, DatabaseProgress(), token)));
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

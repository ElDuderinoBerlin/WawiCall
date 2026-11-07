using DevExpress.XtraEditors;
using WawiCallPro.App.Hosting;
using WawiCallPro.Database;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.App.Views;

public partial class DatabasePage : XtraUserControl
{
    private AppWorkspace? _workspace;
    private PageOperation? _operation;
    public DatabasePage() { InitializeComponent(); }
    internal void Bind(AppWorkspace workspace)
    {
        _workspace = workspace;
        _operation = new PageOperation(this, statusLabel);
        var sqlite = workspace.Configuration.Current.Options.Database.Provider == CommunicationDatabaseProvider.Sqlite;
        backupButton.Enabled = restoreButton.Enabled = sqlite;
    }

    internal Task StopAsync() => _operation?.StopAsync() ?? Task.CompletedTask;
    private Task RunAsync(Func<CancellationToken, Task> action) => _operation?.RunAsync(action) ?? Task.CompletedTask;
    private void DisposePage() => _operation?.Dispose();

    private void ShowStatus(DatabaseMigrationStatus status)
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
        statusLabel.Text = caption + Environment.NewLine + $"Angewendet: {string.Join(", ", status.AppliedVersions)}. Ausstehend: {string.Join(", ", status.PendingVersions)}.";
    }

    private IProgress<DatabaseMigrationProgress> Progress() => new Progress<DatabaseMigrationProgress>(progress =>
    {
        if (IsDisposed) return;
        progressBar.Properties.Maximum = Math.Max(1, progress.TotalSteps);
        progressBar.Position = progress.CompletedSteps;
        statusLabel.Text = progress.Phase switch
        {
            DatabaseMigrationPhase.WaitingForLock => "Warte auf die Freigabe der Datenbank …",
            DatabaseMigrationPhase.CheckingPermissions => "Zugriffsrechte werden geprüft …",
            DatabaseMigrationPhase.Applying => $"Schemaänderung {progress.Version} wird angewendet …",
            DatabaseMigrationPhase.Verifying => "Schema wird überprüft …",
            DatabaseMigrationPhase.Completed => "Schemaaktualisierung abgeschlossen.",
            DatabaseMigrationPhase.Cancelled => "Vorgang abgebrochen.",
            DatabaseMigrationPhase.Failed => "Die Schemaaktualisierung ist fehlgeschlagen.",
            _ => "Datenbank wird geprüft …"
        };
    });

    private async void Status_Click(object? sender, EventArgs e) => await RunAsync(async token => ShowStatus(await _workspace!.Database.GetStatusAsync(token)));
    private async void Create_Click(object? sender, EventArgs e) => await RunAsync(async token => ShowStatus(await _workspace!.Database.CreateAndMigrateAsync(Progress(), token)));
    private async void Migrate_Click(object? sender, EventArgs e) => await RunAsync(async token => ShowStatus(await _workspace!.Database.MigrateAsync(progress: Progress(), cancellationToken: token)));
    private async void Approve_Click(object? sender, EventArgs e)
    {
        if (_operation?.IsRunning == true || XtraMessageBox.Show(this,
                "Die konfigurierte, bereits vorhandene Datenbank als leeres WawiCallPro-Ziel übernehmen und das Schema anlegen?",
                "Leere Datenbank freigeben", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        await RunAsync(async token => ShowStatus(await _workspace!.Database.MigrateAsync(true, Progress(), token)));
    }
    private async void Backup_Click(object? sender, EventArgs e)
    {
        if (_operation?.IsRunning == true) return;
        using var dialog = new SaveFileDialog { Filter = "SQLite-Sicherung (*.db)|*.db", FileName = "WawiCallPro-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".db", OverwritePrompt = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        await RunAsync(async token => { await _workspace!.Database.BackupSqliteAsync(dialog.FileName, token); statusLabel.Text = "SQLite-Sicherung wurde erstellt."; });
    }
    private async void Restore_Click(object? sender, EventArgs e)
    {
        if (_operation?.IsRunning == true) return;
        using var dialog = new OpenFileDialog { Filter = "SQLite-Sicherung (*.db)|*.db|Alle Dateien (*.*)|*.*", CheckFileExists = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        await RunAsync(async token => { await _workspace!.Database.RestoreSqliteNewAsync(dialog.FileName, token); ShowStatus(await _workspace.Database.GetStatusAsync(token)); });
    }
}

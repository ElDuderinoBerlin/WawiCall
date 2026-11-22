using DevExpress.XtraEditors;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.App.Views;

public partial class SettingsPage
{
    private FrmDatabaseSetup? _databaseDialog;

    private void DatabaseLayout_Changed(object? sender, LayoutEventArgs e)
    {
        var margin = databaseSetupButton.Margin;
        if (margin.Left != sqlitePathValue.Margin.Left)
            databaseSetupButton.Margin = new Padding(sqlitePathValue.Margin.Left, margin.Top, margin.Right, margin.Bottom);
    }

    private void ApplyDatabase(CommunicationDatabaseSettings database)
    {
        providerValue.Text = database.Provider switch
        {
            CommunicationDatabaseProvider.Sqlite => "SQLite",
            CommunicationDatabaseProvider.SqlServer => "SQL Server",
            CommunicationDatabaseProvider.MySql => "MySQL / MariaDB",
            CommunicationDatabaseProvider.PostgreSql => "PostgreSQL",
            _ => database.Provider.ToString()
        };
        sqlitePathValue.Text = database.Provider == CommunicationDatabaseProvider.Sqlite ? database.SqliteFileName ?? "communications.db" : "–";
        databaseConnectionValue.Text = database.ConnectionString is null ? "–" : "Geschützt gespeichert";
    }

    private async void DatabaseSetup_Click(object? sender, EventArgs e) =>
        await SetupDatabaseAsync(dialog => Task.FromResult(dialog.ShowDialog((IWin32Window?)FindForm() ?? this)));

    internal Task SetupDatabaseAsync(Func<FrmDatabaseSetup, Task<DialogResult>> showDialog) => _operation?.RunAsync(async token =>
    {
        // Publish the tracked operation before entering the modal message loop.
        await Task.Yield();
        token.ThrowIfCancellationRequested();
        if (_workspace is null) return;
        databaseSetupButton.Enabled = false;
        try
        {
            using var waiting = new Hosting.UiWaitCursor(this);
            using var dialog = new FrmDatabaseSetup(_workspace.DatabaseSetup, _state);
            _databaseDialog = dialog;
            if (await showDialog(dialog) != DialogResult.OK)
            {
                ReportSettingsNotice(WawiCallPro.Utilities.Operations.OperationEvent.SettingsCancelled);
                return;
            }
            _snapshot = await _workspace.Settings.LoadAsync(token);
            // Only update the committed database values. Editors on the other tabs keep their current drafts.
            ApplyDatabase(_snapshot.Options.Database);
            ReportSettingsNotice(WawiCallPro.Utilities.Operations.OperationEvent.SettingsDatabaseConfigured);
        }
        finally
        {
            _databaseDialog = null;
            if (!IsDisposed) databaseSetupButton.Enabled = !_audioStopped;
        }
    }) ?? Task.CompletedTask;
}

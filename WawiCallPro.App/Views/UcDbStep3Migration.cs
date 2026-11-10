using DevExpress.XtraEditors;
using WawiCallPro.Database;

namespace WawiCallPro.App.Views;

/// <summary>Progress and result page of the database setup wizard.</summary>
public partial class UcDbStep3Migration : XtraUserControl
{
    public UcDbStep3Migration() => InitializeComponent();
    public void ShowProgress(DatabaseMigrationProgress? progress = null)
    {
        picResult.Visible = memoResult.Visible = false;
        progressPanel.Visible = true;
        progressPanel.Description = progress?.Phase switch
        {
            DatabaseMigrationPhase.WaitingForLock => "Warte auf Datenbankfreigabe …",
            DatabaseMigrationPhase.CheckingPermissions => "Zugriffsrechte werden geprüft …",
            DatabaseMigrationPhase.Applying => $"Schemaänderung {progress.Version} ({progress.CompletedSteps}/{progress.TotalSteps}) …",
            DatabaseMigrationPhase.Verifying => "Schema wird überprüft …", _ => "Datenbank wird geprüft …"
        };
    }
    public void ShowResult(bool success, string message)
    {
        progressPanel.Visible = false;
        picResult.Visible = false;
        tablePanelRoot.SetColumnSpan(memoResult, 2);
        tablePanelRoot.SetColumn(memoResult, 0);
        memoResult.Visible = true;
        memoResult.Text = (success ? "Datenbank bereit." : "Einrichtung nicht abgeschlossen.") + Environment.NewLine + Environment.NewLine + message;
    }
}

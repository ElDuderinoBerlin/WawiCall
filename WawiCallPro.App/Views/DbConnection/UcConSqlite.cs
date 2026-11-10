using System.ComponentModel;
using DevExpress.XtraEditors;

namespace WawiCallPro.App.Views.DbConnection;

/// <summary>SQLite file selection, following the LxTranslate setup control.</summary>
public partial class UcConSqlite : XtraUserControl
{
    public UcConSqlite()
    {
        InitializeComponent();
        txtSqliteFile.EditValueChanged += InputChanged;
    }
    public event EventHandler? Changed;
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string DataSource { get => txtSqliteFile.Text.Trim(); set => txtSqliteFile.Text = value; }
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public BaseEdit DataSourceEditor => txtSqliteFile;
    private void InputChanged(object? sender, EventArgs e) => Changed?.Invoke(this, EventArgs.Empty);
    private void BtnSqliteBrowse_Click(object? sender, EventArgs e)
    {
        using var dialog = new XtraSaveFileDialog
        {
            Title = "SQLite-Datenbankdatei wählen", Filter = "SQLite-Datenbank (*.db)|*.db|Alle Dateien (*.*)|*.*",
            DefaultExt = "db", OverwritePrompt = false, FileName = DataSource,
            StartPosition = FormStartPosition.CenterParent
        };
        if (dialog.ShowDialog(this) == DialogResult.OK) DataSource = dialog.FileName;
    }
}

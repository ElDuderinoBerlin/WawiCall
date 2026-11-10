using System.ComponentModel;
using DevExpress.XtraEditors;
using Microsoft.Data.SqlClient;

namespace WawiCallPro.App.Views.DbConnection;

/// <summary>SQL Server editor with typed connection-string handling.</summary>
public partial class UcConSqlServer : XtraUserControl
{
    private SqlConnectionStringBuilder _original = new();
    public UcConSqlServer()
    {
        InitializeComponent();
        foreach (var editor in new BaseEdit[] { txtSqlServer, txtSqlPort, txtSqlDb, txtSqlUser, txtSqlPwd, chkEncrypt, chkTrustCert })
            editor.EditValueChanged += InputChanged;
        chkWinAuth.CheckedChanged += ChkWinAuth_CheckedChanged;
    }
    public event EventHandler? Changed;
    public void UseDefaults() => LoadConnection(new SqlConnectionStringBuilder
    {
        DataSource = ".", InitialCatalog = "WawiCallPro", IntegratedSecurity = true,
        Encrypt = SqlConnectionEncryptOption.Mandatory, TrustServerCertificate = false
    }.ConnectionString);
    public void LoadConnection(string connectionString)
    {
        _original = new SqlConnectionStringBuilder(connectionString);
        var endpoint = _original.DataSource.Split(',', 2);
        txtSqlServer.Text = endpoint[0];
        txtSqlPort.Text = endpoint.Length == 2 ? endpoint[1] : "";
        txtSqlDb.Text = _original.InitialCatalog;
        chkWinAuth.Checked = _original.IntegratedSecurity;
        txtSqlUser.Text = _original.UserID;
        txtSqlPwd.Text = _original.Password;
        chkEncrypt.Checked = _original.Encrypt != SqlConnectionEncryptOption.Optional;
        chkTrustCert.Checked = _original.TrustServerCertificate;
        ChkWinAuth_CheckedChanged(this, EventArgs.Empty);
    }
    public string BuildConnectionString()
    {
        var host = txtSqlServer.Text.Trim();
        var port = txtSqlPort.Text.Trim();
        if (port.Length > 0 && (!int.TryParse(port, out var number) || number is < 1 or > 65535))
            throw new ArgumentException("Der Port muss zwischen 1 und 65535 liegen.");
        var builder = new SqlConnectionStringBuilder(_original.ConnectionString)
        {
            DataSource = port.Length == 0 || host.Contains(',') ? host : host + "," + port,
            InitialCatalog = txtSqlDb.Text.Trim(), IntegratedSecurity = chkWinAuth.Checked,
            UserID = chkWinAuth.Checked ? "" : txtSqlUser.Text.Trim(), Password = chkWinAuth.Checked ? "" : txtSqlPwd.Text,
            Encrypt = chkEncrypt.Checked ? (_original.Encrypt == SqlConnectionEncryptOption.Strict
                ? SqlConnectionEncryptOption.Strict : SqlConnectionEncryptOption.Mandatory) : SqlConnectionEncryptOption.Optional,
            TrustServerCertificate = chkTrustCert.Checked
        };
        return builder.ConnectionString;
    }
    internal bool HasRequiredValues => !string.IsNullOrWhiteSpace(txtSqlServer.Text)
        && !string.IsNullOrWhiteSpace(txtSqlDb.Text) && (chkWinAuth.Checked || !string.IsNullOrWhiteSpace(txtSqlUser.Text));
    private void InputChanged(object? sender, EventArgs e) => Changed?.Invoke(this, EventArgs.Empty);
    private void ChkWinAuth_CheckedChanged(object? sender, EventArgs e)
    {
        lblSqlUser.Enabled = txtSqlUser.Enabled = lblSqlPwd.Enabled = txtSqlPwd.Enabled = !chkWinAuth.Checked;
        InputChanged(sender, e);
    }
}

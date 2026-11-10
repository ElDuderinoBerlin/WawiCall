using DevExpress.XtraEditors;
using MySqlConnector;
using Npgsql;
using WawiCallPro.Database;

namespace WawiCallPro.App.Views.DbConnection;

/// <summary>Shared LxTranslate-style MySQL/MariaDB and PostgreSQL editor.</summary>
public partial class UcConMyPg : XtraUserControl
{
    private DatabaseType _type;
    private string _original = "";
    public UcConMyPg()
    {
        InitializeComponent();
        foreach (var editor in new BaseEdit[] { txtHost, txtPort, txtDbName, txtUser, txtPwd, cmbSslMode })
            editor.EditValueChanged += InputChanged;
    }
    public event EventHandler? Changed;
    public void LoadConnection(DatabaseType type, string? connectionString)
    {
        _type = type;
        _original = connectionString ?? "";
        cmbSslMode.Properties.Items.Clear();
        if (type == DatabaseType.PostgreSql)
        {
            cmbSslMode.Properties.Items.AddRange(Enum.GetNames<SslMode>());
            var builder = new NpgsqlConnectionStringBuilder(_original);
            txtHost.Text = builder.Host ?? "localhost";
            txtPort.Text = builder.Port.ToString(); txtDbName.Text = builder.Database ?? "WawiCallPro";
            txtUser.Text = builder.Username ?? ""; txtPwd.Text = builder.Password ?? "";
            cmbSslMode.Text = builder.SslMode.ToString();
        }
        else
        {
            cmbSslMode.Properties.Items.AddRange(Enum.GetNames<MySqlSslMode>());
            var builder = new MySqlConnectionStringBuilder(_original);
            txtHost.Text = string.IsNullOrEmpty(builder.Server) ? "localhost" : builder.Server;
            txtPort.Text = builder.Port.ToString(); txtDbName.Text = string.IsNullOrEmpty(builder.Database) ? "WawiCallPro" : builder.Database;
            txtUser.Text = builder.UserID; txtPwd.Text = builder.Password;
            cmbSslMode.Text = builder.SslMode.ToString();
        }
    }
    public string BuildConnectionString()
    {
        if (!int.TryParse(txtPort.Text, out var port) || port is < 1 or > 65535)
            throw new ArgumentException("Der Port muss zwischen 1 und 65535 liegen.");
        return _type == DatabaseType.PostgreSql
            ? new NpgsqlConnectionStringBuilder(_original)
            {
                Host = txtHost.Text.Trim(), Port = port, Database = txtDbName.Text.Trim(), Username = txtUser.Text.Trim(),
                Password = txtPwd.Text, SslMode = Enum.Parse<SslMode>(cmbSslMode.Text)
            }.ConnectionString
            : new MySqlConnectionStringBuilder(_original)
            {
                Server = txtHost.Text.Trim(), Port = (uint)port, Database = txtDbName.Text.Trim(), UserID = txtUser.Text.Trim(),
                Password = txtPwd.Text, SslMode = Enum.Parse<MySqlSslMode>(cmbSslMode.Text)
            }.ConnectionString;
    }
    internal bool HasRequiredValues => !string.IsNullOrWhiteSpace(txtHost.Text)
        && !string.IsNullOrWhiteSpace(txtDbName.Text) && !string.IsNullOrWhiteSpace(txtUser.Text);
    private void InputChanged(object? sender, EventArgs e) => Changed?.Invoke(this, EventArgs.Empty);
}

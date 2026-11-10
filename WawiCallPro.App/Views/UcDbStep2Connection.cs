using System.ComponentModel;
using System.Data.Common;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.DXErrorProvider;
using WawiCallPro.Database;
using WawiCallPro.Utilities.Persistence;

namespace WawiCallPro.App.Views;

/// <summary>Provider-specific connection editors from LxTranslate with a masked, copyable preview.</summary>
public partial class UcDbStep2Connection : XtraUserControl
{
    private DatabaseType _type = DatabaseType.Sqlite;
    private bool _loading;
    public UcDbStep2Connection()
    {
        InitializeComponent();
        ucSqlite.Changed += InputChanged;
        ucSqlServer.Changed += InputChanged;
        ucMyPg.Changed += InputChanged;
        ucPostgres.Changed += InputChanged;
        _loading = true;
        try
        {
            ucSqlServer.UseDefaults();
            ucMyPg.LoadConnection(DatabaseType.MySql, null);
            ucPostgres.LoadConnection(DatabaseType.PostgreSql, null);
        }
        finally { _loading = false; }
    }
    public void LoadDraft(DatabaseSetupDraft draft)
    {
        _loading = true;
        switch (draft.Type)
        {
            case DatabaseType.Sqlite: ucSqlite.DataSource = draft.SqlitePath ?? ""; break;
            case DatabaseType.SqlServer: ucSqlServer.LoadConnection(draft.ConnectionString!); break;
            case DatabaseType.MySql: ucMyPg.LoadConnection(draft.Type, draft.ConnectionString); break;
            case DatabaseType.PostgreSql: ucPostgres.LoadConnection(draft.Type, draft.ConnectionString); break;
        }
        SelectProvider(draft.Type);
    }
    public void SelectProvider(DatabaseType type)
    {
        _type = type;
        navFrame.SelectedPage = type switch
        {
            DatabaseType.Sqlite => pageSqlite, DatabaseType.SqlServer => pageSqlServer,
            DatabaseType.PostgreSql => pagePostgres, _ => pageMyPg
        };
        _loading = false;
        InputChanged(this, EventArgs.Empty);
    }
    public DatabaseSetupDraft ReadDraft() => new()
    {
        Type = _type,
        SqlitePath = _type == DatabaseType.Sqlite ? ucSqlite.DataSource : null,
        ConnectionString = _type switch
        {
            DatabaseType.SqlServer => ucSqlServer.BuildConnectionString(),
            DatabaseType.MySql => ucMyPg.BuildConnectionString(),
            DatabaseType.PostgreSql => ucPostgres.BuildConnectionString(), _ => null
        }
    };
    public bool ValidateInputs()
    {
        _errorProvider.ClearErrors();
        try
        {
            if (_type == DatabaseType.SqlServer && !ucSqlServer.HasRequiredValues
                || _type == DatabaseType.MySql && !ucMyPg.HasRequiredValues
                || _type == DatabaseType.PostgreSql && !ucPostgres.HasRequiredValues)
                throw new ArgumentException();
            var draft = ReadDraft();
            _ = _type == DatabaseType.Sqlite ? DatabaseTarget.Sqlite(draft.SqlitePath!) : DatabaseTarget.Server(_type, draft.ConnectionString!);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or DatabaseOperationException)
        {
            _errorProvider.SetError(txtCsPreview, "Pfad beziehungsweise Server, Port, Datenbank und Benutzer prüfen.", ErrorType.Critical);
            return false;
        }
    }
    private void InputChanged(object? sender, EventArgs e)
    {
        if (_loading) return;
        _errorProvider.ClearErrors();
        try
        {
            var draft = ReadDraft();
            if (_type == DatabaseType.Sqlite) { txtCsPreview.Text = draft.SqlitePath; return; }
            var preview = new DbConnectionStringBuilder { ConnectionString = draft.ConnectionString! };
            foreach (var key in preview.Keys.Cast<string>().Where(key => key.Equals("Password", StringComparison.OrdinalIgnoreCase)
                         || key.Equals("Pwd", StringComparison.OrdinalIgnoreCase)).ToArray()) preview.Remove(key);
            txtCsPreview.Text = preview.ConnectionString;
        }
        catch (ArgumentException) { txtCsPreview.Text = "Verbindungsparameter unvollständig"; }
    }
}

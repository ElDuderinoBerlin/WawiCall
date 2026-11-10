using System.ComponentModel;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using WawiCallPro.Database;

namespace WawiCallPro.App.Views
{
    /// <summary>Provider selection and explanations adopted from LxTranslate.</summary>
    public partial class UcDbStep1DbType : XtraUserControl
    {
        public UcDbStep1DbType()
        {
            InitializeComponent();
            cmbDbType.Properties.Items.AddRange(new ImageComboBoxItem[]
            {
                new("SQLite", DatabaseType.Sqlite), new("SQL Server", DatabaseType.SqlServer),
                new("MySQL / MariaDB", DatabaseType.MySql), new("PostgreSQL", DatabaseType.PostgreSql)
            });
            cmbDbType.SelectedIndexChanged += CmbDbType_SelectedIndexChanged;
            cmbDbType.SelectedIndex = 0;
        }
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public DatabaseType SelectedDbType
        {
            get => cmbDbType.EditValue is DatabaseType type ? type : DatabaseType.Sqlite;
            set => cmbDbType.EditValue = value;
        }
        public event EventHandler? DbTypeChanged;
        private void CmbDbType_SelectedIndexChanged(object? sender, EventArgs e)
        {
            UpdateDatabaseDescription();
            DbTypeChanged?.Invoke(this, EventArgs.Empty);
        }
        private void UpdateDatabaseDescription()
        {
            lblDbDescription.Text = SelectedDbType switch
            {
                DatabaseType.Sqlite =>
                    "<b>Einsatz:</b> Einzelplatz und kleine Installationen.<br><br>" +
                    "<b>Vorteile:</b> Keine Serverinstallation, einfache Sicherung und geringer Wartungsaufwand.<br><br>" +
                    "<b>Besonderheiten:</b> Dateibasiert; für viele gleichzeitige Benutzer oder Netzwerkfreigaben weniger geeignet.",
                DatabaseType.MySql =>
                    "<b>Einsatz:</b> Mehrbenutzerbetrieb und vorhandene MySQL-/MariaDB-Infrastruktur.<br><br>" +
                    "<b>Vorteile:</b> Bewährt, plattformunabhängig und mit vielen Verwaltungswerkzeugen verfügbar.<br><br>" +
                    "<b>Besonderheiten:</b> Server, Benutzerrechte, Backups und verschlüsselte Verbindungen müssen administriert werden.",
                DatabaseType.SqlServer =>
                    "<b>Einsatz:</b> Mehrbenutzerbetrieb in Windows- und Microsoft-Umgebungen.<br><br>" +
                    "<b>Vorteile:</b> Windows-Authentifizierung, gute Verwaltungswerkzeuge und zuverlässige Sicherungen.<br><br>" +
                    "<b>Besonderheiten:</b> Benötigt einen Serverdienst; Edition, Lizenzierung und Ressourcenbedarf berücksichtigen.",
                DatabaseType.PostgreSql =>
                    "<b>Einsatz:</b> Anspruchsvolle Mehrbenutzer- und Serverinstallationen.<br><br>" +
                    "<b>Vorteile:</b> Hohe Datenintegrität, robuste Parallelität und umfangreiche SQL-Funktionen.<br><br>" +
                    "<b>Besonderheiten:</b> Einrichtung, Benutzerrechte, Backups und Wartung erfordern Datenbankkenntnisse.",
                _ => string.Empty
            };
        }
    }
}

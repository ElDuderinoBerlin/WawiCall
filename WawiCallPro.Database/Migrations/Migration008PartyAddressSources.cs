using FluentMigrator;
using WawiCallPro.Database.Schema;

namespace WawiCallPro.Database.Migrations;

[Migration(8, "Preserve party and address source metadata and country text")]
public sealed class Migration008PartyAddressSources(DatabaseType databaseType) : ForwardOnlyMigration
{
    public override void Up()
    {
        var sql = new SchemaSql(databaseType);
        Add("parties", "sourcerecordtype", 128);
        Add("parties", "sourcerecordid", 256);
        Add("partyaddresses", "sourcecountry", 512);
        Add("partyaddresses", "sourcerecordtype", 128);
        Add("partyaddresses", "sourcerecordid", 256);
        Add("partyaddresses", "sourcefield", 256);

        void Add(string table, string column, int length)
        {
            var quoted = sql.Quote(column);
            var add = databaseType == DatabaseType.SqlServer ? " ADD " : " ADD COLUMN ";
            Execute.Sql($"ALTER TABLE {sql.Table(table)}{add}{quoted} {sql.UnicodeType(length)} NULL" +
                (databaseType == DatabaseType.Sqlite ? $" CHECK ({quoted} IS NULL OR {sql.Length(quoted)}<={length})" : ""));
        }
    }
}

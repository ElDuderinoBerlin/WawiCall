using FluentMigrator;
using WawiCallPro.Database.Schema;

namespace WawiCallPro.Database.Migrations;

[Migration(8, "Preserve party and address source metadata and country text")]
public sealed class Migration008PartyAddressSources(DatabaseType databaseType) : ForwardOnlyMigration
{
    public override void Up()
    {
        var sql = new SchemaSql(databaseType);
        if (databaseType == DatabaseType.Sqlite)
        {
            // SQLite does not enforce VARCHAR lengths, so each new column keeps its inline CHECK.
            Alter.Table("parties").AddColumn("sourcerecordtype")
                .AsCustom("TEXT CHECK (sourcerecordtype IS NULL OR length(sourcerecordtype)<=128)").Nullable();
            Alter.Table("parties").AddColumn("sourcerecordid")
                .AsCustom("TEXT CHECK (sourcerecordid IS NULL OR length(sourcerecordid)<=256)").Nullable();
            Alter.Table("partyaddresses").AddColumn("sourcecountry")
                .AsCustom("TEXT CHECK (sourcecountry IS NULL OR length(sourcecountry)<=512)").Nullable();
            Alter.Table("partyaddresses").AddColumn("sourcerecordtype")
                .AsCustom("TEXT CHECK (sourcerecordtype IS NULL OR length(sourcerecordtype)<=128)").Nullable();
            Alter.Table("partyaddresses").AddColumn("sourcerecordid")
                .AsCustom("TEXT CHECK (sourcerecordid IS NULL OR length(sourcerecordid)<=256)").Nullable();
            Alter.Table("partyaddresses").AddColumn("sourcefield")
                .AsCustom("TEXT CHECK (sourcefield IS NULL OR length(sourcefield)<=256)").Nullable();
            return;
        }

        Alter.Table("parties").InSchema(sql.SchemaName)
            .AddColumn("sourcerecordtype").AsString(128).Nullable();
        Alter.Table("parties").InSchema(sql.SchemaName)
            .AddColumn("sourcerecordid").AsString(256).Nullable();
        Alter.Table("partyaddresses").InSchema(sql.SchemaName)
            .AddColumn("sourcecountry").AsString(512).Nullable();
        Alter.Table("partyaddresses").InSchema(sql.SchemaName)
            .AddColumn("sourcerecordtype").AsString(128).Nullable();
        Alter.Table("partyaddresses").InSchema(sql.SchemaName)
            .AddColumn("sourcerecordid").AsString(256).Nullable();
        Alter.Table("partyaddresses").InSchema(sql.SchemaName)
            .AddColumn("sourcefield").AsString(256).Nullable();
    }
}

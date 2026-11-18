using FluentMigrator;
using WawiCallPro.Database.Schema;

namespace WawiCallPro.Database.Migrations;

/// <summary>Retains every original mail field and distinguishes complete contact snapshots from legacy rows.</summary>
[Migration(14, "Complete party and contact mail fields")]
public sealed class Migration014PartyMailFields(DatabaseType databaseType) : ForwardOnlyMigration
{
    public override void Up()
    {
        var sql = new SchemaSql(databaseType);
        var column = Alter.Table("parties").InSchema(sql.SchemaName).AddColumn("contactfieldsversion");
        if (databaseType == DatabaseType.Sqlite)
            column.AsCustom("INTEGER CONSTRAINT ck_parties_contactfields CHECK (contactfieldsversion BETWEEN 0 AND 1)").NotNullable().WithDefaultValue(0);
        else
        {
            column.AsInt32().NotNullable().WithDefaultValue(0);
            Execute.Sql($"ALTER TABLE {sql.Table("parties")} ADD CONSTRAINT {sql.Quote("ck_parties_contactfields")} CHECK ({sql.IntegerCheck("contactfieldsversion", 0, 1)})");
        }
        var emails = Create.Table("partyemails").InSchema(sql.SchemaName)
            .WithColumn("id").AsCustom(sql.HexType(32)).NotNullable().PrimaryKey("pk_partyemails")
            .WithColumn("partyid").AsCustom(sql.HexType(32)).NotNullable()
            .WithColumn("position").AsInt32().NotNullable()
            .WithColumn("originalvalue").AsString(2048).NotNullable()
            .WithColumn("useformatching").AsInt32().NotNullable()
            .WithColumn("sourcerecordtype").AsString(64).NotNullable()
            .WithColumn("sourcerecordid").AsString(256).NotNullable()
            .WithColumn("sourcefieldname").AsString(128).NotNullable();
        emails.WithForeignKeyConstraint(sql, "fk_partyemails_party", ["partyid"], "parties", "id");
        emails.WithCheckConstraint(sql, "ck_partyemails_id", sql.Hex(sql.Quote("id"), 32));
        emails.WithCheckConstraint(sql, "ck_partyemails_position", sql.IntegerCheck("position", 0, int.MaxValue));
        emails.WithCheckConstraint(sql, "ck_partyemails_value", sql.TextCheck("originalvalue", 2048, nonBlank: true));
        emails.WithCheckConstraint(sql, "ck_partyemails_enabled", sql.IntegerCheck("useformatching", 0, 1));
        emails.WithCheckConstraint(sql, "ck_partyemails_type", sql.TextCheck("sourcerecordtype", 64, nonBlank: true));
        emails.WithCheckConstraint(sql, "ck_partyemails_sourceid", sql.TextCheck("sourcerecordid", 256, nonBlank: true));
        emails.WithCheckConstraint(sql, "ck_partyemails_field", sql.TextCheck("sourcefieldname", 128, nonBlank: true));
        Create.Index("ux_partyemails_position").OnTable("partyemails").InSchema(sql.SchemaName).OnColumn("partyid").Ascending().OnColumn("position").Ascending().WithOptions().Unique();
    }
}

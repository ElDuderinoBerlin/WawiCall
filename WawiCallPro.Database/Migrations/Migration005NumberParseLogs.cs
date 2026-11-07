using FluentMigrator;
using WawiCallPro.Database.Schema;

namespace WawiCallPro.Database.Migrations;

/// <summary>Adds append-only number parsing observations independent of the replaceable cache.</summary>
[Migration(5, "Number parsing observations")]
public sealed class Migration005NumberParseLogs(DatabaseType databaseType) : ForwardOnlyMigration
{
    public override void Up()
    {
        var sql = new SchemaSql(databaseType);

        var logs = Create.Table("numberparselogs").InSchema(sql.SchemaName)
            .WithColumn("id").AsCustom(sql.HexType(32)).NotNullable().PrimaryKey("pk_numberparselogs")
            .WithColumn("companyid").AsCustom(sql.HexType(32)).NotNullable()
            .WithColumn("partykeyhash").AsCustom(sql.HexType(64)).NotNullable()
            .WithColumn("kind").AsInt32().NotNullable()
            .WithColumn("externalid").AsString(256).NotNullable()
            .WithColumn("ownerexternalid").AsString(256).Nullable()
            .WithColumn("sourcerecordtype").AsString(64).NotNullable()
            .WithColumn("sourcerecordid").AsString(256).NotNullable()
            .WithColumn("sourcefieldname").AsString(128).NotNullable()
            .WithColumn("phonekind").AsInt32().NotNullable()
            .WithColumn("originalvalue").AsString(2048).NotNullable()
            .WithColumn("occurredatutc").AsInt64().NotNullable()
            .WithColumn("code").AsString(64).NotNullable();
        logs.WithCheckConstraint(sql, "ck_numberparselogs_id", $"{sql.Hex(sql.Quote("id"), 32)} AND {sql.Quote("id")} <> '00000000000000000000000000000000'");
        logs.WithCheckConstraint(sql, "ck_numberparselogs_companyid", $"{sql.Hex(sql.Quote("companyid"), 32)} AND {sql.Quote("companyid")} <> '00000000000000000000000000000000'");
        logs.WithCheckConstraint(sql, "ck_numberparselogs_partykeyhash", sql.Hex(sql.Quote("partykeyhash"), 64));
        logs.WithCheckConstraint(sql, "ck_numberparselogs_kind", sql.IntegerCheck("kind", 1, 4));
        logs.WithCheckConstraint(sql, "ck_numberparselogs_externalid", sql.TextCheck("externalid", 256, nonBlank: true));
        logs.WithCheckConstraint(sql, "ck_numberparselogs_ownerexternalid", sql.TextCheck("ownerexternalid", 256, nullable: true, nonBlank: true));
        logs.WithCheckConstraint(sql, "ck_numberparselogs_owner", "([kind] IN (1,2) AND [ownerexternalid] IS NULL) OR ([kind] IN (3,4) AND [ownerexternalid] IS NOT NULL)");
        logs.WithCheckConstraint(sql, "ck_numberparselogs_sourcerecordtype", sql.TextCheck("sourcerecordtype", 64, nonBlank: true));
        logs.WithCheckConstraint(sql, "ck_numberparselogs_sourcerecordid", sql.TextCheck("sourcerecordid", 256, nonBlank: true));
        logs.WithCheckConstraint(sql, "ck_numberparselogs_sourcefieldname", sql.TextCheck("sourcefieldname", 128, nonBlank: true));
        logs.WithCheckConstraint(sql, "ck_numberparselogs_phonekind", sql.IntegerCheck("phonekind", 1, 3));
        logs.WithCheckConstraint(sql, "ck_numberparselogs_originalvalue", sql.TextCheck("originalvalue", 2048));
        logs.WithCheckConstraint(sql, "ck_numberparselogs_occurredatutc", sql.IntegerCheck("occurredatutc", 0, 3155378975999999999));
        logs.WithCheckConstraint(sql, "ck_numberparselogs_code", sql.TextCheck("code", 64, nonBlank: true));
        logs.WithCheckConstraint(sql, "ck_numberparselogs_code_ascii", sql.AllowedAscii(sql.Quote("code"), "A-Za-z0-9_.-"));
        logs.WithForeignKeyConstraint(sql, "fk_numberparselogs_company", ["companyid"], "companies", "id");
        Create.Index("ix_numberparselogs_company").OnTable("numberparselogs").InSchema(sql.SchemaName).OnColumn("companyid").Ascending().OnColumn("occurredatutc").Descending().OnColumn("id").Ascending();
        Create.Index("ix_numberparselogs_party").OnTable("numberparselogs").InSchema(sql.SchemaName).OnColumn("partykeyhash").Ascending().OnColumn("occurredatutc").Descending().OnColumn("id").Ascending();
    }
}

using FluentMigrator;
using WawiCallPro.Database.Schema;

namespace WawiCallPro.Database.Migrations;

/// <summary>Adds recording metadata; files remain outside the database.</summary>
[Migration(4, "Recording metadata")]
public sealed class Migration004Recordings(DatabaseType databaseType) : ForwardOnlyMigration
{
    public override void Up()
    {
        var sql = new SchemaSql(databaseType);

        var recordings = Create.Table("recordings").InSchema(sql.SchemaName)
            .WithColumn("id").AsCustom(sql.HexType(32)).NotNullable().PrimaryKey("pk_recordings")
            .WithColumn("callid").AsCustom(sql.HexType(32)).NotNullable()
            .WithColumn("relativepath").AsString(1024).NotNullable()
            .WithColumn("format").AsString(32).NotNullable()
            .WithColumn("durationticks").AsInt64().Nullable()
            .WithColumn("availability").AsInt32().NotNullable()
            .WithColumn("writeversion").AsInt64().NotNullable()
            .WithColumn("createdatutc").AsInt64().NotNullable()
            .WithColumn("updatedatutc").AsInt64().NotNullable();
        recordings.WithCheckConstraint(sql, "ck_recordings_id", $"{sql.Hex(sql.Quote("id"), 32)} AND {sql.Quote("id")} <> '00000000000000000000000000000000'");
        recordings.WithCheckConstraint(sql, "ck_recordings_callid", $"{sql.Hex(sql.Quote("callid"), 32)} AND {sql.Quote("callid")} <> '00000000000000000000000000000000'");
        recordings.WithCheckConstraint(sql, "ck_recordings_relativepath", sql.TextCheck("relativepath", 1024, nonBlank: true));
        recordings.WithCheckConstraint(sql, "ck_recordings_format", sql.TextCheck("format", 32, nonBlank: true));
        recordings.WithCheckConstraint(sql, "ck_recordings_durationticks", sql.IntegerCheck("durationticks", 0, long.MaxValue, nullable: true));
        recordings.WithCheckConstraint(sql, "ck_recordings_availability", sql.IntegerCheck("availability", 0, 3));
        recordings.WithCheckConstraint(sql, "ck_recordings_writeversion", sql.IntegerCheck("writeversion", 1, long.MaxValue));
        recordings.WithCheckConstraint(sql, "ck_recordings_createdatutc", sql.IntegerCheck("createdatutc", 0, 3155378975999999999));
        recordings.WithCheckConstraint(sql, "ck_recordings_updatedatutc", sql.IntegerCheck("updatedatutc", 0, 3155378975999999999));
        recordings.WithCheckConstraint(sql, "ck_recordings_chronology", "[createdatutc] <= [updatedatutc]");
        recordings.WithCheckConstraint(sql, "ck_recordings_path", "[relativepath] NOT LIKE '/%' AND [relativepath] NOT LIKE '%/' AND [relativepath] NOT LIKE '%:%' AND [relativepath] NOT LIKE '%//%' AND [relativepath] <> '.' AND [relativepath] <> '..' AND [relativepath] NOT LIKE './%' AND [relativepath] NOT LIKE '../%' AND [relativepath] NOT LIKE '%/./%' AND [relativepath] NOT LIKE '%/../%' AND [relativepath] NOT LIKE '%/.' AND [relativepath] NOT LIKE '%/..'");
        recordings.WithCheckConstraint(sql, "ck_recordings_separator", $"{sql.Length(sql.Quote("relativepath"))} = {sql.Length($"REPLACE({sql.Quote("relativepath")}, {sql.Character(92)}, '')")}");
        recordings.WithForeignKeyConstraint(sql, "fk_recordings_call", ["callid"], "phonecalls", "id");
        Create.Index("ix_recordings_call").OnTable("recordings").InSchema(sql.SchemaName).OnColumn("callid").Ascending().OnColumn("id").Ascending();
    }
}

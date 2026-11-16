using FluentMigrator;
using WawiCallPro.Database.Schema;

namespace WawiCallPro.Database.Migrations;

/// <summary>Optional canonical WAV/MP3 references. Does not invent artifacts for legacy recordings.</summary>
[Migration(11, "Separate recording file artifacts")]
public sealed class Migration011RecordingArtifacts(DatabaseType databaseType) : ForwardOnlyMigration
{
    public override void Up()
    {
        var sql = new SchemaSql(databaseType);
        var table = Create.Table("recordingartifacts").InSchema(sql.SchemaName)
            .WithColumn("id").AsCustom(sql.HexType(32)).NotNullable().PrimaryKey("pk_recordingartifacts")
            .WithColumn("recordingid").AsCustom(sql.HexType(32)).NotNullable()
            .WithColumn("format").AsInt32().NotNullable()
            .WithColumn("relativepath").AsString(1024).NotNullable()
            .WithColumn("availability").AsInt32().NotNullable()
            .WithColumn("bytelength").AsInt64().Nullable()
            .WithColumn("sha256").AsCustom(sql.HexType(64)).Nullable()
            .WithColumn("writeversion").AsInt64().NotNullable()
            .WithColumn("createdatutc").AsInt64().NotNullable()
            .WithColumn("updatedatutc").AsInt64().NotNullable();
        table.WithForeignKeyConstraint(sql, "fk_recordingartifacts_recording", ["recordingid"], "recordings", "id");
        table.WithCheckConstraint(sql, "ck_recordingartifacts_id", sql.Hex(sql.Quote("id"), 32) + " AND [id] <> '00000000000000000000000000000000'");
        table.WithCheckConstraint(sql, "ck_recordingartifacts_recording", sql.Hex(sql.Quote("recordingid"), 32) + " AND [recordingid] <> '00000000000000000000000000000000'");
        table.WithCheckConstraint(sql, "ck_recordingartifacts_format", sql.IntegerCheck("format", 1, 2));
        table.WithCheckConstraint(sql, "ck_recordingartifacts_pathtext", sql.TextCheck("relativepath", 1024, nonBlank: true));
        table.WithCheckConstraint(sql, "ck_recordingartifacts_path", "[relativepath] NOT LIKE '/%' AND [relativepath] NOT LIKE '%/' AND [relativepath] NOT LIKE '%:%' AND [relativepath] NOT LIKE '%//%' AND [relativepath] <> '.' AND [relativepath] <> '..' AND [relativepath] NOT LIKE './%' AND [relativepath] NOT LIKE '../%' AND [relativepath] NOT LIKE '%/./%' AND [relativepath] NOT LIKE '%/../%' AND [relativepath] NOT LIKE '%/.' AND [relativepath] NOT LIKE '%/..'");
        table.WithCheckConstraint(sql, "ck_recordingartifacts_separator", $"{sql.Length(sql.Quote("relativepath"))} = {sql.Length($"REPLACE({sql.Quote("relativepath")}, {sql.Character(92)}, '')")}");
        table.WithCheckConstraint(sql, "ck_recordingartifacts_extension", "([format] = 1 AND LOWER([relativepath]) LIKE '%.wav') OR ([format] = 2 AND LOWER([relativepath]) LIKE '%.mp3')");
        table.WithCheckConstraint(sql, "ck_recordingartifacts_availability", sql.IntegerCheck("availability", 0, 3));
        table.WithCheckConstraint(sql, "ck_recordingartifacts_length", sql.IntegerCheck("bytelength", 1, long.MaxValue, nullable: true));
        table.WithCheckConstraint(sql, "ck_recordingartifacts_digest", "[sha256] IS NULL OR (" + sql.Hex(sql.Quote("sha256"), 64) + ")");
        table.WithCheckConstraint(sql, "ck_recordingartifacts_result", "([availability] IN (0,3) AND [bytelength] IS NULL AND [sha256] IS NULL) OR ([availability] IN (1,2) AND [bytelength] IS NOT NULL AND [sha256] IS NOT NULL)");
        table.WithCheckConstraint(sql, "ck_recordingartifacts_version", sql.IntegerCheck("writeversion", 1, long.MaxValue));
        table.WithCheckConstraint(sql, "ck_recordingartifacts_created", sql.IntegerCheck("createdatutc", 0, 3155378975999999999));
        table.WithCheckConstraint(sql, "ck_recordingartifacts_updated", sql.IntegerCheck("updatedatutc", 0, 3155378975999999999));
        table.WithCheckConstraint(sql, "ck_recordingartifacts_chronology", "[createdatutc] <= [updatedatutc]");
        Create.Index("ux_recordingartifacts_format").OnTable("recordingartifacts").InSchema(sql.SchemaName)
            .OnColumn("recordingid").Ascending().OnColumn("format").Ascending().WithOptions().Unique();
    }
}

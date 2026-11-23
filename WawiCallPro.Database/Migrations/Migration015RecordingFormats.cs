using Dapper;
using FluentMigrator;
using WawiCallPro.Database.Schema;

namespace WawiCallPro.Database.Migrations;

/// <summary>Extends recording artifacts without changing the identity or state of existing conversion jobs.</summary>
[Migration(15, "Ogg Vorbis and WMA recording artifacts")]
public sealed class Migration015RecordingFormats(DatabaseType databaseType) : ForwardOnlyMigration
{
    private const string ExtensionCheck = "([format] = 1 AND LOWER([relativepath]) LIKE '%.wav')" +
        " OR ([format] = 2 AND LOWER([relativepath]) LIKE '%.mp3')" +
        " OR ([format] = 3 AND LOWER([relativepath]) LIKE '%.ogg')" +
        " OR ([format] = 4 AND LOWER([relativepath]) LIKE '%.wma')";

    public override void Up()
    {
        var sql = new SchemaSql(databaseType);
        if (databaseType == DatabaseType.Sqlite)
        {
            RebuildSqliteTables(sql);
            return;
        }

        if (databaseType == DatabaseType.MySql)
        {
            Execute.WithConnection((connection, transaction) =>
            {
                var version = connection.ExecuteScalar<string>("SELECT VERSION()", transaction: transaction);
                var drop = version?.Contains("MariaDB", StringComparison.OrdinalIgnoreCase) == true ? "DROP CONSTRAINT" : "DROP CHECK";
                connection.Execute($"ALTER TABLE {sql.Table("recordingartifacts")} " +
                    $"{drop} {sql.Quote("ck_recordingartifacts_format")}, {drop} {sql.Quote("ck_recordingartifacts_extension")}, " +
                    $"ADD CONSTRAINT {sql.Quote("ck_recordingartifacts_format")} CHECK ({sql.IntegerCheck("format", 1, 4)}), " +
                    $"ADD CONSTRAINT {sql.Quote("ck_recordingartifacts_extension")} CHECK ({sql.Expression(ExtensionCheck)})",
                    transaction: transaction);
            });
        }
        else
        {
            var artifacts = sql.Table("recordingartifacts");
            Execute.Sql($"ALTER TABLE {artifacts} DROP CONSTRAINT {sql.Quote("ck_recordingartifacts_format")}");
            Execute.Sql($"ALTER TABLE {artifacts} DROP CONSTRAINT {sql.Quote("ck_recordingartifacts_extension")}");
            Execute.Sql($"ALTER TABLE {artifacts} ADD CONSTRAINT {sql.Quote("ck_recordingartifacts_format")} CHECK ({sql.IntegerCheck("format", 1, 4)})");
            Execute.Sql($"ALTER TABLE {artifacts} ADD CONSTRAINT {sql.Quote("ck_recordingartifacts_extension")} CHECK ({sql.Expression(ExtensionCheck)})");
        }
        Create.Index("ix_recordingconversions_source").OnTable("recordingconversions").InSchema(sql.SchemaName).OnColumn("sourceartifactid").Ascending();
        Delete.Index("ux_recordingconversions_source").OnTable("recordingconversions").InSchema(sql.SchemaName);
    }

    private void RebuildSqliteTables(SchemaSql sql)
    {
        var table = Create.Table("recordingartifacts_v15").InSchema(sql.SchemaName)
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
        table.WithCheckConstraint(sql, "ck_recordingartifacts_format", sql.IntegerCheck("format", 1, 4));
        table.WithCheckConstraint(sql, "ck_recordingartifacts_pathtext", sql.TextCheck("relativepath", 1024, nonBlank: true));
        table.WithCheckConstraint(sql, "ck_recordingartifacts_path", "[relativepath] NOT LIKE '/%' AND [relativepath] NOT LIKE '%/' AND [relativepath] NOT LIKE '%:%' AND [relativepath] NOT LIKE '%//%' AND [relativepath] <> '.' AND [relativepath] <> '..' AND [relativepath] NOT LIKE './%' AND [relativepath] NOT LIKE '../%' AND [relativepath] NOT LIKE '%/./%' AND [relativepath] NOT LIKE '%/../%' AND [relativepath] NOT LIKE '%/.' AND [relativepath] NOT LIKE '%/..'");
        table.WithCheckConstraint(sql, "ck_recordingartifacts_separator", $"{sql.Length(sql.Quote("relativepath"))} = {sql.Length($"REPLACE({sql.Quote("relativepath")}, {sql.Character(92)}, '')")}");
        table.WithCheckConstraint(sql, "ck_recordingartifacts_extension", ExtensionCheck);
        table.WithCheckConstraint(sql, "ck_recordingartifacts_availability", sql.IntegerCheck("availability", 0, 3));
        table.WithCheckConstraint(sql, "ck_recordingartifacts_length", sql.IntegerCheck("bytelength", 1, long.MaxValue, nullable: true));
        table.WithCheckConstraint(sql, "ck_recordingartifacts_digest", "[sha256] IS NULL OR (" + sql.Hex(sql.Quote("sha256"), 64) + ")");
        table.WithCheckConstraint(sql, "ck_recordingartifacts_result", "([availability] IN (0,3) AND [bytelength] IS NULL AND [sha256] IS NULL) OR ([availability] IN (1,2) AND [bytelength] IS NOT NULL AND [sha256] IS NOT NULL)");
        table.WithCheckConstraint(sql, "ck_recordingartifacts_version", sql.IntegerCheck("writeversion", 1, long.MaxValue));
        table.WithCheckConstraint(sql, "ck_recordingartifacts_created", sql.IntegerCheck("createdatutc", 0, 3155378975999999999));
        table.WithCheckConstraint(sql, "ck_recordingartifacts_updated", sql.IntegerCheck("updatedatutc", 0, 3155378975999999999));
        table.WithCheckConstraint(sql, "ck_recordingartifacts_chronology", "[createdatutc] <= [updatedatutc]");

        var conversions = Create.Table("recordingconversions_v15").InSchema(sql.SchemaName)
            .WithColumn("id").AsCustom(sql.HexType(32)).NotNullable().PrimaryKey("pk_recordingconversions")
            .WithColumn("sourceartifactid").AsCustom(sql.HexType(32)).NotNullable()
            .WithColumn("targetartifactid").AsCustom(sql.HexType(32)).NotNullable()
            .WithColumn("samplerate").AsInt32().NotNullable()
            .WithColumn("channels").AsInt32().NotNullable()
            .WithColumn("bitrate").AsInt32().NotNullable()
            .WithColumn("state").AsInt32().NotNullable()
            .WithColumn("attempts").AsInt32().NotNullable()
            .WithColumn("attemptid").AsCustom(sql.HexType(32)).Nullable()
            .WithColumn("failure").AsInt32().Nullable()
            .WithColumn("writeversion").AsInt64().NotNullable()
            .WithColumn("createdatutc").AsInt64().NotNullable()
            .WithColumn("updatedatutc").AsInt64().NotNullable();
        conversions.WithForeignKeyConstraint(sql, "fk_recordingconversions_source", ["sourceartifactid"], "recordingartifacts_v15", "id");
        conversions.WithForeignKeyConstraint(sql, "fk_recordingconversions_target", ["targetartifactid"], "recordingartifacts_v15", "id");
        conversions.WithCheckConstraint(sql, "ck_recordingconversions_id", sql.Hex(sql.Quote("id"), 32) + " AND [id] <> '00000000000000000000000000000000'");
        conversions.WithCheckConstraint(sql, "ck_recordingconversions_source", sql.Hex(sql.Quote("sourceartifactid"), 32) + " AND [sourceartifactid] <> [targetartifactid]");
        conversions.WithCheckConstraint(sql, "ck_recordingconversions_target", sql.Hex(sql.Quote("targetartifactid"), 32));
        conversions.WithCheckConstraint(sql, "ck_recordingconversions_rate", sql.IntegerCheck("samplerate", 8000, 48000) + " AND [samplerate] IN (8000,16000,32000,44100,48000)");
        conversions.WithCheckConstraint(sql, "ck_recordingconversions_channels", sql.IntegerCheck("channels", 1, 2));
        conversions.WithCheckConstraint(sql, "ck_recordingconversions_bitrate", sql.IntegerCheck("bitrate", 8000, 320000) + " AND [bitrate] % 1000 = 0");
        conversions.WithCheckConstraint(sql, "ck_recordingconversions_state", sql.IntegerCheck("state", 0, 3));
        conversions.WithCheckConstraint(sql, "ck_recordingconversions_attempts", sql.IntegerCheck("attempts", 0, int.MaxValue));
        conversions.WithCheckConstraint(sql, "ck_recordingconversions_attemptid", "[attemptid] IS NULL OR (" + sql.Hex(sql.Quote("attemptid"), 32) + " AND [attemptid] <> '00000000000000000000000000000000')");
        conversions.WithCheckConstraint(sql, "ck_recordingconversions_failure", sql.IntegerCheck("failure", 1, 9, nullable: true));
        conversions.WithCheckConstraint(sql, "ck_recordingconversions_result", "([state] = 0 AND [attemptid] IS NULL AND [failure] IS NULL) OR ([state] IN (1,2) AND [attempts] > 0 AND [attemptid] IS NOT NULL AND [failure] IS NULL) OR ([state] = 3 AND [attempts] > 0 AND [attemptid] IS NOT NULL AND [failure] IS NOT NULL)");
        conversions.WithCheckConstraint(sql, "ck_recordingconversions_version", sql.IntegerCheck("writeversion", 1, long.MaxValue));
        conversions.WithCheckConstraint(sql, "ck_recordingconversions_created", sql.IntegerCheck("createdatutc", 0, 3155378975999999999));
        conversions.WithCheckConstraint(sql, "ck_recordingconversions_updated", sql.IntegerCheck("updatedatutc", 0, 3155378975999999999));
        conversions.WithCheckConstraint(sql, "ck_recordingconversions_chronology", "[createdatutc] <= [updatedatutc]");

        CopyRows(sql, "recordingartifacts", "recordingartifacts_v15",
            "id,recordingid,format,relativepath,availability,bytelength,sha256,writeversion,createdatutc,updatedatutc");
        CopyRows(sql, "recordingconversions", "recordingconversions_v15",
            "id,sourceartifactid,targetartifactid,samplerate,channels,bitrate,state,attempts,attemptid,failure,writeversion,createdatutc,updatedatutc");
        Delete.Table("recordingconversions").InSchema(sql.SchemaName);
        Delete.Table("recordingartifacts").InSchema(sql.SchemaName);
        Rename.Table("recordingartifacts_v15").InSchema(sql.SchemaName).To("recordingartifacts");
        Rename.Table("recordingconversions_v15").InSchema(sql.SchemaName).To("recordingconversions");

        Create.Index("ux_recordingartifacts_format").OnTable("recordingartifacts").InSchema(sql.SchemaName)
            .OnColumn("recordingid").Ascending().OnColumn("format").Ascending().WithOptions().Unique();
        Create.Index("ix_recordingconversions_source").OnTable("recordingconversions").InSchema(sql.SchemaName).OnColumn("sourceartifactid").Ascending();
        Create.Index("ux_recordingconversions_target").OnTable("recordingconversions").InSchema(sql.SchemaName).OnColumn("targetartifactid").Ascending().WithOptions().Unique();
    }

    private void CopyRows(SchemaSql sql, string source, string destination, string columns) =>
        Execute.Sql($"INSERT INTO {sql.Table(destination)} ({columns}) SELECT {columns} FROM {sql.Table(source)}");
}

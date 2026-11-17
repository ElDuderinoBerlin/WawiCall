using FluentMigrator;
using WawiCallPro.Database.Schema;

namespace WawiCallPro.Database.Migrations;

/// <summary>Optional conversion jobs; existing recordings/artifacts acquire no implicit jobs.</summary>
[Migration(12, "Durable recording conversion jobs")]
public sealed class Migration012RecordingConversions(DatabaseType databaseType) : ForwardOnlyMigration
{
    public override void Up()
    {
        var sql = new SchemaSql(databaseType);
        var table = Create.Table("recordingconversions").InSchema(sql.SchemaName)
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
        table.WithForeignKeyConstraint(sql, "fk_recordingconversions_source", ["sourceartifactid"], "recordingartifacts", "id");
        table.WithForeignKeyConstraint(sql, "fk_recordingconversions_target", ["targetartifactid"], "recordingartifacts", "id");
        table.WithCheckConstraint(sql, "ck_recordingconversions_id", sql.Hex(sql.Quote("id"), 32) + " AND [id] <> '00000000000000000000000000000000'");
        table.WithCheckConstraint(sql, "ck_recordingconversions_source", sql.Hex(sql.Quote("sourceartifactid"), 32) + " AND [sourceartifactid] <> [targetartifactid]");
        table.WithCheckConstraint(sql, "ck_recordingconversions_target", sql.Hex(sql.Quote("targetartifactid"), 32));
        table.WithCheckConstraint(sql, "ck_recordingconversions_rate", sql.IntegerCheck("samplerate", 8000, 48000) + " AND [samplerate] IN (8000,16000,32000,44100,48000)");
        table.WithCheckConstraint(sql, "ck_recordingconversions_channels", sql.IntegerCheck("channels", 1, 2));
        table.WithCheckConstraint(sql, "ck_recordingconversions_bitrate", sql.IntegerCheck("bitrate", 8000, 320000) + " AND [bitrate] % 1000 = 0");
        table.WithCheckConstraint(sql, "ck_recordingconversions_state", sql.IntegerCheck("state", 0, 3));
        table.WithCheckConstraint(sql, "ck_recordingconversions_attempts", sql.IntegerCheck("attempts", 0, int.MaxValue));
        table.WithCheckConstraint(sql, "ck_recordingconversions_attemptid", "[attemptid] IS NULL OR (" + sql.Hex(sql.Quote("attemptid"), 32) + " AND [attemptid] <> '00000000000000000000000000000000')");
        table.WithCheckConstraint(sql, "ck_recordingconversions_failure", sql.IntegerCheck("failure", 1, 9, nullable: true));
        table.WithCheckConstraint(sql, "ck_recordingconversions_result", "([state] = 0 AND [attemptid] IS NULL AND [failure] IS NULL) OR ([state] IN (1,2) AND [attempts] > 0 AND [attemptid] IS NOT NULL AND [failure] IS NULL) OR ([state] = 3 AND [attempts] > 0 AND [attemptid] IS NOT NULL AND [failure] IS NOT NULL)");
        table.WithCheckConstraint(sql, "ck_recordingconversions_version", sql.IntegerCheck("writeversion", 1, long.MaxValue));
        table.WithCheckConstraint(sql, "ck_recordingconversions_created", sql.IntegerCheck("createdatutc", 0, 3155378975999999999));
        table.WithCheckConstraint(sql, "ck_recordingconversions_updated", sql.IntegerCheck("updatedatutc", 0, 3155378975999999999));
        table.WithCheckConstraint(sql, "ck_recordingconversions_chronology", "[createdatutc] <= [updatedatutc]");
        Create.Index("ux_recordingconversions_source").OnTable("recordingconversions").InSchema(sql.SchemaName).OnColumn("sourceartifactid").Ascending().WithOptions().Unique();
        Create.Index("ux_recordingconversions_target").OnTable("recordingconversions").InSchema(sql.SchemaName).OnColumn("targetartifactid").Ascending().WithOptions().Unique();
    }
}

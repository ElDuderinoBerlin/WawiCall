using FluentMigrator;
using WawiCallPro.Database.Schema;

namespace WawiCallPro.Database.Migrations;

/// <summary>Adds optional capture lifecycle metadata; all earlier recording rows remain unchanged.</summary>
[Migration(10, "Recording capture lifecycle")]
public sealed class Migration010RecordingCaptures(DatabaseType databaseType) : ForwardOnlyMigration
{
    public override void Up()
    {
        var sql = new SchemaSql(databaseType);
        var table = Create.Table("recordingcaptures").InSchema(sql.SchemaName)
            .WithColumn("id").AsCustom(sql.HexType(32)).NotNullable().PrimaryKey("pk_recordingcaptures")
            .WithColumn("startedatutc").AsInt64().NotNullable()
            .WithColumn("endedatutc").AsInt64().Nullable()
            .WithColumn("state").AsInt32().NotNullable()
            .WithColumn("samplerate").AsInt32().NotNullable()
            .WithColumn("channels").AsInt32().NotNullable()
            .WithColumn("framecount").AsInt64().Nullable()
            .WithColumn("failure").AsInt32().Nullable()
            .WithColumn("writeversion").AsInt64().NotNullable()
            .WithColumn("updatedatutc").AsInt64().NotNullable();
        table.WithForeignKeyConstraint(sql, "fk_recordingcaptures_recording", ["id"], "recordings", "id");
        table.WithCheckConstraint(sql, "ck_recordingcaptures_id", sql.Hex(sql.Quote("id"), 32) + " AND [id] <> '00000000000000000000000000000000'");
        table.WithCheckConstraint(sql, "ck_recordingcaptures_start", sql.IntegerCheck("startedatutc", 0, 3155378975999999999));
        table.WithCheckConstraint(sql, "ck_recordingcaptures_end", sql.IntegerCheck("endedatutc", 0, 3155378975999999999, nullable: true));
        table.WithCheckConstraint(sql, "ck_recordingcaptures_state", sql.IntegerCheck("state", 0, 4));
        table.WithCheckConstraint(sql, "ck_recordingcaptures_rate", sql.IntegerCheck("samplerate", 8000, 48000) + " AND [samplerate] IN (8000,16000,32000,44100,48000)");
        table.WithCheckConstraint(sql, "ck_recordingcaptures_channels", sql.IntegerCheck("channels", 1, 2));
        table.WithCheckConstraint(sql, "ck_recordingcaptures_frames", sql.IntegerCheck("framecount", 1, (int.MaxValue - 44L) / 2, nullable: true));
        table.WithCheckConstraint(sql, "ck_recordingcaptures_size", "[framecount] IS NULL OR ([framecount] <= 28800 * [samplerate] AND [framecount] * [channels] * 2 <= 2147483603)");
        table.WithCheckConstraint(sql, "ck_recordingcaptures_failure", sql.IntegerCheck("failure", 1, 9, nullable: true));
        table.WithCheckConstraint(sql, "ck_recordingcaptures_version", sql.IntegerCheck("writeversion", 1, long.MaxValue));
        table.WithCheckConstraint(sql, "ck_recordingcaptures_updated", sql.IntegerCheck("updatedatutc", 0, 3155378975999999999));
        table.WithCheckConstraint(sql, "ck_recordingcaptures_times", "[startedatutc] <= [updatedatutc] AND ([endedatutc] IS NULL OR ([endedatutc] >= [startedatutc] AND [endedatutc] <= [updatedatutc]))");
        table.WithCheckConstraint(sql, "ck_recordingcaptures_result", "([state] IN (0,1) AND [endedatutc] IS NULL AND [framecount] IS NULL AND [failure] IS NULL) OR ([state] = 2 AND [endedatutc] IS NOT NULL AND [framecount] IS NOT NULL AND [failure] IS NULL) OR ([state] IN (3,4) AND [framecount] IS NULL AND [failure] IS NOT NULL)");
    }
}

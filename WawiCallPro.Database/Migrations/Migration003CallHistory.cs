using FluentMigrator;
using WawiCallPro.Database.Schema;

namespace WawiCallPro.Database.Migrations;

/// <summary>Adds call snapshots, historical candidates, events and independent notes.</summary>
[Migration(3, "Call history candidates events and notes")]
public sealed class Migration003CallHistory(DatabaseType databaseType) : ForwardOnlyMigration
{
    public override void Up()
    {
        var sql = new SchemaSql(databaseType);

        var calls = Create.Table("phonecalls").InSchema(sql.SchemaName)
            .WithColumn("id").AsCustom(sql.HexType(32)).NotNullable().PrimaryKey("pk_phonecalls")
            .WithColumn("companyid").AsCustom(sql.HexType(32)).Nullable()
            .WithColumn("writeversion").AsInt64().NotNullable()
            .WithColumn("revision").AsInt64().NotNullable()
            .WithColumn("workstationid").AsCustom(sql.HexType(32)).NotNullable()
            .WithColumn("machinename").AsString(256).NotNullable()
            .WithColumn("windowsusername").AsString(256).NotNullable()
            .WithColumn("direction").AsInt32().NotNullable()
            .WithColumn("state").AsInt32().NotNullable()
            .WithColumn("localpresentation").AsInt32().NotNullable()
            .WithColumn("localoriginalvalue").AsString(2048).Nullable()
            .WithColumn("locale164").AsString(16).Nullable()
            .WithColumn("localextension").AsString(32).Nullable()
            .WithColumn("remotepresentation").AsInt32().NotNullable()
            .WithColumn("remoteoriginalvalue").AsString(2048).Nullable()
            .WithColumn("remotee164").AsString(16).Nullable()
            .WithColumn("remoteextension").AsString(32).Nullable()
            .WithColumn("startedatutc").AsInt64().NotNullable()
            .WithColumn("statechangedatutc").AsInt64().NotNullable()
            .WithColumn("answeredatutc").AsInt64().Nullable()
            .WithColumn("endedatutc").AsInt64().Nullable()
            .WithColumn("endreason").AsInt32().Nullable();
        calls.WithCheckConstraint(sql, "ck_phonecalls_id", $"{sql.Hex(sql.Quote("id"), 32)} AND {sql.Quote("id")} <> '00000000000000000000000000000000'");
        calls.WithCheckConstraint(sql, "ck_phonecalls_companyid", $"{sql.Quote("companyid")} IS NULL OR (" + $"{sql.Hex(sql.Quote("companyid"), 32)} AND {sql.Quote("companyid")} <> '00000000000000000000000000000000'" + ")");
        calls.WithCheckConstraint(sql, "ck_phonecalls_writeversion", sql.IntegerCheck("writeversion", 1, long.MaxValue));
        calls.WithCheckConstraint(sql, "ck_phonecalls_revision", sql.IntegerCheck("revision", 0, long.MaxValue));
        calls.WithCheckConstraint(sql, "ck_phonecalls_workstationid", $"{sql.Hex(sql.Quote("workstationid"), 32)} AND {sql.Quote("workstationid")} <> '00000000000000000000000000000000'");
        calls.WithCheckConstraint(sql, "ck_phonecalls_machinename", sql.TextCheck("machinename", 256, nonBlank: true));
        calls.WithCheckConstraint(sql, "ck_phonecalls_windowsusername", sql.TextCheck("windowsusername", 256, nonBlank: true));
        calls.WithCheckConstraint(sql, "ck_phonecalls_direction", sql.IntegerCheck("direction", 1, 2));
        calls.WithCheckConstraint(sql, "ck_phonecalls_state", sql.IntegerCheck("state", 1, 7));
        calls.WithCheckConstraint(sql, "ck_phonecalls_localpresentation", sql.IntegerCheck("localpresentation", 0, 2));
        calls.WithCheckConstraint(sql, "ck_phonecalls_localoriginalvalue", sql.TextCheck("localoriginalvalue", 2048, nullable: true));
        calls.WithCheckConstraint(sql, "ck_phonecalls_locale164", sql.TextCheck("locale164", 16, nullable: true));
        calls.WithCheckConstraint(sql, "ck_phonecalls_localextension", sql.TextCheck("localextension", 32, nullable: true));
        calls.WithCheckConstraint(sql, "ck_phonecalls_local_available", $"[localpresentation] <> 1 OR ([localoriginalvalue] IS NOT NULL AND {sql.NonBlank(sql.Quote("localoriginalvalue"))})");
        calls.WithCheckConstraint(sql, "ck_phonecalls_local_withheld", "[localpresentation] <> 2 OR ([localoriginalvalue] IS NULL AND [locale164] IS NULL AND [localextension] IS NULL)");
        calls.WithCheckConstraint(sql, "ck_phonecalls_local_e164", $"[locale164] IS NULL OR ({sql.E164(sql.Quote("locale164"))})");
        calls.WithCheckConstraint(sql, "ck_phonecalls_remotepresentation", sql.IntegerCheck("remotepresentation", 0, 2));
        calls.WithCheckConstraint(sql, "ck_phonecalls_remoteoriginalvalue", sql.TextCheck("remoteoriginalvalue", 2048, nullable: true));
        calls.WithCheckConstraint(sql, "ck_phonecalls_remotee164", sql.TextCheck("remotee164", 16, nullable: true));
        calls.WithCheckConstraint(sql, "ck_phonecalls_remoteextension", sql.TextCheck("remoteextension", 32, nullable: true));
        calls.WithCheckConstraint(sql, "ck_phonecalls_remote_available", $"[remotepresentation] <> 1 OR ([remoteoriginalvalue] IS NOT NULL AND {sql.NonBlank(sql.Quote("remoteoriginalvalue"))})");
        calls.WithCheckConstraint(sql, "ck_phonecalls_remote_withheld", "[remotepresentation] <> 2 OR ([remoteoriginalvalue] IS NULL AND [remotee164] IS NULL AND [remoteextension] IS NULL)");
        calls.WithCheckConstraint(sql, "ck_phonecalls_remote_e164", $"[remotee164] IS NULL OR ({sql.E164(sql.Quote("remotee164"))})");
        calls.WithCheckConstraint(sql, "ck_phonecalls_startedatutc", sql.IntegerCheck("startedatutc", 0, 3155378975999999999));
        calls.WithCheckConstraint(sql, "ck_phonecalls_statechangedatutc", sql.IntegerCheck("statechangedatutc", 0, 3155378975999999999));
        calls.WithCheckConstraint(sql, "ck_phonecalls_answeredatutc", sql.IntegerCheck("answeredatutc", 0, 3155378975999999999, nullable: true));
        calls.WithCheckConstraint(sql, "ck_phonecalls_endedatutc", sql.IntegerCheck("endedatutc", 0, 3155378975999999999, nullable: true));
        calls.WithCheckConstraint(sql, "ck_phonecalls_endreason", sql.IntegerCheck("endreason", 1, 10, nullable: true));
        calls.WithCheckConstraint(sql, "ck_phonecalls_direction_state", "[direction] <> 1 OR [state] <> 2");
        calls.WithCheckConstraint(sql, "ck_phonecalls_chronology", "[startedatutc] <= [statechangedatutc] AND ([answeredatutc] IS NULL OR ([answeredatutc] >= [startedatutc] AND [answeredatutc] <= [statechangedatutc]))");
        calls.WithCheckConstraint(sql, "ck_phonecalls_answer", "([state] IN (4,5) AND [answeredatutc] IS NOT NULL) OR ([state] IN (1,2,3) AND [answeredatutc] IS NULL) OR [state] IN (6,7)");
        calls.WithCheckConstraint(sql, "ck_phonecalls_end", "([state] IN (1,2,3,4,5) AND [endedatutc] IS NULL AND [endreason] IS NULL) OR ([state] IN (6,7) AND [endedatutc] IS NOT NULL AND [endedatutc] = [statechangedatutc] AND [endreason] IS NOT NULL)");
        calls.WithCheckConstraint(sql, "ck_phonecalls_reason", "[state] NOT IN (6,7) OR ([state] = 7 AND [endreason] IN (7,8,9)) OR ([state] = 6 AND (([endreason] IN (1,10) AND [answeredatutc] IS NOT NULL) OR ([endreason] = 2 AND [direction] = 1 AND [answeredatutc] IS NULL) OR ([endreason] = 5 AND [direction] = 2 AND [answeredatutc] IS NULL) OR ([endreason] IN (3,4) AND [answeredatutc] IS NULL) OR [endreason] = 6))");
        calls.WithForeignKeyConstraint(sql, "fk_phonecalls_company", ["companyid"], "companies", "id");
        Create.Index("ix_phonecalls_start").OnTable("phonecalls").InSchema(sql.SchemaName).OnColumn("startedatutc").Descending().OnColumn("id").Ascending();
        Create.Index("ix_phonecalls_workstation").OnTable("phonecalls").InSchema(sql.SchemaName).OnColumn("workstationid").Ascending().OnColumn("startedatutc").Descending().OnColumn("id").Ascending();
        Create.Index("ix_phonecalls_machine").OnTable("phonecalls").InSchema(sql.SchemaName).OnColumn("machinename").Ascending().OnColumn("startedatutc").Descending().OnColumn("id").Ascending();
        Create.Index("ix_phonecalls_user").OnTable("phonecalls").InSchema(sql.SchemaName).OnColumn("windowsusername").Ascending().OnColumn("startedatutc").Descending().OnColumn("id").Ascending();
        Create.Index("ix_phonecalls_company").OnTable("phonecalls").InSchema(sql.SchemaName).OnColumn("companyid").Ascending().OnColumn("startedatutc").Descending().OnColumn("id").Ascending();

        var candidates = Create.Table("callparties").InSchema(sql.SchemaName)
            .WithColumn("id").AsCustom(sql.HexType(32)).NotNullable().PrimaryKey("pk_callparties")
            .WithColumn("callid").AsCustom(sql.HexType(32)).NotNullable()
            .WithColumn("partykeyhash").AsCustom(sql.HexType(64)).NotNullable()
            .WithColumn("kind").AsInt32().NotNullable()
            .WithColumn("externalid").AsString(256).NotNullable()
            .WithColumn("ownerexternalid").AsString(256).Nullable()
            .WithColumn("displayname").AsString(512).NotNullable()
            .WithColumn("number").AsString(2048).Nullable()
            .WithColumn("isselected").AsInt32().NotNullable()
            .WithColumn("position").AsInt32().NotNullable();
        if (databaseType == DatabaseType.MySql)
            candidates.WithColumn("selectedcallid").AsCustom(sql.HexType(32)).Nullable()
                .Computed($"CASE WHEN {sql.Quote("isselected")} = 1 THEN RTRIM({sql.Quote("callid")}) ELSE NULL END", true);
        candidates.WithCheckConstraint(sql, "ck_callparties_id", $"{sql.Hex(sql.Quote("id"), 32)} AND {sql.Quote("id")} <> '00000000000000000000000000000000'");
        candidates.WithCheckConstraint(sql, "ck_callparties_callid", $"{sql.Hex(sql.Quote("callid"), 32)} AND {sql.Quote("callid")} <> '00000000000000000000000000000000'");
        candidates.WithCheckConstraint(sql, "ck_callparties_partykeyhash", sql.Hex(sql.Quote("partykeyhash"), 64));
        candidates.WithCheckConstraint(sql, "ck_callparties_kind", sql.IntegerCheck("kind", 1, 4));
        candidates.WithCheckConstraint(sql, "ck_callparties_externalid", sql.TextCheck("externalid", 256, nonBlank: true));
        candidates.WithCheckConstraint(sql, "ck_callparties_ownerexternalid", sql.TextCheck("ownerexternalid", 256, nullable: true, nonBlank: true));
        candidates.WithCheckConstraint(sql, "ck_callparties_owner", "([kind] IN (1,2) AND [ownerexternalid] IS NULL) OR ([kind] IN (3,4) AND [ownerexternalid] IS NOT NULL)");
        candidates.WithCheckConstraint(sql, "ck_callparties_displayname", sql.TextCheck("displayname", 512, nonBlank: true));
        candidates.WithCheckConstraint(sql, "ck_callparties_number", sql.TextCheck("number", 2048, nullable: true));
        candidates.WithCheckConstraint(sql, "ck_callparties_isselected", sql.IntegerCheck("isselected", 0, 1));
        candidates.WithCheckConstraint(sql, "ck_callparties_position", sql.IntegerCheck("position", 0, int.MaxValue));
        candidates.WithForeignKeyConstraint(sql, "fk_callparties_call", ["callid"], "phonecalls", "id");
        Create.Index("ux_callparties_source").OnTable("callparties").InSchema(sql.SchemaName).OnColumn("callid").Ascending().OnColumn("partykeyhash").Ascending().WithOptions().Unique();
        Create.Index("ux_callparties_position").OnTable("callparties").InSchema(sql.SchemaName).OnColumn("callid").Ascending().OnColumn("position").Ascending().WithOptions().Unique();
        Create.Index("ix_callparties_party").OnTable("callparties").InSchema(sql.SchemaName).OnColumn("partykeyhash").Ascending().OnColumn("callid").Ascending();
        if (databaseType == DatabaseType.MySql)
            Create.Index("ux_callparties_selected").OnTable("callparties").InSchema(sql.SchemaName).OnColumn("selectedcallid").Ascending().WithOptions().Unique();
        else
            Execute.Sql($"CREATE UNIQUE INDEX {sql.Quote("ux_callparties_selected")} ON {sql.Table("callparties")} ({sql.Quote("callid")}) WHERE {sql.Quote("isselected")} = 1");

        var notes = Create.Table("callnotes").InSchema(sql.SchemaName)
            .WithColumn("id").AsCustom(sql.HexType(32)).NotNullable().PrimaryKey("pk_callnotes")
            .WithColumn("callid").AsCustom(sql.HexType(32)).NotNullable()
            .WithColumn("text").AsString(int.MaxValue).NotNullable()
            .WithColumn("writeversion").AsInt64().NotNullable()
            .WithColumn("createdatutc").AsInt64().NotNullable()
            .WithColumn("updatedatutc").AsInt64().NotNullable();
        notes.WithCheckConstraint(sql, "ck_callnotes_id", $"{sql.Hex(sql.Quote("id"), 32)} AND {sql.Quote("id")} <> '00000000000000000000000000000000'");
        notes.WithCheckConstraint(sql, "ck_callnotes_callid", $"{sql.Hex(sql.Quote("callid"), 32)} AND {sql.Quote("callid")} <> '00000000000000000000000000000000'");
        notes.WithCheckConstraint(sql, "ck_callnotes_text", sql.TextCheck("text", 32768, nonBlank: true));
        notes.WithCheckConstraint(sql, "ck_callnotes_writeversion", sql.IntegerCheck("writeversion", 1, long.MaxValue));
        notes.WithCheckConstraint(sql, "ck_callnotes_createdatutc", sql.IntegerCheck("createdatutc", 0, 3155378975999999999));
        notes.WithCheckConstraint(sql, "ck_callnotes_updatedatutc", sql.IntegerCheck("updatedatutc", 0, 3155378975999999999));
        notes.WithCheckConstraint(sql, "ck_callnotes_chronology", "[createdatutc] <= [updatedatutc]");
        notes.WithForeignKeyConstraint(sql, "fk_callnotes_call", ["callid"], "phonecalls", "id");
        Create.Index("ix_callnotes_call").OnTable("callnotes").InSchema(sql.SchemaName).OnColumn("callid").Ascending().OnColumn("createdatutc").Ascending().OnColumn("id").Ascending();

        var events = Create.Table("callevents").InSchema(sql.SchemaName)
            .WithColumn("id").AsCustom(sql.HexType(32)).NotNullable().PrimaryKey("pk_callevents")
            .WithColumn("callid").AsCustom(sql.HexType(32)).NotNullable()
            .WithColumn("sequence").AsInt64().NotNullable()
            .WithColumn("occurredatutc").AsInt64().NotNullable()
            .WithColumn("state").AsInt32().NotNullable()
            .WithColumn("code").AsString(64).NotNullable();
        events.WithCheckConstraint(sql, "ck_callevents_id", $"{sql.Hex(sql.Quote("id"), 32)} AND {sql.Quote("id")} <> '00000000000000000000000000000000'");
        events.WithCheckConstraint(sql, "ck_callevents_callid", $"{sql.Hex(sql.Quote("callid"), 32)} AND {sql.Quote("callid")} <> '00000000000000000000000000000000'");
        events.WithCheckConstraint(sql, "ck_callevents_sequence", sql.IntegerCheck("sequence", 1, long.MaxValue));
        events.WithCheckConstraint(sql, "ck_callevents_occurredatutc", sql.IntegerCheck("occurredatutc", 0, 3155378975999999999));
        events.WithCheckConstraint(sql, "ck_callevents_state", sql.IntegerCheck("state", 1, 7));
        events.WithCheckConstraint(sql, "ck_callevents_code", sql.TextCheck("code", 64, nonBlank: true));
        events.WithCheckConstraint(sql, "ck_callevents_code_ascii", sql.AllowedAscii(sql.Quote("code"), "A-Za-z0-9_.-"));
        events.WithForeignKeyConstraint(sql, "fk_callevents_call", ["callid"], "phonecalls", "id");
        Create.Index("ux_callevents_sequence").OnTable("callevents").InSchema(sql.SchemaName).OnColumn("callid").Ascending().OnColumn("sequence").Ascending().WithOptions().Unique();
        Create.Index("ix_callevents_time").OnTable("callevents").InSchema(sql.SchemaName).OnColumn("callid").Ascending().OnColumn("occurredatutc").Ascending().OnColumn("id").Ascending();
    }
}

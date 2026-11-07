using FluentMigrator;
using WawiCallPro.Database.Schema;

namespace WawiCallPro.Database.Migrations;

/// <summary>Adds call snapshots, historical candidates, events and independent notes.</summary>
[Migration(3, "Call history candidates events and notes")]
public sealed class Migration003CallHistory(DatabaseType databaseType) : ForwardOnlyMigration
{
    public override void Up()
    {
        foreach (var statement in Statements(databaseType)) Execute.Sql(statement);
    }

    internal static IEnumerable<string> Statements(DatabaseType type)
    {
        var sql = new SchemaSql(type);
        var calls = new SchemaTable(sql, "phonecalls")
            .Id(primaryKey: true)
            .Id("companyid", nullable: true)
            .Counter("writeversion")
            .Counter("revision", 0)
            .Id("workstationid")
            .Text("machinename", 256, nonBlank: true)
            .Text("windowsusername", 256, nonBlank: true)
            .Integer("direction", 1, 2)
            .Integer("state", 1, 7)
            .Integer("localpresentation", 0, 2)
            .Text("localoriginalvalue", 2048, nullable: true)
            .Text("locale164", 16, nullable: true)
            .Text("localextension", 32, nullable: true)
            .Check("local_available", $"[localpresentation] <> 1 OR ([localoriginalvalue] IS NOT NULL AND {sql.NonBlank(sql.Quote("localoriginalvalue"))})")
            .Check("local_withheld", "[localpresentation] <> 2 OR ([localoriginalvalue] IS NULL AND [locale164] IS NULL AND [localextension] IS NULL)")
            .Check("local_e164", $"[locale164] IS NULL OR ({sql.E164(sql.Quote("locale164"))})")
            .Integer("remotepresentation", 0, 2)
            .Text("remoteoriginalvalue", 2048, nullable: true)
            .Text("remotee164", 16, nullable: true)
            .Text("remoteextension", 32, nullable: true)
            .Check("remote_available", $"[remotepresentation] <> 1 OR ([remoteoriginalvalue] IS NOT NULL AND {sql.NonBlank(sql.Quote("remoteoriginalvalue"))})")
            .Check("remote_withheld", "[remotepresentation] <> 2 OR ([remoteoriginalvalue] IS NULL AND [remotee164] IS NULL AND [remoteextension] IS NULL)")
            .Check("remote_e164", $"[remotee164] IS NULL OR ({sql.E164(sql.Quote("remotee164"))})")
            .Utc("startedatutc")
            .Utc("statechangedatutc")
            .Utc("answeredatutc", nullable: true)
            .Utc("endedatutc", nullable: true)
            .Integer("endreason", 1, 10, nullable: true)
            .Check("direction_state", "[direction] <> 1 OR [state] <> 2")
            .Check("chronology", "[startedatutc] <= [statechangedatutc] AND ([answeredatutc] IS NULL OR ([answeredatutc] >= [startedatutc] AND [answeredatutc] <= [statechangedatutc]))")
            .Check("answer", "([state] IN (4,5) AND [answeredatutc] IS NOT NULL) OR ([state] IN (1,2,3) AND [answeredatutc] IS NULL) OR [state] IN (6,7)")
            .Check("end", "([state] IN (1,2,3,4,5) AND [endedatutc] IS NULL AND [endreason] IS NULL) OR ([state] IN (6,7) AND [endedatutc] IS NOT NULL AND [endedatutc] = [statechangedatutc] AND [endreason] IS NOT NULL)")
            .Check("reason", "[state] NOT IN (6,7) OR ([state] = 7 AND [endreason] IN (7,8,9)) OR ([state] = 6 AND (([endreason] IN (1,10) AND [answeredatutc] IS NOT NULL) OR ([endreason] = 2 AND [direction] = 1 AND [answeredatutc] IS NULL) OR ([endreason] = 5 AND [direction] = 2 AND [answeredatutc] IS NULL) OR ([endreason] IN (3,4) AND [answeredatutc] IS NULL) OR [endreason] = 6))")
            .ForeignKey("company", ["companyid"], "companies", "id")
            .Index("ix_phonecalls_start", "startedatutc DESC", "id ASC")
            .Index("ix_phonecalls_workstation", "workstationid", "startedatutc DESC", "id ASC")
            .Index("ix_phonecalls_machine", "machinename", "startedatutc DESC", "id ASC")
            .Index("ix_phonecalls_user", "windowsusername", "startedatutc DESC", "id ASC")
            .Index("ix_phonecalls_company", "companyid", "startedatutc DESC", "id ASC");

        var candidates = new SchemaTable(sql, "callparties")
            .Id(primaryKey: true)
            .Id("callid")
            .Hash("partykeyhash")
            .Integer("kind", 1, 4)
            .Text("externalid", 256, nonBlank: true)
            .Text("ownerexternalid", 256, nullable: true, nonBlank: true)
            .Check("owner", "([kind] IN (1,2) AND [ownerexternalid] IS NULL) OR ([kind] IN (3,4) AND [ownerexternalid] IS NOT NULL)")
            .Text("displayname", 512, nonBlank: true)
            .Text("number", 2048, nullable: true)
            .Boolean("isselected")
            .Integer("position", 0, int.MaxValue)
            .ForeignKey("call", ["callid"], "phonecalls", "id")
            .Unique("ux_callparties_source", "callid", "partykeyhash")
            .Unique("ux_callparties_position", "callid", "position")
            .Index("ix_callparties_party", "partykeyhash", "callid");
        var candidateStatements = candidates.Statements();
        if (type == DatabaseType.MySql)
        {
            candidates.StoredGeneratedColumn("selectedcallid", sql.HexType(32),
                // RTRIM makes CHAR padding independent of sql_mode for MariaDB's stored expression.
                $"CASE WHEN {sql.Quote("isselected")} = 1 THEN RTRIM({sql.Quote("callid")}) ELSE NULL END")
                .Unique("ux_callparties_selected", "selectedcallid");
        }
        else
        {
            candidateStatements = candidateStatements.Append(
                $"CREATE UNIQUE INDEX {sql.Quote("ux_callparties_selected")} ON {sql.Table("callparties")} ({sql.Quote("callid")}) WHERE {sql.Quote("isselected")} = 1");
        }

        var notes = new SchemaTable(sql, "callnotes")
            .Id(primaryKey: true)
            .Id("callid")
            .Text("text", 32768, nonBlank: true)
            .Counter("writeversion")
            .Utc("createdatutc")
            .Utc("updatedatutc")
            .Check("chronology", "[createdatutc] <= [updatedatutc]")
            .ForeignKey("call", ["callid"], "phonecalls", "id")
            .Index("ix_callnotes_call", "callid", "createdatutc", "id");

        var events = new SchemaTable(sql, "callevents")
            .Id(primaryKey: true)
            .Id("callid")
            .Counter("sequence")
            .Utc("occurredatutc")
            .Integer("state", 1, 7)
            .Text("code", 64, nonBlank: true)
            .Check("code_ascii", sql.AllowedAscii(sql.Quote("code"), "A-Za-z0-9_.-"))
            .ForeignKey("call", ["callid"], "phonecalls", "id")
            .Unique("ux_callevents_sequence", "callid", "sequence")
            .Index("ix_callevents_time", "callid", "occurredatutc", "id");

        return calls.Statements().Concat(candidateStatements).Concat(notes.Statements()).Concat(events.Statements());
    }
}

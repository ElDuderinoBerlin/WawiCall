namespace WawiCallPro.Database.Schema;

/// <summary>Frozen definitions of schema versions 1–5. Changes after shipment require a new migration version.</summary>
internal static class InitialSchema
{
    internal static IEnumerable<string> Statements(DatabaseType type, long version)
    {
        var sql = new SchemaSql(type);
        var tables = version switch
        {
            1 => Companies(sql),
            2 => PartyCache(sql),
            3 => Calls(sql),
            4 => [Recordings(sql)],
            5 => [NumberParseLogs(sql)],
            _ => throw new ArgumentOutOfRangeException(nameof(version))
        };
        return tables.SelectMany(table => table.Statements());
    }

    private static SchemaTable[] Companies(SchemaSql sql) =>
    [
        new SchemaTable(sql, "wawicallpro_metadata")
            .Text("applicationid", 64, nonBlank: true).Integer("formatversion", 1, int.MaxValue).Utc("createdatutc")
            .PrimaryKey("applicationid")
            .Check("identity", sql.Exact(sql.Quote("applicationid"), "WawiCallPro.Communications")),
        new SchemaTable(sql, "companies")
            .Id(primaryKey: true).Hash("sourcehash").Integer("productline", 1, 2)
            .Text("installationid", 256, nonBlank: true).Text("externalid", 256, nonBlank: true).Text("displayname", 512, nonBlank: true)
            .Counter("writeversion").Counter("cacheversion", 0).Utc("cacherefreshedatutc", nullable: true)
            .Utc("createdatutc").Utc("updatedatutc")
            .Check("chronology", "[createdatutc] <= [updatedatutc]")
            .Check("cache", "([cacheversion] = 0 AND [cacherefreshedatutc] IS NULL) OR ([cacheversion] > 0 AND [cacherefreshedatutc] IS NOT NULL)")
            .Unique("ux_companies_sourcehash", "sourcehash").Index("ix_companies_productline", "productline", "sourcehash")
    ];

    private static SchemaTable[] PartyCache(SchemaSql sql) =>
    [
        PartyIdentity(new SchemaTable(sql, "parties").Id(primaryKey: true).Id("companyid").Hash("sourcehash"))
            .Text("displayname", 512, nonBlank: true).Text("number", 128, nullable: true).Text("email", 320, nullable: true)
            .Boolean("isinactive").Utc("sourceupdatedatutc", nullable: true).Utc("refreshedatutc")
            .Text("searchname", 1024).Text("searchnumber", 256, nullable: true)
            .ForeignKey("company", ["companyid"], "companies", "id")
            .Unique("ux_parties_sourcehash", "sourcehash").Index("ix_parties_company", "companyid", "sourcehash"),
        new SchemaTable(sql, "partyaddresses")
            .Id(primaryKey: true).Id("partyid").Hash("sourcehash").Text("externalid", 256, nonBlank: true)
            .Integer("kind", 1, 4).Integer("position", 0, int.MaxValue)
            .Text("companyname", 512, nullable: true).Text("firstname", 256, nullable: true).Text("lastname", 256, nullable: true)
            .Text("street", 512, nullable: true).Text("addressline2", 512, nullable: true)
            .Text("housenumber", 64, nullable: true).Text("postalcode", 32, nullable: true)
            .Text("city", 256, nullable: true).Text("countrycode", 2, nullable: true).Text("email", 320, nullable: true)
            .ForeignKey("party", ["partyid"], "parties", "id")
            .Unique("ux_partyaddresses_sourcehash", "sourcehash").Unique("ux_partyaddresses_position", "partyid", "position")
            .Unique("ux_partyaddresses_owner", "id", "partyid"),
        SourceField(new SchemaTable(sql, "partyphones").Id(primaryKey: true).Id("partyid").Id("addressid", nullable: true)
            .Integer("position", 0, int.MaxValue).Integer("kind", 1, 3).Text("originalvalue", 2048, nonBlank: true)
            .Text("e164", 16, nullable: true).Text("extension", 32, nullable: true))
            .Check("e164_format", "[e164] IS NULL OR (" + sql.E164(sql.Quote("e164")) + ")")
            .ForeignKey("party", ["partyid"], "parties", "id")
            .ForeignKey("address", ["addressid", "partyid"], "partyaddresses", "id", "partyid")
            .Unique("ux_partyphones_position", "partyid", "position")
            .Index("ix_partyphones_number", "e164", "partyid").Index("ix_partyphones_address", "addressid", "partyid")
    ];

    private static SchemaTable[] Calls(SchemaSql sql) =>
    [
        CallAddress(sql, CallAddress(sql, new SchemaTable(sql, "phonecalls")
            .Id(primaryKey: true).Id("companyid", nullable: true).Counter("writeversion").Counter("revision", 0)
            .Id("workstationid").Text("machinename", 256, nonBlank: true).Text("windowsusername", 256, nonBlank: true)
            .Integer("direction", 1, 2).Integer("state", 1, 7), "local"), "remote")
            .Utc("startedatutc").Utc("statechangedatutc").Utc("answeredatutc", nullable: true).Utc("endedatutc", nullable: true)
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
            .Index("ix_phonecalls_company", "companyid", "startedatutc DESC", "id ASC"),
        PartyIdentity(new SchemaTable(sql, "callparties").Id(primaryKey: true).Id("callid").Hash("partykeyhash"))
            .Text("displayname", 512, nonBlank: true).Text("number", 2048, nullable: true)
            .Boolean("isselected").Integer("position", 0, int.MaxValue)
            .ForeignKey("call", ["callid"], "phonecalls", "id")
            .Unique("ux_callparties_source", "callid", "partykeyhash").Unique("ux_callparties_position", "callid", "position")
            .Index("ix_callparties_party", "partykeyhash", "callid").SelectedCallIndex(),
        new SchemaTable(sql, "callnotes").Id(primaryKey: true).Id("callid").Text("text", 32768, nonBlank: true)
            .Counter("writeversion").Utc("createdatutc").Utc("updatedatutc")
            .Check("chronology", "[createdatutc] <= [updatedatutc]")
            .ForeignKey("call", ["callid"], "phonecalls", "id").Index("ix_callnotes_call", "callid", "createdatutc", "id"),
        EventCode(sql, new SchemaTable(sql, "callevents").Id(primaryKey: true).Id("callid")
            .Counter("sequence").Utc("occurredatutc").Integer("state", 1, 7))
            .ForeignKey("call", ["callid"], "phonecalls", "id")
            .Unique("ux_callevents_sequence", "callid", "sequence").Index("ix_callevents_time", "callid", "occurredatutc", "id")
    ];

    private static SchemaTable Recordings(SchemaSql sql) => new SchemaTable(sql, "recordings")
        .Id(primaryKey: true).Id("callid").Text("relativepath", 1024, nonBlank: true).Text("format", 32, nonBlank: true)
        .Counter("durationticks", 0, nullable: true).Integer("availability", 0, 3).Counter("writeversion")
        .Utc("createdatutc").Utc("updatedatutc").Check("chronology", "[createdatutc] <= [updatedatutc]")
        // Full Windows device/alias/reparse validation stays in the shared write contract and file layer.
        .Check("path", "[relativepath] NOT LIKE '/%' AND [relativepath] NOT LIKE '%/' AND [relativepath] NOT LIKE '%:%' AND [relativepath] NOT LIKE '%//%' AND [relativepath] <> '.' AND [relativepath] <> '..' AND [relativepath] NOT LIKE './%' AND [relativepath] NOT LIKE '../%' AND [relativepath] NOT LIKE '%/./%' AND [relativepath] NOT LIKE '%/../%' AND [relativepath] NOT LIKE '%/.' AND [relativepath] NOT LIKE '%/..'")
        .Check("separator", $"{sql.Length(sql.Quote("relativepath"))} = {sql.Length($"REPLACE({sql.Quote("relativepath")}, {sql.Character(92)}, '')")}")
        .ForeignKey("call", ["callid"], "phonecalls", "id").Index("ix_recordings_call", "callid", "id");

    private static SchemaTable NumberParseLogs(SchemaSql sql) => EventCode(sql, SourceField(PartyIdentity(
            new SchemaTable(sql, "numberparselogs").Id(primaryKey: true).Id("companyid").Hash("partykeyhash")))
        .Integer("phonekind", 1, 3).Text("originalvalue", 2048).Utc("occurredatutc"))
        .ForeignKey("company", ["companyid"], "companies", "id")
        .Index("ix_numberparselogs_company", "companyid", "occurredatutc DESC", "id ASC")
        .Index("ix_numberparselogs_party", "partykeyhash", "occurredatutc DESC", "id ASC");

    private static SchemaTable PartyIdentity(SchemaTable table) => table.Integer("kind", 1, 4)
        .Text("externalid", 256, nonBlank: true).Text("ownerexternalid", 256, nullable: true, nonBlank: true)
        .Check("owner", "([kind] IN (1,2) AND [ownerexternalid] IS NULL) OR ([kind] IN (3,4) AND [ownerexternalid] IS NOT NULL)");

    private static SchemaTable SourceField(SchemaTable table) => table.Text("sourcerecordtype", 64, nonBlank: true)
        .Text("sourcerecordid", 256, nonBlank: true).Text("sourcefieldname", 128, nonBlank: true);

    private static SchemaTable EventCode(SchemaSql sql, SchemaTable table) => table.Text("code", 64, nonBlank: true)
        .Check("code_ascii", sql.AllowedAscii(sql.Quote("code"), "A-Za-z0-9_.-"));

    private static SchemaTable CallAddress(SchemaSql sql, SchemaTable table, string prefix)
    {
        var presentation = $"[{prefix}presentation]";
        var original = $"[{prefix}originalvalue]";
        var e164 = $"[{prefix}e164]";
        var extension = $"[{prefix}extension]";
        return table.Integer(prefix + "presentation", 0, 2).Text(prefix + "originalvalue", 2048, nullable: true)
            .Text(prefix + "e164", 16, nullable: true).Text(prefix + "extension", 32, nullable: true)
            .Check(prefix + "_available", $"{presentation} <> 1 OR ({original} IS NOT NULL AND {sql.NonBlank(sql.Quote(prefix + "originalvalue"))})")
            .Check(prefix + "_withheld", $"{presentation} <> 2 OR ({original} IS NULL AND {e164} IS NULL AND {extension} IS NULL)")
            .Check(prefix + "_e164", $"{e164} IS NULL OR ({sql.E164(sql.Quote(prefix + "e164"))})");
    }
}

namespace WawiCallPro.Database.Schema;

/// <summary>Version 6 only. The original schema definitions remain frozen.</summary>
internal static class MultiCompanySchema
{
    internal static IEnumerable<string> Statements(DatabaseType type)
    {
        var sql = new SchemaSql(type);
        yield return $"CREATE UNIQUE INDEX {sql.Quote("ux_phonecalls_id_company")} ON {sql.Table("phonecalls")} ({sql.Quote("id")},{sql.Quote("companyid")})";
        var candidates = new SchemaTable(sql, "callparties_v6").Id(primaryKey: true).Id("callid").Id("companyid")
            .Id("assignedcompanyid", nullable: true).Hash("partykeyhash").Integer("kind", 1, 4)
            .Text("externalid", 256, nonBlank: true).Text("ownerexternalid", 256, nullable: true, nonBlank: true)
            .Check("owner", "([kind] IN (1,2) AND [ownerexternalid] IS NULL) OR ([kind] IN (3,4) AND [ownerexternalid] IS NOT NULL)")
            .Text("displayname", 512, nonBlank: true).Text("number", 2048, nullable: true)
            .Text("companydisplayname", 512, nullable: true, nonBlank: true)
            .Boolean("isselected").Integer("position", 0, int.MaxValue)
            .Integer("matchkind", 0, 4).Integer("startwidth", 1, 15, nullable: true)
            .Text("apparatusnumber", 32, nullable: true).Text("evidencejson", 8192, nullable: true, nonBlank: true)
            .Check("selection", "([isselected]=0 AND [assignedcompanyid] IS NULL) OR ([isselected]=1 AND [assignedcompanyid]=[companyid] AND [assignedcompanyid] IS NOT NULL)")
            .Check("evidence", "([matchkind]=0 AND [evidencejson] IS NULL AND [startwidth] IS NULL AND [apparatusnumber] IS NULL) OR ([matchkind] BETWEEN 1 AND 4 AND [evidencejson] IS NOT NULL)")
            .Check("width", "([matchkind]=2 AND [startwidth] IS NOT NULL) OR ([matchkind]<>2 AND [startwidth] IS NULL)")
            .Check("digits", $"[apparatusnumber] IS NULL OR {sql.Length(sql.Quote("apparatusnumber"))}=0 OR {sql.AllowedAscii(sql.Quote("apparatusnumber"), "0-9")}")
            .ForeignKey("call", ["callid"], "phonecalls", "id")
            .ForeignKey("company", ["companyid"], "companies", "id")
            .ForeignKey("selectedcompany", ["callid", "assignedcompanyid"], "phonecalls", "id", "companyid")
            .Unique("ux_callparties_v6_source", "callid", "partykeyhash")
            .Unique("ux_callparties_v6_position", "callid", "position")
            .Index("ix_callparties_v6_party", "partykeyhash", "callid")
            .Index("ix_callparties_v6_company", "companyid", "callid")
            .Index("ix_callparties_v6_selection", "callid", "assignedcompanyid");
        foreach (var statement in candidates.Statements()) yield return statement;
        string[] original = ["id", "callid", "partykeyhash", "kind", "externalid", "ownerexternalid", "displayname", "number", "isselected", "position"];
        var columns = string.Join(',', original.Select(sql.Quote));
        var values = string.Join(',', original.Select(column => "p." + sql.Quote(column)));
        yield return $"INSERT INTO {sql.Table("callparties_v6")} ({columns},{sql.Quote("companyid")},{sql.Quote("assignedcompanyid")},{sql.Quote("matchkind")}) " +
            $"SELECT {values},c.{sql.Quote("companyid")},CASE WHEN p.{sql.Quote("isselected")}=1 THEN c.{sql.Quote("companyid")} ELSE NULL END,0 " +
            $"FROM {sql.Table("callparties")} p JOIN {sql.Table("phonecalls")} c ON c.{sql.Quote("id")}=p.{sql.Quote("callid")}";
        yield return $"DROP TABLE {sql.Table("callparties")}";
        yield return type switch
        {
            DatabaseType.SqlServer => "EXEC sys.sp_rename 'dbo.callparties_v6','callparties'",
            DatabaseType.MySql => $"RENAME TABLE {sql.Table("callparties_v6")} TO {sql.Table("callparties")}",
            _ => $"ALTER TABLE {sql.Table("callparties_v6")} RENAME TO {sql.Quote("callparties")}"
        };
        if (type == DatabaseType.MySql)
        {
            yield return $"ALTER TABLE {sql.Table("callparties")} ADD {sql.Quote("selectedcallid")} {sql.HexType(32)} GENERATED ALWAYS AS (CASE WHEN {sql.Quote("isselected")}=1 THEN {sql.Quote("callid")} ELSE NULL END) STORED";
            yield return $"CREATE UNIQUE INDEX {sql.Quote("ux_callparties_v6_selected")} ON {sql.Table("callparties")} ({sql.Quote("selectedcallid")})";
        }
        else yield return $"CREATE UNIQUE INDEX {sql.Quote("ux_callparties_v6_selected")} ON {sql.Table("callparties")} ({sql.Quote("callid")}) WHERE {sql.Quote("isselected")}=1";

        var lookup = new SchemaTable(sql, "calllookups").Id("callid", primaryKey: true)
            .Text("detailsjson", 1048576, nonBlank: true).Integer("formatversion", 1, 1)
            .Text("parserversion", 256, nonBlank: true).Boolean("iscomplete").Utc("searchedatutc")
            .Text("selectionreason", 64, nullable: true, nonBlank: true)
            .Check("reason", $"[selectionreason] IS NULL OR {sql.AllowedAscii(sql.Quote("selectionreason"), "A-Za-z0-9_.-")}")
            .ForeignKey("call", ["callid"], "phonecalls", "id");
        foreach (var statement in lookup.Statements()) yield return statement;
        var settings = new SchemaTable(sql, "phonematchsettings").Id("companyid", primaryKey: true)
            .Boolean("enabled").Integer("startwidth", 1, 15).Counter("writeversion").Counter("rulerevision").Utc("changedatutc")
            .ForeignKey("company", ["companyid"], "companies", "id");
        foreach (var statement in settings.Statements()) yield return statement;
        var rules = new SchemaTable(sql, "phonenumberblockrules").Id(primaryKey: true).Id("companyid").Hash("partykeyhash")
            .Integer("partykind", 1, 2).Text("externalid", 256, nonBlank: true).Integer("kind", 1, 2).Boolean("enabled")
            .Integer("countrycallingcode", 1, 999).Text("destinationcode", 10, nonBlank: true).Text("stem", 15, nonBlank: true)
            .Integer("minsuffixdigits", 0, 15).Integer("maxsuffixdigits", 0, 15).Text("evidence", 1024, nonBlank: true)
            .Counter("writeversion").Utc("changedatutc")
            .Check("suffix", "[minsuffixdigits] <= [maxsuffixdigits]")
            .Check("digits", $"{sql.AllowedAscii(sql.Quote("destinationcode"), "0-9")} AND {sql.AllowedAscii(sql.Quote("stem"), "0-9")}")
            .Check("e164capacity", $"(CASE WHEN [countrycallingcode]<10 THEN 1 WHEN [countrycallingcode]<100 THEN 2 ELSE 3 END)+{sql.Length(sql.Quote("destinationcode"))}+{sql.Length(sql.Quote("stem"))}+[maxsuffixdigits] <= 15")
            .ForeignKey("company", ["companyid"], "companies", "id")
            .Index("ix_phoneblock_company", "companyid", "id")
            .Index("ix_phoneblock_prefix", "companyid", "countrycallingcode", "destinationcode", "stem")
            .Index("ix_phoneblock_party", "partykeyhash", "id");
        foreach (var statement in rules.Statements()) yield return statement;
    }
}

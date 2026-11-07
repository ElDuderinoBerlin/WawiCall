using FluentMigrator;
using WawiCallPro.Database.Schema;

namespace WawiCallPro.Database.Migrations;

[Migration(6, "Independent candidate companies lookup evidence and phone rules")]
public sealed class Migration006MultiCompanyLookup(DatabaseType databaseType) : ForwardOnlyMigration
{
    public override void Up()
    {
        // Reject impossible legacy candidate identities before MySQL's first nontransactional DDL statement.
        Execute.WithConnection((connection, transaction) =>
        {
            using var command = connection.CreateCommand(); command.Transaction = transaction;
            command.CommandText = $"SELECT COUNT(*) FROM {DatabaseSql.Table(databaseType, "callparties")} p " +
                $"LEFT JOIN {DatabaseSql.Table(databaseType, "phonecalls")} c ON c.id=p.callid " +
                $"LEFT JOIN {DatabaseSql.Table(databaseType, "companies")} co ON co.id=c.companyid WHERE c.companyid IS NULL OR co.id IS NULL";
            if (Convert.ToInt64(command.ExecuteScalar()) != 0) throw new DatabaseOperationException(DatabaseErrorCode.InvalidSchema);
        });
        var sql = new SchemaSql(databaseType);
        Create.Index("ux_phonecalls_id_company").OnTable("phonecalls").InSchema(sql.SchemaName).OnColumn("id").Ascending().OnColumn("companyid").Ascending().WithOptions().Unique();

        var candidates = Create.Table("callparties_v6").InSchema(sql.SchemaName)
            .WithColumn("id").AsCustom(sql.HexType(32)).NotNullable().PrimaryKey("pk_callparties_v6")
            .WithColumn("callid").AsCustom(sql.HexType(32)).NotNullable()
            .WithColumn("companyid").AsCustom(sql.HexType(32)).NotNullable()
            .WithColumn("assignedcompanyid").AsCustom(sql.HexType(32)).Nullable()
            .WithColumn("partykeyhash").AsCustom(sql.HexType(64)).NotNullable()
            .WithColumn("kind").AsInt32().NotNullable()
            .WithColumn("externalid").AsString(256).NotNullable()
            .WithColumn("ownerexternalid").AsString(256).Nullable()
            .WithColumn("displayname").AsString(512).NotNullable()
            .WithColumn("number").AsString(2048).Nullable()
            .WithColumn("companydisplayname").AsString(512).Nullable()
            .WithColumn("isselected").AsInt32().NotNullable()
            .WithColumn("position").AsInt32().NotNullable()
            .WithColumn("matchkind").AsInt32().NotNullable()
            .WithColumn("startwidth").AsInt32().Nullable()
            .WithColumn("apparatusnumber").AsString(32).Nullable()
            .WithColumn("evidencejson").AsString(int.MaxValue).Nullable();
        candidates.WithCheckConstraint(sql, "ck_callparties_v6_id", $"{sql.Hex(sql.Quote("id"), 32)} AND {sql.Quote("id")} <> '00000000000000000000000000000000'");
        candidates.WithCheckConstraint(sql, "ck_callparties_v6_callid", $"{sql.Hex(sql.Quote("callid"), 32)} AND {sql.Quote("callid")} <> '00000000000000000000000000000000'");
        candidates.WithCheckConstraint(sql, "ck_callparties_v6_companyid", $"{sql.Hex(sql.Quote("companyid"), 32)} AND {sql.Quote("companyid")} <> '00000000000000000000000000000000'");
        candidates.WithCheckConstraint(sql, "ck_callparties_v6_assignedcompanyid", $"{sql.Quote("assignedcompanyid")} IS NULL OR (" + $"{sql.Hex(sql.Quote("assignedcompanyid"), 32)} AND {sql.Quote("assignedcompanyid")} <> '00000000000000000000000000000000'" + ")");
        candidates.WithCheckConstraint(sql, "ck_callparties_v6_partykeyhash", sql.Hex(sql.Quote("partykeyhash"), 64));
        candidates.WithCheckConstraint(sql, "ck_callparties_v6_kind", sql.IntegerCheck("kind", 1, 4));
        candidates.WithCheckConstraint(sql, "ck_callparties_v6_externalid", sql.TextCheck("externalid", 256, nonBlank: true));
        candidates.WithCheckConstraint(sql, "ck_callparties_v6_ownerexternalid", sql.TextCheck("ownerexternalid", 256, nullable: true, nonBlank: true));
        candidates.WithCheckConstraint(sql, "ck_callparties_v6_owner", "([kind] IN (1,2) AND [ownerexternalid] IS NULL) OR ([kind] IN (3,4) AND [ownerexternalid] IS NOT NULL)");
        candidates.WithCheckConstraint(sql, "ck_callparties_v6_displayname", sql.TextCheck("displayname", 512, nonBlank: true));
        candidates.WithCheckConstraint(sql, "ck_callparties_v6_number", sql.TextCheck("number", 2048, nullable: true));
        candidates.WithCheckConstraint(sql, "ck_callparties_v6_companydisplayname", sql.TextCheck("companydisplayname", 512, nullable: true, nonBlank: true));
        candidates.WithCheckConstraint(sql, "ck_callparties_v6_isselected", sql.IntegerCheck("isselected", 0, 1));
        candidates.WithCheckConstraint(sql, "ck_callparties_v6_position", sql.IntegerCheck("position", 0, int.MaxValue));
        candidates.WithCheckConstraint(sql, "ck_callparties_v6_matchkind", sql.IntegerCheck("matchkind", 0, 4));
        candidates.WithCheckConstraint(sql, "ck_callparties_v6_startwidth", sql.IntegerCheck("startwidth", 1, 15, nullable: true));
        candidates.WithCheckConstraint(sql, "ck_callparties_v6_apparatusnumber", sql.TextCheck("apparatusnumber", 32, nullable: true));
        candidates.WithCheckConstraint(sql, "ck_callparties_v6_evidencejson", sql.TextCheck("evidencejson", 8192, nullable: true, nonBlank: true));
        candidates.WithCheckConstraint(sql, "ck_callparties_v6_selection", "([isselected]=0 AND [assignedcompanyid] IS NULL) OR ([isselected]=1 AND [assignedcompanyid]=[companyid] AND [assignedcompanyid] IS NOT NULL)");
        candidates.WithCheckConstraint(sql, "ck_callparties_v6_evidence", "([matchkind]=0 AND [evidencejson] IS NULL AND [startwidth] IS NULL AND [apparatusnumber] IS NULL) OR ([matchkind] BETWEEN 1 AND 4 AND [evidencejson] IS NOT NULL)");
        candidates.WithCheckConstraint(sql, "ck_callparties_v6_width", "([matchkind]=2 AND [startwidth] IS NOT NULL) OR ([matchkind]<>2 AND [startwidth] IS NULL)");
        candidates.WithCheckConstraint(sql, "ck_callparties_v6_digits", $"[apparatusnumber] IS NULL OR {sql.Length(sql.Quote("apparatusnumber"))}=0 OR {sql.AllowedAscii(sql.Quote("apparatusnumber"), "0-9")}");
        candidates.WithForeignKeyConstraint(sql, "fk_callparties_v6_call", ["callid"], "phonecalls", "id");
        candidates.WithForeignKeyConstraint(sql, "fk_callparties_v6_company", ["companyid"], "companies", "id");
        candidates.WithForeignKeyConstraint(sql, "fk_callparties_v6_selectedcompany", ["callid", "assignedcompanyid"], "phonecalls", "id", "companyid");
        Create.Index("ux_callparties_v6_source").OnTable("callparties_v6").InSchema(sql.SchemaName).OnColumn("callid").Ascending().OnColumn("partykeyhash").Ascending().WithOptions().Unique();
        Create.Index("ux_callparties_v6_position").OnTable("callparties_v6").InSchema(sql.SchemaName).OnColumn("callid").Ascending().OnColumn("position").Ascending().WithOptions().Unique();
        Create.Index("ix_callparties_v6_party").OnTable("callparties_v6").InSchema(sql.SchemaName).OnColumn("partykeyhash").Ascending().OnColumn("callid").Ascending();
        Create.Index("ix_callparties_v6_company").OnTable("callparties_v6").InSchema(sql.SchemaName).OnColumn("companyid").Ascending().OnColumn("callid").Ascending();
        Create.Index("ix_callparties_v6_selection").OnTable("callparties_v6").InSchema(sql.SchemaName).OnColumn("callid").Ascending().OnColumn("assignedcompanyid").Ascending();
        string[] original = ["id", "callid", "partykeyhash", "kind", "externalid", "ownerexternalid", "displayname", "number", "isselected", "position"];
        var columns = string.Join(',', original.Select(sql.Quote));
        var values = string.Join(',', original.Select(column => "p." + sql.Quote(column)));
        Execute.Sql($"INSERT INTO {sql.Table("callparties_v6")} ({columns},{sql.Quote("companyid")},{sql.Quote("assignedcompanyid")},{sql.Quote("matchkind")}) " +
            $"SELECT {values},c.{sql.Quote("companyid")},CASE WHEN p.{sql.Quote("isselected")}=1 THEN c.{sql.Quote("companyid")} ELSE NULL END,0 " +
            $"FROM {sql.Table("callparties")} p JOIN {sql.Table("phonecalls")} c ON c.{sql.Quote("id")}=p.{sql.Quote("callid")}");
        Delete.Table("callparties").InSchema(sql.SchemaName);
        Rename.Table("callparties_v6").InSchema(sql.SchemaName).To("callparties");
        if (databaseType == DatabaseType.MySql)
        {
            Alter.Table("callparties").InSchema(sql.SchemaName).AddColumn("selectedcallid").AsCustom(sql.HexType(32)).Nullable()
                .Computed($"CASE WHEN {sql.Quote("isselected")}=1 THEN RTRIM({sql.Quote("callid")}) ELSE NULL END", true);
            Create.Index("ux_callparties_v6_selected").OnTable("callparties").InSchema(sql.SchemaName).OnColumn("selectedcallid").Ascending().WithOptions().Unique();
        }
        else Execute.Sql($"CREATE UNIQUE INDEX {sql.Quote("ux_callparties_v6_selected")} ON {sql.Table("callparties")} ({sql.Quote("callid")}) WHERE {sql.Quote("isselected")}=1");

        var lookup = Create.Table("calllookups").InSchema(sql.SchemaName)
            .WithColumn("callid").AsCustom(sql.HexType(32)).NotNullable().PrimaryKey("pk_calllookups")
            .WithColumn("detailsjson").AsString(int.MaxValue).NotNullable()
            .WithColumn("formatversion").AsInt32().NotNullable()
            .WithColumn("parserversion").AsString(256).NotNullable()
            .WithColumn("iscomplete").AsInt32().NotNullable()
            .WithColumn("searchedatutc").AsInt64().NotNullable()
            .WithColumn("selectionreason").AsString(64).Nullable();
        lookup.WithCheckConstraint(sql, "ck_calllookups_callid", $"{sql.Hex(sql.Quote("callid"), 32)} AND {sql.Quote("callid")} <> '00000000000000000000000000000000'");
        lookup.WithCheckConstraint(sql, "ck_calllookups_detailsjson", sql.TextCheck("detailsjson", 1048576, nonBlank: true));
        lookup.WithCheckConstraint(sql, "ck_calllookups_formatversion", sql.IntegerCheck("formatversion", 1, 1));
        lookup.WithCheckConstraint(sql, "ck_calllookups_parserversion", sql.TextCheck("parserversion", 256, nonBlank: true));
        lookup.WithCheckConstraint(sql, "ck_calllookups_iscomplete", sql.IntegerCheck("iscomplete", 0, 1));
        lookup.WithCheckConstraint(sql, "ck_calllookups_searchedatutc", sql.IntegerCheck("searchedatutc", 0, 3155378975999999999));
        lookup.WithCheckConstraint(sql, "ck_calllookups_selectionreason", sql.TextCheck("selectionreason", 64, nullable: true, nonBlank: true));
        lookup.WithCheckConstraint(sql, "ck_calllookups_reason", $"[selectionreason] IS NULL OR {sql.AllowedAscii(sql.Quote("selectionreason"), "A-Za-z0-9_.-")}");
        lookup.WithForeignKeyConstraint(sql, "fk_calllookups_call", ["callid"], "phonecalls", "id");

        var settings = Create.Table("phonematchsettings").InSchema(sql.SchemaName)
            .WithColumn("companyid").AsCustom(sql.HexType(32)).NotNullable().PrimaryKey("pk_phonematchsettings")
            .WithColumn("enabled").AsInt32().NotNullable()
            .WithColumn("startwidth").AsInt32().NotNullable()
            .WithColumn("writeversion").AsInt64().NotNullable()
            .WithColumn("rulerevision").AsInt64().NotNullable()
            .WithColumn("changedatutc").AsInt64().NotNullable();
        settings.WithCheckConstraint(sql, "ck_phonematchsettings_companyid", $"{sql.Hex(sql.Quote("companyid"), 32)} AND {sql.Quote("companyid")} <> '00000000000000000000000000000000'");
        settings.WithCheckConstraint(sql, "ck_phonematchsettings_enabled", sql.IntegerCheck("enabled", 0, 1));
        settings.WithCheckConstraint(sql, "ck_phonematchsettings_startwidth", sql.IntegerCheck("startwidth", 1, 15));
        settings.WithCheckConstraint(sql, "ck_phonematchsettings_writeversion", sql.IntegerCheck("writeversion", 1, long.MaxValue));
        settings.WithCheckConstraint(sql, "ck_phonematchsettings_rulerevision", sql.IntegerCheck("rulerevision", 1, long.MaxValue));
        settings.WithCheckConstraint(sql, "ck_phonematchsettings_changedatutc", sql.IntegerCheck("changedatutc", 0, 3155378975999999999));
        settings.WithForeignKeyConstraint(sql, "fk_phonematchsettings_company", ["companyid"], "companies", "id");

        var rules = Create.Table("phonenumberblockrules").InSchema(sql.SchemaName)
            .WithColumn("id").AsCustom(sql.HexType(32)).NotNullable().PrimaryKey("pk_phonenumberblockrules")
            .WithColumn("companyid").AsCustom(sql.HexType(32)).NotNullable()
            .WithColumn("partykeyhash").AsCustom(sql.HexType(64)).NotNullable()
            .WithColumn("partykind").AsInt32().NotNullable()
            .WithColumn("externalid").AsString(256).NotNullable()
            .WithColumn("kind").AsInt32().NotNullable()
            .WithColumn("enabled").AsInt32().NotNullable()
            .WithColumn("countrycallingcode").AsInt32().NotNullable()
            .WithColumn("destinationcode").AsString(10).NotNullable()
            .WithColumn("stem").AsString(15).NotNullable()
            .WithColumn("minsuffixdigits").AsInt32().NotNullable()
            .WithColumn("maxsuffixdigits").AsInt32().NotNullable()
            .WithColumn("evidence").AsString(1024).NotNullable()
            .WithColumn("writeversion").AsInt64().NotNullable()
            .WithColumn("changedatutc").AsInt64().NotNullable();
        rules.WithCheckConstraint(sql, "ck_phonenumberblockrules_id", $"{sql.Hex(sql.Quote("id"), 32)} AND {sql.Quote("id")} <> '00000000000000000000000000000000'");
        rules.WithCheckConstraint(sql, "ck_phonenumberblockrules_companyid", $"{sql.Hex(sql.Quote("companyid"), 32)} AND {sql.Quote("companyid")} <> '00000000000000000000000000000000'");
        rules.WithCheckConstraint(sql, "ck_phonenumberblockrules_partykeyhash", sql.Hex(sql.Quote("partykeyhash"), 64));
        rules.WithCheckConstraint(sql, "ck_phonenumberblockrules_partykind", sql.IntegerCheck("partykind", 1, 2));
        rules.WithCheckConstraint(sql, "ck_phonenumberblockrules_externalid", sql.TextCheck("externalid", 256, nonBlank: true));
        rules.WithCheckConstraint(sql, "ck_phonenumberblockrules_kind", sql.IntegerCheck("kind", 1, 2));
        rules.WithCheckConstraint(sql, "ck_phonenumberblockrules_enabled", sql.IntegerCheck("enabled", 0, 1));
        rules.WithCheckConstraint(sql, "ck_phonenumberblockrules_countrycallingcode", sql.IntegerCheck("countrycallingcode", 1, 999));
        rules.WithCheckConstraint(sql, "ck_phonenumberblockrules_destinationcode", sql.TextCheck("destinationcode", 10, nonBlank: true));
        rules.WithCheckConstraint(sql, "ck_phonenumberblockrules_stem", sql.TextCheck("stem", 15, nonBlank: true));
        rules.WithCheckConstraint(sql, "ck_phonenumberblockrules_minsuffixdigits", sql.IntegerCheck("minsuffixdigits", 0, 15));
        rules.WithCheckConstraint(sql, "ck_phonenumberblockrules_maxsuffixdigits", sql.IntegerCheck("maxsuffixdigits", 0, 15));
        rules.WithCheckConstraint(sql, "ck_phonenumberblockrules_evidence", sql.TextCheck("evidence", 1024, nonBlank: true));
        rules.WithCheckConstraint(sql, "ck_phonenumberblockrules_writeversion", sql.IntegerCheck("writeversion", 1, long.MaxValue));
        rules.WithCheckConstraint(sql, "ck_phonenumberblockrules_changedatutc", sql.IntegerCheck("changedatutc", 0, 3155378975999999999));
        rules.WithCheckConstraint(sql, "ck_phonenumberblockrules_suffix", "[minsuffixdigits] <= [maxsuffixdigits]");
        rules.WithCheckConstraint(sql, "ck_phonenumberblockrules_digits", $"{sql.AllowedAscii(sql.Quote("destinationcode"), "0-9")} AND {sql.AllowedAscii(sql.Quote("stem"), "0-9")}");
        rules.WithCheckConstraint(sql, "ck_phonenumberblockrules_e164capacity", $"(CASE WHEN [countrycallingcode]<10 THEN 1 WHEN [countrycallingcode]<100 THEN 2 ELSE 3 END)+{sql.Length(sql.Quote("destinationcode"))}+{sql.Length(sql.Quote("stem"))}+[maxsuffixdigits] <= 15");
        rules.WithForeignKeyConstraint(sql, "fk_phonenumberblockrules_company", ["companyid"], "companies", "id");
        Create.Index("ix_phoneblock_company").OnTable("phonenumberblockrules").InSchema(sql.SchemaName).OnColumn("companyid").Ascending().OnColumn("id").Ascending();
        Create.Index("ix_phoneblock_prefix").OnTable("phonenumberblockrules").InSchema(sql.SchemaName).OnColumn("companyid").Ascending().OnColumn("countrycallingcode").Ascending().OnColumn("destinationcode").Ascending().OnColumn("stem").Ascending();
        Create.Index("ix_phoneblock_party").OnTable("phonenumberblockrules").InSchema(sql.SchemaName).OnColumn("partykeyhash").Ascending().OnColumn("id").Ascending();
    }
}

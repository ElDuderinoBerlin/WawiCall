using FluentMigrator;
using WawiCallPro.Database.Schema;

namespace WawiCallPro.Database.Migrations;

/// <summary>Adds the replaceable party, address and phone cache with explicit ownership keys.</summary>
[Migration(2, "Party address and phone cache")]
public sealed class Migration002PartyCache(DatabaseType databaseType) : ForwardOnlyMigration
{
    public override void Up()
    {
        var sql = new SchemaSql(databaseType);

        var parties = Create.Table("parties").InSchema(sql.SchemaName)
            .WithColumn("id").AsCustom(sql.HexType(32)).NotNullable().PrimaryKey("pk_parties")
            .WithColumn("companyid").AsCustom(sql.HexType(32)).NotNullable()
            .WithColumn("sourcehash").AsCustom(sql.HexType(64)).NotNullable()
            .WithColumn("kind").AsInt32().NotNullable()
            .WithColumn("externalid").AsString(256).NotNullable()
            .WithColumn("ownerexternalid").AsString(256).Nullable()
            .WithColumn("displayname").AsString(512).NotNullable()
            .WithColumn("number").AsString(128).Nullable()
            .WithColumn("email").AsString(320).Nullable()
            .WithColumn("isinactive").AsInt32().NotNullable()
            .WithColumn("sourceupdatedatutc").AsInt64().Nullable()
            .WithColumn("refreshedatutc").AsInt64().NotNullable()
            .WithColumn("searchname").AsString(1024).NotNullable()
            .WithColumn("searchnumber").AsString(256).Nullable();
        parties.WithCheckConstraint(sql, "ck_parties_id", $"{sql.Hex(sql.Quote("id"), 32)} AND {sql.Quote("id")} <> '00000000000000000000000000000000'");
        parties.WithCheckConstraint(sql, "ck_parties_companyid", $"{sql.Hex(sql.Quote("companyid"), 32)} AND {sql.Quote("companyid")} <> '00000000000000000000000000000000'");
        parties.WithCheckConstraint(sql, "ck_parties_sourcehash", sql.Hex(sql.Quote("sourcehash"), 64));
        parties.WithCheckConstraint(sql, "ck_parties_kind", sql.IntegerCheck("kind", 1, 4));
        parties.WithCheckConstraint(sql, "ck_parties_externalid", sql.TextCheck("externalid", 256, nonBlank: true));
        parties.WithCheckConstraint(sql, "ck_parties_ownerexternalid", sql.TextCheck("ownerexternalid", 256, nullable: true, nonBlank: true));
        parties.WithCheckConstraint(sql, "ck_parties_owner", "([kind] IN (1,2) AND [ownerexternalid] IS NULL) OR ([kind] IN (3,4) AND [ownerexternalid] IS NOT NULL)");
        parties.WithCheckConstraint(sql, "ck_parties_displayname", sql.TextCheck("displayname", 512, nonBlank: true));
        parties.WithCheckConstraint(sql, "ck_parties_number", sql.TextCheck("number", 128, nullable: true));
        parties.WithCheckConstraint(sql, "ck_parties_email", sql.TextCheck("email", 320, nullable: true));
        parties.WithCheckConstraint(sql, "ck_parties_isinactive", sql.IntegerCheck("isinactive", 0, 1));
        parties.WithCheckConstraint(sql, "ck_parties_sourceupdatedatutc", sql.IntegerCheck("sourceupdatedatutc", 0, 3155378975999999999, nullable: true));
        parties.WithCheckConstraint(sql, "ck_parties_refreshedatutc", sql.IntegerCheck("refreshedatutc", 0, 3155378975999999999));
        parties.WithCheckConstraint(sql, "ck_parties_searchname", sql.TextCheck("searchname", 1024));
        parties.WithCheckConstraint(sql, "ck_parties_searchnumber", sql.TextCheck("searchnumber", 256, nullable: true));
        parties.WithForeignKeyConstraint(sql, "fk_parties_company", ["companyid"], "companies", "id");
        Create.Index("ux_parties_sourcehash").OnTable("parties").InSchema(sql.SchemaName).OnColumn("sourcehash").Ascending().WithOptions().Unique();
        Create.Index("ix_parties_company").OnTable("parties").InSchema(sql.SchemaName).OnColumn("companyid").Ascending().OnColumn("sourcehash").Ascending();

        var addresses = Create.Table("partyaddresses").InSchema(sql.SchemaName)
            .WithColumn("id").AsCustom(sql.HexType(32)).NotNullable().PrimaryKey("pk_partyaddresses")
            .WithColumn("partyid").AsCustom(sql.HexType(32)).NotNullable()
            .WithColumn("sourcehash").AsCustom(sql.HexType(64)).NotNullable()
            .WithColumn("externalid").AsString(256).NotNullable()
            .WithColumn("kind").AsInt32().NotNullable()
            .WithColumn("position").AsInt32().NotNullable()
            .WithColumn("companyname").AsString(512).Nullable()
            .WithColumn("firstname").AsString(256).Nullable()
            .WithColumn("lastname").AsString(256).Nullable()
            .WithColumn("street").AsString(512).Nullable()
            .WithColumn("addressline2").AsString(512).Nullable()
            .WithColumn("housenumber").AsString(64).Nullable()
            .WithColumn("postalcode").AsString(32).Nullable()
            .WithColumn("city").AsString(256).Nullable()
            .WithColumn("countrycode").AsString(2).Nullable()
            .WithColumn("email").AsString(320).Nullable();
        addresses.WithCheckConstraint(sql, "ck_partyaddresses_id", $"{sql.Hex(sql.Quote("id"), 32)} AND {sql.Quote("id")} <> '00000000000000000000000000000000'");
        addresses.WithCheckConstraint(sql, "ck_partyaddresses_partyid", $"{sql.Hex(sql.Quote("partyid"), 32)} AND {sql.Quote("partyid")} <> '00000000000000000000000000000000'");
        addresses.WithCheckConstraint(sql, "ck_partyaddresses_sourcehash", sql.Hex(sql.Quote("sourcehash"), 64));
        addresses.WithCheckConstraint(sql, "ck_partyaddresses_externalid", sql.TextCheck("externalid", 256, nonBlank: true));
        addresses.WithCheckConstraint(sql, "ck_partyaddresses_kind", sql.IntegerCheck("kind", 1, 4));
        addresses.WithCheckConstraint(sql, "ck_partyaddresses_position", sql.IntegerCheck("position", 0, int.MaxValue));
        addresses.WithCheckConstraint(sql, "ck_partyaddresses_companyname", sql.TextCheck("companyname", 512, nullable: true));
        addresses.WithCheckConstraint(sql, "ck_partyaddresses_firstname", sql.TextCheck("firstname", 256, nullable: true));
        addresses.WithCheckConstraint(sql, "ck_partyaddresses_lastname", sql.TextCheck("lastname", 256, nullable: true));
        addresses.WithCheckConstraint(sql, "ck_partyaddresses_street", sql.TextCheck("street", 512, nullable: true));
        addresses.WithCheckConstraint(sql, "ck_partyaddresses_addressline2", sql.TextCheck("addressline2", 512, nullable: true));
        addresses.WithCheckConstraint(sql, "ck_partyaddresses_housenumber", sql.TextCheck("housenumber", 64, nullable: true));
        addresses.WithCheckConstraint(sql, "ck_partyaddresses_postalcode", sql.TextCheck("postalcode", 32, nullable: true));
        addresses.WithCheckConstraint(sql, "ck_partyaddresses_city", sql.TextCheck("city", 256, nullable: true));
        addresses.WithCheckConstraint(sql, "ck_partyaddresses_countrycode", sql.TextCheck("countrycode", 2, nullable: true));
        addresses.WithCheckConstraint(sql, "ck_partyaddresses_email", sql.TextCheck("email", 320, nullable: true));
        addresses.WithForeignKeyConstraint(sql, "fk_partyaddresses_party", ["partyid"], "parties", "id");
        Create.Index("ux_partyaddresses_sourcehash").OnTable("partyaddresses").InSchema(sql.SchemaName).OnColumn("sourcehash").Ascending().WithOptions().Unique();
        Create.Index("ux_partyaddresses_position").OnTable("partyaddresses").InSchema(sql.SchemaName).OnColumn("partyid").Ascending().OnColumn("position").Ascending().WithOptions().Unique();
        Create.Index("ux_partyaddresses_owner").OnTable("partyaddresses").InSchema(sql.SchemaName).OnColumn("id").Ascending().OnColumn("partyid").Ascending().WithOptions().Unique();

        var phones = Create.Table("partyphones").InSchema(sql.SchemaName)
            .WithColumn("id").AsCustom(sql.HexType(32)).NotNullable().PrimaryKey("pk_partyphones")
            .WithColumn("partyid").AsCustom(sql.HexType(32)).NotNullable()
            .WithColumn("addressid").AsCustom(sql.HexType(32)).Nullable()
            .WithColumn("position").AsInt32().NotNullable()
            .WithColumn("kind").AsInt32().NotNullable()
            .WithColumn("originalvalue").AsString(2048).NotNullable()
            .WithColumn("e164").AsString(16).Nullable()
            .WithColumn("extension").AsString(32).Nullable()
            .WithColumn("sourcerecordtype").AsString(64).NotNullable()
            .WithColumn("sourcerecordid").AsString(256).NotNullable()
            .WithColumn("sourcefieldname").AsString(128).NotNullable();
        phones.WithCheckConstraint(sql, "ck_partyphones_id", $"{sql.Hex(sql.Quote("id"), 32)} AND {sql.Quote("id")} <> '00000000000000000000000000000000'");
        phones.WithCheckConstraint(sql, "ck_partyphones_partyid", $"{sql.Hex(sql.Quote("partyid"), 32)} AND {sql.Quote("partyid")} <> '00000000000000000000000000000000'");
        phones.WithCheckConstraint(sql, "ck_partyphones_addressid", $"{sql.Quote("addressid")} IS NULL OR (" + $"{sql.Hex(sql.Quote("addressid"), 32)} AND {sql.Quote("addressid")} <> '00000000000000000000000000000000'" + ")");
        phones.WithCheckConstraint(sql, "ck_partyphones_position", sql.IntegerCheck("position", 0, int.MaxValue));
        phones.WithCheckConstraint(sql, "ck_partyphones_kind", sql.IntegerCheck("kind", 1, 3));
        phones.WithCheckConstraint(sql, "ck_partyphones_originalvalue", sql.TextCheck("originalvalue", 2048, nonBlank: true));
        phones.WithCheckConstraint(sql, "ck_partyphones_e164", sql.TextCheck("e164", 16, nullable: true));
        phones.WithCheckConstraint(sql, "ck_partyphones_extension", sql.TextCheck("extension", 32, nullable: true));
        phones.WithCheckConstraint(sql, "ck_partyphones_sourcerecordtype", sql.TextCheck("sourcerecordtype", 64, nonBlank: true));
        phones.WithCheckConstraint(sql, "ck_partyphones_sourcerecordid", sql.TextCheck("sourcerecordid", 256, nonBlank: true));
        phones.WithCheckConstraint(sql, "ck_partyphones_sourcefieldname", sql.TextCheck("sourcefieldname", 128, nonBlank: true));
        phones.WithCheckConstraint(sql, "ck_partyphones_e164_format", "[e164] IS NULL OR (" + sql.E164(sql.Quote("e164")) + ")");
        phones.WithForeignKeyConstraint(sql, "fk_partyphones_party", ["partyid"], "parties", "id");
        phones.WithForeignKeyConstraint(sql, "fk_partyphones_address", ["addressid", "partyid"], "partyaddresses", "id", "partyid");
        Create.Index("ux_partyphones_position").OnTable("partyphones").InSchema(sql.SchemaName).OnColumn("partyid").Ascending().OnColumn("position").Ascending().WithOptions().Unique();
        Create.Index("ix_partyphones_number").OnTable("partyphones").InSchema(sql.SchemaName).OnColumn("e164").Ascending().OnColumn("partyid").Ascending();
        Create.Index("ix_partyphones_address").OnTable("partyphones").InSchema(sql.SchemaName).OnColumn("addressid").Ascending().OnColumn("partyid").Ascending();
    }
}

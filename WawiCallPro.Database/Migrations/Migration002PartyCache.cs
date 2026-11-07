using FluentMigrator;
using WawiCallPro.Database.Schema;

namespace WawiCallPro.Database.Migrations;

/// <summary>Adds the replaceable party, address and phone cache with explicit ownership keys.</summary>
[Migration(2, "Party address and phone cache")]
public sealed class Migration002PartyCache(DatabaseType databaseType) : ForwardOnlyMigration
{
    public override void Up()
    {
        foreach (var statement in Statements(databaseType)) Execute.Sql(statement);
    }

    internal static IEnumerable<string> Statements(DatabaseType type)
    {
        var sql = new SchemaSql(type);
        var parties = new SchemaTable(sql, "parties")
            .Id(primaryKey: true)
            .Id("companyid")
            .Hash("sourcehash")
            .Integer("kind", 1, 4)
            .Text("externalid", 256, nonBlank: true)
            .Text("ownerexternalid", 256, nullable: true, nonBlank: true)
            .Check("owner", "([kind] IN (1,2) AND [ownerexternalid] IS NULL) OR ([kind] IN (3,4) AND [ownerexternalid] IS NOT NULL)")
            .Text("displayname", 512, nonBlank: true)
            .Text("number", 128, nullable: true)
            .Text("email", 320, nullable: true)
            .Boolean("isinactive")
            .Utc("sourceupdatedatutc", nullable: true)
            .Utc("refreshedatutc")
            .Text("searchname", 1024)
            .Text("searchnumber", 256, nullable: true)
            .ForeignKey("company", ["companyid"], "companies", "id")
            .Unique("ux_parties_sourcehash", "sourcehash")
            .Index("ix_parties_company", "companyid", "sourcehash");

        var addresses = new SchemaTable(sql, "partyaddresses")
            .Id(primaryKey: true)
            .Id("partyid")
            .Hash("sourcehash")
            .Text("externalid", 256, nonBlank: true)
            .Integer("kind", 1, 4)
            .Integer("position", 0, int.MaxValue)
            .Text("companyname", 512, nullable: true)
            .Text("firstname", 256, nullable: true)
            .Text("lastname", 256, nullable: true)
            .Text("street", 512, nullable: true)
            .Text("addressline2", 512, nullable: true)
            .Text("housenumber", 64, nullable: true)
            .Text("postalcode", 32, nullable: true)
            .Text("city", 256, nullable: true)
            .Text("countrycode", 2, nullable: true)
            .Text("email", 320, nullable: true)
            .ForeignKey("party", ["partyid"], "parties", "id")
            .Unique("ux_partyaddresses_sourcehash", "sourcehash")
            .Unique("ux_partyaddresses_position", "partyid", "position")
            .Unique("ux_partyaddresses_owner", "id", "partyid");

        var phones = new SchemaTable(sql, "partyphones")
            .Id(primaryKey: true)
            .Id("partyid")
            .Id("addressid", nullable: true)
            .Integer("position", 0, int.MaxValue)
            .Integer("kind", 1, 3)
            .Text("originalvalue", 2048, nonBlank: true)
            .Text("e164", 16, nullable: true)
            .Text("extension", 32, nullable: true)
            .Text("sourcerecordtype", 64, nonBlank: true)
            .Text("sourcerecordid", 256, nonBlank: true)
            .Text("sourcefieldname", 128, nonBlank: true)
            .Check("e164_format", "[e164] IS NULL OR (" + sql.E164(sql.Quote("e164")) + ")")
            .ForeignKey("party", ["partyid"], "parties", "id")
            .ForeignKey("address", ["addressid", "partyid"], "partyaddresses", "id", "partyid")
            .Unique("ux_partyphones_position", "partyid", "position")
            .Index("ix_partyphones_number", "e164", "partyid")
            .Index("ix_partyphones_address", "addressid", "partyid");

        return parties.Statements().Concat(addresses.Statements()).Concat(phones.Statements());
    }
}

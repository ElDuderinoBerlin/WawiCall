using FluentMigrator;
using WawiCallPro.Database.Schema;

namespace WawiCallPro.Database.Migrations;

/// <summary>Adds append-only number parsing observations independent of the replaceable cache.</summary>
[Migration(5, "Number parsing observations")]
public sealed class Migration005NumberParseLogs(DatabaseType databaseType) : ForwardOnlyMigration
{
    public override void Up()
    {
        foreach (var statement in Statements(databaseType)) Execute.Sql(statement);
    }

    internal static IEnumerable<string> Statements(DatabaseType type)
    {
        var sql = new SchemaSql(type);
        var logs = new SchemaTable(sql, "numberparselogs")
            .Id(primaryKey: true)
            .Id("companyid")
            .Hash("partykeyhash")
            .Integer("kind", 1, 4)
            .Text("externalid", 256, nonBlank: true)
            .Text("ownerexternalid", 256, nullable: true, nonBlank: true)
            .Check("owner", "([kind] IN (1,2) AND [ownerexternalid] IS NULL) OR ([kind] IN (3,4) AND [ownerexternalid] IS NOT NULL)")
            .Text("sourcerecordtype", 64, nonBlank: true)
            .Text("sourcerecordid", 256, nonBlank: true)
            .Text("sourcefieldname", 128, nonBlank: true)
            .Integer("phonekind", 1, 3)
            .Text("originalvalue", 2048)
            .Utc("occurredatutc")
            .Text("code", 64, nonBlank: true)
            .Check("code_ascii", sql.AllowedAscii(sql.Quote("code"), "A-Za-z0-9_.-"))
            .ForeignKey("company", ["companyid"], "companies", "id")
            .Index("ix_numberparselogs_company", "companyid", "occurredatutc DESC", "id ASC")
            .Index("ix_numberparselogs_party", "partykeyhash", "occurredatutc DESC", "id ASC");
        return logs.Statements();
    }
}

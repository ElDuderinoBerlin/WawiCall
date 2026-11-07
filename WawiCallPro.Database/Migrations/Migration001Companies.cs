using FluentMigrator;
using WawiCallPro.Database.Schema;

namespace WawiCallPro.Database.Migrations;

/// <summary>Establishes application ownership and company metadata in the communication database.</summary>
[Migration(1, "Communication database ownership and companies")]
public sealed class Migration001Companies(DatabaseType databaseType) : ForwardOnlyMigration
{
    public override void Up()
    {
        foreach (var statement in Statements(databaseType)) Execute.Sql(statement);
        var insert = Insert.IntoTable("wawicallpro_metadata");
        if (databaseType == DatabaseType.SqlServer) insert.InSchema("dbo");
        if (databaseType == DatabaseType.PostgreSql) insert.InSchema("public");
        insert.Row(new
        {
            applicationid = "WawiCallPro.Communications",
            formatversion = 1,
            createdatutc = DateTime.UtcNow.Ticks
        });
    }

    internal static IEnumerable<string> Statements(DatabaseType type)
    {
        var sql = new SchemaSql(type);
        var metadata = new SchemaTable(sql, "wawicallpro_metadata")
            .Text("applicationid", 64, nonBlank: true)
            .Integer("formatversion", 1, int.MaxValue)
            .Utc("createdatutc")
            .PrimaryKey("applicationid")
            .Check("identity", sql.Exact(sql.Quote("applicationid"), "WawiCallPro.Communications"));

        var companies = new SchemaTable(sql, "companies")
            .Id(primaryKey: true)
            .Hash("sourcehash")
            .Integer("productline", 1, 2)
            .Text("installationid", 256, nonBlank: true)
            .Text("externalid", 256, nonBlank: true)
            .Text("displayname", 512, nonBlank: true)
            .Counter("writeversion")
            .Counter("cacheversion", 0)
            .Utc("cacherefreshedatutc", nullable: true)
            .Utc("createdatutc")
            .Utc("updatedatutc")
            .Check("chronology", "[createdatutc] <= [updatedatutc]")
            .Check("cache", "([cacheversion] = 0 AND [cacherefreshedatutc] IS NULL) OR ([cacheversion] > 0 AND [cacherefreshedatutc] IS NOT NULL)")
            .Unique("ux_companies_sourcehash", "sourcehash")
            .Index("ix_companies_productline", "productline", "sourcehash");

        return metadata.Statements().Concat(companies.Statements());
    }
}

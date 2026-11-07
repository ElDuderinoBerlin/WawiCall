using FluentMigrator;
using WawiCallPro.Database.Schema;

namespace WawiCallPro.Database.Migrations;

/// <summary>Establishes application ownership and company metadata in the communication database.</summary>
[Migration(1, "Communication database ownership and companies")]
public sealed class Migration001Companies(DatabaseType databaseType) : ForwardOnlyMigration
{
    public override void Up()
    {
        var sql = new SchemaSql(databaseType);

        var metadata = Create.Table("wawicallpro_metadata").InSchema(sql.SchemaName)
            .WithColumn("applicationid").AsString(64).NotNullable().PrimaryKey("pk_wawicallpro_metadata")
            .WithColumn("formatversion").AsInt32().NotNullable()
            .WithColumn("createdatutc").AsInt64().NotNullable();
        metadata.WithCheckConstraint(sql, "ck_wawicallpro_metadata_applicationid", sql.TextCheck("applicationid", 64, nonBlank: true));
        metadata.WithCheckConstraint(sql, "ck_wawicallpro_metadata_formatversion", sql.IntegerCheck("formatversion", 1, int.MaxValue));
        metadata.WithCheckConstraint(sql, "ck_wawicallpro_metadata_createdatutc", sql.IntegerCheck("createdatutc", 0, 3155378975999999999));
        metadata.WithCheckConstraint(sql, "ck_wawicallpro_metadata_identity", sql.Exact(sql.Quote("applicationid"), "WawiCallPro.Communications"));

        var companies = Create.Table("companies").InSchema(sql.SchemaName)
            .WithColumn("id").AsCustom(sql.HexType(32)).NotNullable().PrimaryKey("pk_companies")
            .WithColumn("sourcehash").AsCustom(sql.HexType(64)).NotNullable()
            .WithColumn("productline").AsInt32().NotNullable()
            .WithColumn("installationid").AsString(256).NotNullable()
            .WithColumn("externalid").AsString(256).NotNullable()
            .WithColumn("displayname").AsString(512).NotNullable()
            .WithColumn("writeversion").AsInt64().NotNullable()
            .WithColumn("cacheversion").AsInt64().NotNullable()
            .WithColumn("cacherefreshedatutc").AsInt64().Nullable()
            .WithColumn("createdatutc").AsInt64().NotNullable()
            .WithColumn("updatedatutc").AsInt64().NotNullable();
        companies.WithCheckConstraint(sql, "ck_companies_id", $"{sql.Hex(sql.Quote("id"), 32)} AND {sql.Quote("id")} <> '00000000000000000000000000000000'");
        companies.WithCheckConstraint(sql, "ck_companies_sourcehash", sql.Hex(sql.Quote("sourcehash"), 64));
        companies.WithCheckConstraint(sql, "ck_companies_productline", sql.IntegerCheck("productline", 1, 2));
        companies.WithCheckConstraint(sql, "ck_companies_installationid", sql.TextCheck("installationid", 256, nonBlank: true));
        companies.WithCheckConstraint(sql, "ck_companies_externalid", sql.TextCheck("externalid", 256, nonBlank: true));
        companies.WithCheckConstraint(sql, "ck_companies_displayname", sql.TextCheck("displayname", 512, nonBlank: true));
        companies.WithCheckConstraint(sql, "ck_companies_writeversion", sql.IntegerCheck("writeversion", 1, long.MaxValue));
        companies.WithCheckConstraint(sql, "ck_companies_cacheversion", sql.IntegerCheck("cacheversion", 0, long.MaxValue));
        companies.WithCheckConstraint(sql, "ck_companies_cacherefreshedatutc", sql.IntegerCheck("cacherefreshedatutc", 0, 3155378975999999999, nullable: true));
        companies.WithCheckConstraint(sql, "ck_companies_createdatutc", sql.IntegerCheck("createdatutc", 0, 3155378975999999999));
        companies.WithCheckConstraint(sql, "ck_companies_updatedatutc", sql.IntegerCheck("updatedatutc", 0, 3155378975999999999));
        companies.WithCheckConstraint(sql, "ck_companies_chronology", "[createdatutc] <= [updatedatutc]");
        companies.WithCheckConstraint(sql, "ck_companies_cache", "([cacheversion] = 0 AND [cacherefreshedatutc] IS NULL) OR ([cacheversion] > 0 AND [cacherefreshedatutc] IS NOT NULL)");
        Create.Index("ux_companies_sourcehash").OnTable("companies").InSchema(sql.SchemaName).OnColumn("sourcehash").Ascending().WithOptions().Unique();
        Create.Index("ix_companies_productline").OnTable("companies").InSchema(sql.SchemaName).OnColumn("productline").Ascending().OnColumn("sourcehash").Ascending();

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
}

using FluentMigrator;
using WawiCallPro.Database.Schema;

namespace WawiCallPro.Database.Migrations;

[Migration(7, "Persist cache parser and context provenance")]
public sealed class Migration007CacheProvenance(DatabaseType databaseType) : ForwardOnlyMigration
{
    public override void Up()
    {
        var sql = new SchemaSql(databaseType);
        Alter.Table("companies").InSchema(sql.SchemaName)
            .AddColumn("cacheparserversion").AsString(256).Nullable();
        var check = $"({sql.Quote("cacheparserversion")} IS NULL AND {sql.Quote("cachecontextversion")} IS NULL) OR " +
            $"({sql.Quote("cacheversion")}>0 AND {sql.Quote("cacheparserversion")} IS NOT NULL AND {sql.Quote("cachecontextversion")} IS NOT NULL AND " +
            $"{sql.Length(sql.Quote("cacheparserversion"))}<=256 AND {sql.Length(sql.Quote("cachecontextversion"))}<=256 AND " +
            $"{sql.NonBlank(sql.Quote("cacheparserversion"))} AND {sql.NonBlank(sql.Quote("cachecontextversion"))})";
        if (databaseType == DatabaseType.Sqlite)
        {
            // SQLite requires the cross-column CHECK on the new column in the same ALTER statement.
            Alter.Table("companies").AddColumn("cachecontextversion")
                .AsCustom($"TEXT CONSTRAINT {sql.Quote("ck_companies_cacheprovenance")} CHECK ({check})").Nullable();
        }
        else
        {
            Alter.Table("companies").InSchema(sql.SchemaName)
                .AddColumn("cachecontextversion").AsString(256).Nullable();
            Execute.Sql($"ALTER TABLE {sql.Table("companies")} ADD CONSTRAINT {sql.Quote("ck_companies_cacheprovenance")} CHECK ({check})");
        }
    }
}

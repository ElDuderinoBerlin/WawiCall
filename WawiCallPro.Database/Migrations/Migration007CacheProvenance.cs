using FluentMigrator;
using WawiCallPro.Database.Schema;

namespace WawiCallPro.Database.Migrations;

[Migration(7, "Persist cache parser and context provenance")]
public sealed class Migration007CacheProvenance(DatabaseType databaseType) : ForwardOnlyMigration
{
    public override void Up()
    {
        var sql = new SchemaSql(databaseType);
        var add = databaseType == DatabaseType.SqlServer ? " ADD " : " ADD COLUMN ";
        Execute.Sql($"ALTER TABLE {sql.Table("companies")}{add}{sql.Quote("cacheparserversion")} {sql.UnicodeType(256)} NULL");
        var check = $"({sql.Quote("cacheparserversion")} IS NULL AND {sql.Quote("cachecontextversion")} IS NULL) OR " +
            $"({sql.Quote("cacheversion")}>0 AND {sql.Quote("cacheparserversion")} IS NOT NULL AND {sql.Quote("cachecontextversion")} IS NOT NULL AND " +
            $"{sql.Length(sql.Quote("cacheparserversion"))}<=256 AND {sql.Length(sql.Quote("cachecontextversion"))}<=256 AND " +
            $"{sql.NonBlank(sql.Quote("cacheparserversion"))} AND {sql.NonBlank(sql.Quote("cachecontextversion"))})";
        Execute.Sql($"ALTER TABLE {sql.Table("companies")}{add}{sql.Quote("cachecontextversion")} {sql.UnicodeType(256)} NULL" +
            (databaseType == DatabaseType.Sqlite ? $" CONSTRAINT {sql.Quote("ck_companies_cacheprovenance")} CHECK ({check})" : ""));
        if (databaseType != DatabaseType.Sqlite)
            Execute.Sql($"ALTER TABLE {sql.Table("companies")} ADD CONSTRAINT {sql.Quote("ck_companies_cacheprovenance")} CHECK ({check})");
    }
}

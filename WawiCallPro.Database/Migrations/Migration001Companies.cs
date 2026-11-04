using FluentMigrator;
using WawiCallPro.Database.Schema;

namespace WawiCallPro.Database.Migrations;

/// <summary>Establishes application ownership and company metadata in the communication database.</summary>
[Migration(1, "Communication database ownership and companies")]
public sealed class Migration001Companies(DatabaseType databaseType) : ForwardOnlyMigration
{
    public override void Up()
    {
        foreach (var statement in InitialSchema.Statements(databaseType, 1)) Execute.Sql(statement);
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

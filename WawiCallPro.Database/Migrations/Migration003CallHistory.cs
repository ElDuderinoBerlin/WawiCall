using FluentMigrator;
using WawiCallPro.Database.Schema;

namespace WawiCallPro.Database.Migrations;

/// <summary>Adds call snapshots, historical candidates, events and independent notes.</summary>
[Migration(3, "Call history candidates events and notes")]
public sealed class Migration003CallHistory(DatabaseType databaseType) : ForwardOnlyMigration
{
    public override void Up()
    {
        foreach (var statement in InitialSchema.Statements(databaseType, 3)) Execute.Sql(statement);
    }
}

using FluentMigrator;
using WawiCallPro.Database.Schema;

namespace WawiCallPro.Database.Migrations;

/// <summary>Adds append-only number parsing observations independent of the replaceable cache.</summary>
[Migration(5, "Number parsing observations")]
public sealed class Migration005NumberParseLogs(DatabaseType databaseType) : ForwardOnlyMigration
{
    public override void Up()
    {
        foreach (var statement in InitialSchema.Statements(databaseType, 5)) Execute.Sql(statement);
    }
}

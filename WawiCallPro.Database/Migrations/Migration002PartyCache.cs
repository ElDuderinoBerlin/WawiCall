using FluentMigrator;
using WawiCallPro.Database.Schema;

namespace WawiCallPro.Database.Migrations;

/// <summary>Adds the replaceable party, address and phone cache with explicit ownership keys.</summary>
[Migration(2, "Party address and phone cache")]
public sealed class Migration002PartyCache(DatabaseType databaseType) : ForwardOnlyMigration
{
    public override void Up()
    {
        foreach (var statement in InitialSchema.Statements(databaseType, 2)) Execute.Sql(statement);
    }
}

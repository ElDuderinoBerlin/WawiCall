using FluentMigrator;
using WawiCallPro.Database.Schema;

namespace WawiCallPro.Database.Migrations;

/// <summary>Adds recording metadata; files remain outside the database.</summary>
[Migration(4, "Recording metadata")]
public sealed class Migration004Recordings(DatabaseType databaseType) : ForwardOnlyMigration
{
    public override void Up()
    {
        foreach (var statement in InitialSchema.Statements(databaseType, 4)) Execute.Sql(statement);
    }
}

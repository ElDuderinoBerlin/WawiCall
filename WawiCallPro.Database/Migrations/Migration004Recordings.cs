using FluentMigrator;
using WawiCallPro.Database.Schema;

namespace WawiCallPro.Database.Migrations;

/// <summary>Adds recording metadata; files remain outside the database.</summary>
[Migration(4, "Recording metadata")]
public sealed class Migration004Recordings(DatabaseType databaseType) : ForwardOnlyMigration
{
    public override void Up()
    {
        foreach (var statement in Statements(databaseType)) Execute.Sql(statement);
    }

    internal static IEnumerable<string> Statements(DatabaseType type)
    {
        var sql = new SchemaSql(type);
        var recordings = new SchemaTable(sql, "recordings")
            .Id(primaryKey: true)
            .Id("callid")
            .Text("relativepath", 1024, nonBlank: true)
            .Text("format", 32, nonBlank: true)
            .Counter("durationticks", 0, nullable: true)
            .Integer("availability", 0, 3)
            .Counter("writeversion")
            .Utc("createdatutc")
            .Utc("updatedatutc")
            .Check("chronology", "[createdatutc] <= [updatedatutc]")
            // Full Windows device/alias/reparse validation stays in the write contract and file layer.
            .Check("path", "[relativepath] NOT LIKE '/%' AND [relativepath] NOT LIKE '%/' AND [relativepath] NOT LIKE '%:%' AND [relativepath] NOT LIKE '%//%' AND [relativepath] <> '.' AND [relativepath] <> '..' AND [relativepath] NOT LIKE './%' AND [relativepath] NOT LIKE '../%' AND [relativepath] NOT LIKE '%/./%' AND [relativepath] NOT LIKE '%/../%' AND [relativepath] NOT LIKE '%/.' AND [relativepath] NOT LIKE '%/..'")
            .Check("separator", $"{sql.Length(sql.Quote("relativepath"))} = {sql.Length($"REPLACE({sql.Quote("relativepath")}, {sql.Character(92)}, '')")}")
            .ForeignKey("call", ["callid"], "phonecalls", "id")
            .Index("ix_recordings_call", "callid", "id");
        return recordings.Statements();
    }
}

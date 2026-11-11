using FluentMigrator;

namespace WawiCallPro.Database.Migrations;

/// <summary>SQL Server treats the trailing hyphen in the original LIKE character class as a range. Preserve all observations.</summary>
[Migration(9, "Accept hyphenated number diagnostic codes on SQL Server")]
public sealed class Migration009NumberDiagnosticCodes(DatabaseType databaseType) : ForwardOnlyMigration
{
    public override void Up()
    {
        if (databaseType != DatabaseType.SqlServer) return;
        // CHECK syntax is provider-specific; earlier migration definitions and existing data remain unchanged.
        Execute.Sql("ALTER TABLE [dbo].[numberparselogs] DROP CONSTRAINT [ck_numberparselogs_code_ascii]");
        Execute.Sql("ALTER TABLE [dbo].[numberparselogs] ADD CONSTRAINT [ck_numberparselogs_code_ascii] CHECK ([code] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^-A-Za-z0-9_.]%')");
    }
}

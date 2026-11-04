namespace WawiCallPro.Database;

internal static class DatabaseSql
{
    internal static string Table(DatabaseType type, string name) => type switch
    {
        DatabaseType.SqlServer => "[dbo].[" + name.Replace("]", "]]", StringComparison.Ordinal) + "]",
        DatabaseType.MySql => "`" + name.Replace("`", "``", StringComparison.Ordinal) + "`",
        DatabaseType.PostgreSql => "\"public\".\"" + name.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"",
        _ => "\"" + name.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
    };

    internal static string CreateDatabase(DatabaseTarget target) => target.Type switch
    {
        DatabaseType.SqlServer => "CREATE DATABASE [" + target.DatabaseName.Replace("]", "]]", StringComparison.Ordinal) + "]",
        DatabaseType.MySql => "CREATE DATABASE `" + target.DatabaseName.Replace("`", "``", StringComparison.Ordinal) + "` CHARACTER SET utf8mb4",
        DatabaseType.PostgreSql => "CREATE DATABASE \"" + target.DatabaseName.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"",
        _ => throw new DatabaseOperationException(DatabaseErrorCode.InvalidTarget)
    };

    internal static string DatabaseExists(DatabaseType type) => type switch
    {
        DatabaseType.SqlServer => "SELECT 1 FROM sys.databases WHERE name = @name",
        DatabaseType.MySql => "SELECT 1 FROM information_schema.schemata WHERE schema_name = @name",
        DatabaseType.PostgreSql => "SELECT 1 FROM pg_catalog.pg_database WHERE datname = @name",
        _ => throw new DatabaseOperationException(DatabaseErrorCode.InvalidTarget)
    };

    internal static string Objects(DatabaseType type) => type switch
    {
        DatabaseType.Sqlite => "SELECT 'main', name FROM sqlite_schema WHERE type IN ('table','view','trigger') AND substr(name,1,7) <> 'sqlite_'",
        DatabaseType.SqlServer => "SELECT s.name, o.name FROM sys.objects o JOIN sys.schemas s ON s.schema_id=o.schema_id WHERE o.is_ms_shipped=0 AND o.type IN ('U','V','SO','SN','P','PC','FN','IF','TF','FS','FT','AF','TR')",
        DatabaseType.MySql => "SELECT table_schema, table_name FROM information_schema.tables WHERE table_schema = DATABASE() UNION ALL SELECT routine_schema,routine_name FROM information_schema.routines WHERE routine_schema=DATABASE()",
        DatabaseType.PostgreSql => "SELECT n.nspname, c.relname FROM pg_catalog.pg_class c JOIN pg_catalog.pg_namespace n ON n.oid=c.relnamespace WHERE c.relkind IN ('r','p','v','m','f','S') AND left(n.nspname,3) <> 'pg_' AND n.nspname <> 'information_schema' UNION ALL SELECT n.nspname,p.proname FROM pg_catalog.pg_proc p JOIN pg_catalog.pg_namespace n ON n.oid=p.pronamespace WHERE left(n.nspname,3)<>'pg_' AND n.nspname<>'information_schema'",
        _ => throw new DatabaseOperationException(DatabaseErrorCode.InvalidTarget)
    };

    internal static string Schema(DatabaseTarget target) => target.Type switch
    {
        DatabaseType.Sqlite => "main",
        DatabaseType.SqlServer => "dbo",
        DatabaseType.MySql => target.DatabaseName,
        _ => "public"
    };
}

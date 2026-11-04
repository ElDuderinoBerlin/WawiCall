namespace WawiCallPro.Database;

/// <summary>Providers for the owned communications database, independent of Lexware source adapters.</summary>
public enum DatabaseType
{
    Sqlite = 1,
    SqlServer = 2,
    MySql = 3,
    PostgreSql = 4
}

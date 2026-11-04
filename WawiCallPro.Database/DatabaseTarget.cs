using System.Text.Json.Serialization;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using MySqlConnector;
using Npgsql;

namespace WawiCallPro.Database;

/// <summary>Validated connection configuration. Reveal plaintext only through a connection created by DatabaseFactory.</summary>
public sealed class DatabaseTarget
{
    private DatabaseTarget(DatabaseType type, string connectionString, string databaseName, string? sqlitePath,
        int connectionTimeoutSeconds, int commandTimeoutSeconds)
    {
        Type = type;
        ConnectionString = connectionString;
        DatabaseName = databaseName;
        SqlitePath = sqlitePath;
        ConnectionTimeoutSeconds = connectionTimeoutSeconds;
        CommandTimeoutSeconds = commandTimeoutSeconds;
    }

    public DatabaseType Type { get; }
    public int ConnectionTimeoutSeconds { get; }
    public int CommandTimeoutSeconds { get; }
    [JsonIgnore] internal string ConnectionString { get; }
    [JsonIgnore] internal string DatabaseName { get; }
    [JsonIgnore] internal string? SqlitePath { get; }

    public static DatabaseTarget Sqlite(string absoluteFilePath, int commandTimeoutSeconds = 30)
    {
        ValidateTimeout(commandTimeoutSeconds);
        try
        {
            if (string.IsNullOrWhiteSpace(absoluteFilePath) || !Path.IsPathFullyQualified(absoluteFilePath)
                || absoluteFilePath.IndexOf('\0') >= 0) throw Invalid();
            var path = Path.GetFullPath(absoluteFilePath);
            if (string.IsNullOrEmpty(Path.GetFileName(path)) || path[Path.GetPathRoot(path)!.Length..].Contains(':')) throw Invalid();
            _ = new System.Text.UTF8Encoding(false, true).GetByteCount(path);
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = path, Mode = SqliteOpenMode.ReadWrite, Pooling = false,
                ForeignKeys = true, DefaultTimeout = commandTimeoutSeconds
            };
            return new DatabaseTarget(DatabaseType.Sqlite, builder.ToString(), Path.GetFileName(path), path, commandTimeoutSeconds, commandTimeoutSeconds);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw Invalid();
        }
    }

    public static DatabaseTarget Server(DatabaseType type, string connectionString, int connectionTimeoutSeconds = 15, int commandTimeoutSeconds = 30)
    {
        if (type is not (DatabaseType.SqlServer or DatabaseType.MySql or DatabaseType.PostgreSql)) throw Invalid();
        ValidateTimeout(connectionTimeoutSeconds);
        ValidateTimeout(commandTimeoutSeconds);
        if (string.IsNullOrWhiteSpace(connectionString)) throw Invalid();
        try
        {
            string normalized;
            string name;
            switch (type)
            {
                case DatabaseType.SqlServer:
                    var sql = new SqlConnectionStringBuilder(connectionString)
                    {
                        ConnectTimeout = connectionTimeoutSeconds, ApplicationName = "WawiCallPro", Enlist = false
                    };
                    if (!string.IsNullOrEmpty(sql.AttachDBFilename) || sql.UserInstance || string.IsNullOrWhiteSpace(sql.DataSource)) throw Invalid();
                    name = sql.InitialCatalog;
                    normalized = sql.ConnectionString;
                    break;
                case DatabaseType.MySql:
                    var mysql = new MySqlConnectionStringBuilder(connectionString)
                    {
                        ConnectionTimeout = (uint)connectionTimeoutSeconds, DefaultCommandTimeout = (uint)commandTimeoutSeconds,
                        AutoEnlist = false
                    };
                    if (string.IsNullOrWhiteSpace(mysql.Server)) throw Invalid();
                    name = mysql.Database;
                    normalized = mysql.ConnectionString;
                    break;
                default:
                    var postgres = new NpgsqlConnectionStringBuilder(connectionString)
                    {
                        Timeout = connectionTimeoutSeconds, CommandTimeout = commandTimeoutSeconds,
                        ApplicationName = "WawiCallPro", SearchPath = "public", Enlist = false
                    };
                    if (string.IsNullOrWhiteSpace(postgres.Host)) throw Invalid();
                    name = postgres.Database ?? "";
                    normalized = postgres.ConnectionString;
                    break;
            }
            var bytes = new System.Text.UTF8Encoding(false, true).GetByteCount(name);
            var maximum = type == DatabaseType.MySql ? 64 : 128;
            if (string.IsNullOrWhiteSpace(name) || name.Length > maximum || type == DatabaseType.PostgreSql && bytes > 63 || name.Any(char.IsControl)
                || name != name.Trim() || IsSystemDatabase(name)) throw Invalid();
            return new DatabaseTarget(type, normalized, name, null, connectionTimeoutSeconds, commandTimeoutSeconds);
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or NotSupportedException or OverflowException)
        {
            throw Invalid();
        }
    }

    private static bool IsSystemDatabase(string name) => name.ToLowerInvariant() is
        "master" or "model" or "msdb" or "tempdb" or "mysql" or "information_schema" or "performance_schema" or "sys"
        or "postgres" or "template0" or "template1";

    private static void ValidateTimeout(int seconds)
    {
        if (seconds is < 1 or > 300) throw Invalid();
    }

    private static DatabaseOperationException Invalid() => new(DatabaseErrorCode.InvalidTarget);
    public override string ToString() => $"{nameof(DatabaseTarget)} ({Type})";
}

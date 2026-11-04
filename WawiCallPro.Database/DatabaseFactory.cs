using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using MySqlConnector;
using Npgsql;

namespace WawiCallPro.Database;

/// <summary>Creates closed connections. Ordinary SQLite opens never create a database file.</summary>
public sealed class DatabaseFactory
{
    /// <summary>ReadOnly enforces SQLite file mode; server status services use only metadata SELECT statements.</summary>
    public DbConnection CreateConnection(DatabaseTarget target, bool readOnly = false)
    {
        ArgumentNullException.ThrowIfNull(target);
        return target.Type switch
        {
            DatabaseType.Sqlite => new SqliteConnection(new SqliteConnectionStringBuilder(target.ConnectionString)
                { Mode = readOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWrite }.ToString()),
            DatabaseType.SqlServer => new SqlConnection(target.ConnectionString),
            DatabaseType.MySql => new MySqlConnection(target.ConnectionString),
            DatabaseType.PostgreSql => new NpgsqlConnection(target.ConnectionString),
            _ => throw new DatabaseOperationException(DatabaseErrorCode.InvalidTarget)
        };
    }

    internal DbConnection CreateMaintenanceConnection(DatabaseTarget target) => target.Type switch
    {
        DatabaseType.SqlServer => new SqlConnection(new SqlConnectionStringBuilder(target.ConnectionString) { InitialCatalog = "master" }.ConnectionString),
        DatabaseType.MySql => new MySqlConnection(new MySqlConnectionStringBuilder(target.ConnectionString) { Database = "" }.ConnectionString),
        DatabaseType.PostgreSql => new NpgsqlConnection(new NpgsqlConnectionStringBuilder(target.ConnectionString) { Database = "postgres" }.ConnectionString),
        _ => throw new DatabaseOperationException(DatabaseErrorCode.InvalidTarget)
    };

    internal static DbCommand Command(DbConnection connection, DatabaseTarget target, string sql)
    {
        var command = connection.CreateCommand();
        command.CommandTimeout = target.CommandTimeoutSeconds;
        command.CommandText = sql;
        return command;
    }
}

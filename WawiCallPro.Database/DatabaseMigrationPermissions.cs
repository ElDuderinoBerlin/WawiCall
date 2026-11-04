using System.Data.Common;
using System.Text.RegularExpressions;

namespace WawiCallPro.Database;

public interface IDatabaseMigrationPermissions
{
    Task EnsureAsync(DatabaseTarget target, CancellationToken cancellationToken = default);
    Task EnsureMetadataVisibilityAsync(DatabaseTarget target, CancellationToken cancellationToken = default);
}

/// <summary>Checks DDL/DML capability without creating a probe table or changing version metadata.</summary>
public sealed class DatabaseMigrationPermissions(DatabaseFactory factory) : IDatabaseMigrationPermissions
{
    public async Task EnsureMetadataVisibilityAsync(DatabaseTarget target, CancellationToken cancellationToken = default)
    {
        await using var connection = factory.CreateConnection(target);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            if (target.Type == DatabaseType.MySql)
            {
                await CheckMySqlAsync(connection, target, cancellationToken, fullMetadata: true).ConfigureAwait(false);
                return;
            }
            var sql = target.Type switch
            {
                DatabaseType.SqlServer => "SELECT HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','VIEW DEFINITION')",
                DatabaseType.PostgreSql => "SELECT pg_has_role(datdba,'USAGE') FROM pg_database WHERE datname=current_database()",
                _ => "SELECT 1"
            };
            await using var command = DatabaseFactory.Command(connection, target, sql);
            if (Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)) != 1)
                throw new DatabaseOperationException(DatabaseErrorCode.AccessDenied);
        }
        catch (DbException exception) { throw new DatabaseOperationException(DatabaseOperationException.Classify(exception)); }
    }

    public async Task EnsureAsync(DatabaseTarget target, CancellationToken cancellationToken = default)
    {
        try
        {
            if (target.Type == DatabaseType.Sqlite && (File.GetAttributes(target.SqlitePath!) & FileAttributes.ReadOnly) != 0)
                throw new DatabaseOperationException(DatabaseErrorCode.AccessDenied);
            await using var connection = factory.CreateConnection(target);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await EnsureAsync(connection, target, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception) when (exception is DbException or IOException or UnauthorizedAccessException)
        { throw new DatabaseOperationException(DatabaseOperationException.Classify(exception)); }
    }

    /// <summary>Checks a caller-owned open session, including an explicitly impersonated maintenance session.</summary>
    public async Task EnsureAsync(DbConnection connection, DatabaseTarget target, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(target);
        if (connection.State != System.Data.ConnectionState.Open ||
            target.Type != DatabaseType.Sqlite && !string.Equals(connection.Database, target.DatabaseName,
                target.Type == DatabaseType.PostgreSql ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase))
            throw new DatabaseOperationException(DatabaseErrorCode.InvalidTarget);
        try
        {
            if (target.Type == DatabaseType.MySql)
            {
                await CheckMySqlAsync(connection, target, cancellationToken).ConfigureAwait(false);
                return;
            }
            var sql = target.Type switch
            {
                DatabaseType.SqlServer => "SELECT CASE WHEN HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','CREATE TABLE')=1 AND " +
                    "HAS_PERMS_BY_NAME('dbo','SCHEMA','ALTER')=1 AND HAS_PERMS_BY_NAME('dbo','SCHEMA','SELECT')=1 AND " +
                    "HAS_PERMS_BY_NAME('dbo','SCHEMA','INSERT')=1 AND HAS_PERMS_BY_NAME('dbo','SCHEMA','UPDATE')=1 AND " +
                    "HAS_PERMS_BY_NAME('dbo','SCHEMA','DELETE')=1 AND HAS_PERMS_BY_NAME('dbo','SCHEMA','REFERENCES')=1 THEN 1 ELSE 0 END",
                DatabaseType.PostgreSql => "SELECT has_schema_privilege(current_user,'public','CREATE') AND has_schema_privilege(current_user,'public','USAGE') AND " +
                    "NOT EXISTS(SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='public' AND c.relkind IN ('r','p') AND NOT pg_has_role(c.relowner,'USAGE'))",
                _ => "PRAGMA query_only"
            };
            await using var command = DatabaseFactory.Command(connection, target, sql);
            var value = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
            if (value != (target.Type == DatabaseType.Sqlite ? 0 : 1)) throw new DatabaseOperationException(DatabaseErrorCode.AccessDenied);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception) when (exception is DbException or IOException or UnauthorizedAccessException)
        { throw new DatabaseOperationException(DatabaseOperationException.Classify(exception)); }
    }

    private static async Task CheckMySqlAsync(DbConnection connection, DatabaseTarget target, CancellationToken token, bool fullMetadata = false)
    {
        var roles = new List<string>();
        await using (var command = DatabaseFactory.Command(connection, target, "SELECT ROLE_NAME, ROLE_HOST FROM information_schema.enabled_roles"))
        await using (var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(token).ConfigureAwait(false))
            {
                if (roles.Count >= 256) throw new DatabaseOperationException(DatabaseErrorCode.AccessDenied);
                roles.Add(Quote(reader.GetString(0)) + "@" + Quote(reader.GetString(1)));
            }
        }
        var needed = new HashSet<string>(["CREATE", "ALTER", "DROP", "INDEX", "REFERENCES", "SELECT", "INSERT", "UPDATE", "DELETE"], StringComparer.OrdinalIgnoreCase);
        var statements = new List<(string Privileges, string Pattern, bool Global)>();
        var metadataVisible = !fullMetadata;
        await using (var command = DatabaseFactory.Command(connection, target,
            "SHOW GRANTS FOR CURRENT_USER" + (roles.Count == 0 ? "" : " USING " + string.Join(',', roles))))
        await using (var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(token).ConfigureAwait(false))
            {
                var match = Regex.Match(reader.GetString(0), @"^GRANT (.+?) ON (\*|`(?:``|[^`])*`)\.\* TO ", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
                if (!match.Success) continue;
                var pattern = match.Groups[2].Value;
                statements.Add((match.Groups[1].Value, pattern == "*" ? "%" : pattern[1..^1].Replace("``", "`", StringComparison.Ordinal), pattern == "*"));
            }
        }
        foreach (var (privileges, pattern, global) in statements)
        {
            await using var command = DatabaseFactory.Command(connection, target, "SELECT @name LIKE @pattern");
            var name = command.CreateParameter(); name.ParameterName = "@name"; name.Value = target.DatabaseName; command.Parameters.Add(name);
            var scope = command.CreateParameter(); scope.ParameterName = "@pattern"; scope.Value = pattern; command.Parameters.Add(scope);
            if (Convert.ToInt32(await command.ExecuteScalarAsync(token).ConfigureAwait(false)) != 1) continue;
            if (global && (privileges == "ALL PRIVILEGES" || privileges.Split(',', StringSplitOptions.TrimEntries).Any(value => value is "SHOW_ROUTINE" or "SELECT"))) metadataVisible = true;
            if (privileges == "ALL PRIVILEGES") needed.Clear();
            else foreach (var privilege in privileges.Split(',', StringSplitOptions.TrimEntries)) needed.Remove(privilege);
        }
        if (needed.Count != 0 || !metadataVisible) throw new DatabaseOperationException(DatabaseErrorCode.AccessDenied);
    }

    private static string Quote(string identifier) => "`" + identifier.Replace("`", "``", StringComparison.Ordinal) + "`";
}

using System.Collections.Immutable;
using System.Data;
using System.Data.Common;
using System.Text.Json;
using Dapper;
using Microsoft.Data.Sqlite;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Common;

namespace WawiCallPro.Database.Repositories;

/// <summary>One fresh transaction per operation. A shared gate excludes concurrent schema changes and restores.</summary>
public sealed class DatabaseRepositoryContext(IDatabaseTargetSource targets, DatabaseFactory factory,
    IDatabaseMigrationEngineFactory engines, IDatabaseMigrationLock gate)
{
    public DatabaseRepositoryContext(DatabaseTarget target, DatabaseFactory factory)
        : this(new FixedTarget(target), factory, new DatabaseMigrationEngineFactory(), new DatabaseMigrationLock()) { }

    internal Task<T> RunAsync<T>(bool write, Func<RepositorySession, Task<T>> action, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var target = targets.GetTarget();
        return target.Type == DatabaseType.Sqlite ? Task.Run(() => RunCoreAsync(target, write, action, token), token) : RunCoreAsync(target, write, action, token);
    }

    private async Task<T> RunCoreAsync<T>(DatabaseTarget target, bool write, Func<RepositorySession, Task<T>> action, CancellationToken token)
    {
        try
        {
            await using var lease = await gate.AcquireAsync(target, TimeSpan.FromSeconds(30), token).ConfigureAwait(false);
            var status = await new DatabaseStatusService(factory, engines.GetEngine(target.Type).Catalog).GetStatusAsync(target, token).ConfigureAwait(false);
            if (status.State != DatabaseSchemaState.Current) throw new DatabaseOperationException(status.Error ?? DatabaseErrorCode.InvalidSchema);
            await using var connection = factory.CreateConnection(target, readOnly: !write);
            await connection.OpenAsync(token).ConfigureAwait(false);
            await using var transaction = connection is SqliteConnection sqlite ? sqlite.BeginTransaction(deferred: !write) :
                await connection.BeginTransactionAsync(IsolationLevel.Serializable, token).ConfigureAwait(false);
            var session = new RepositorySession(connection, transaction, target, token);
            var result = await action(session).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            await lease.EnsureHeldAsync(token).ConfigureAwait(false);
            try { await transaction.CommitAsync(CancellationToken.None).ConfigureAwait(false); }
            catch (DbException) { throw new DatabaseOperationException(DatabaseErrorCode.CommitUncertain); }
            return result;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (RepositoryConflictException) { throw; }
        catch (DatabaseOperationException) { throw; }
        catch (DbException exception) { throw new DatabaseOperationException(DatabaseOperationException.Classify(exception)); }
        catch (Exception exception) when (exception is JsonException or InvalidCastException or FormatException or ArgumentException or OverflowException)
        { throw new DatabaseOperationException(DatabaseErrorCode.InvalidSchema); }
    }

    private sealed class FixedTarget(DatabaseTarget target) : IDatabaseTargetSource { public DatabaseTarget GetTarget() => target; }
}

internal sealed class RepositorySession(DbConnection connection, DbTransaction transaction, DatabaseTarget target, CancellationToken token)
{
    internal DatabaseType Type => target.Type;
    internal string T(string table) => DatabaseSql.Table(Type, table);
    internal string Q(string column) => Type switch { DatabaseType.SqlServer => "[" + column + "]", DatabaseType.MySql => "`" + column + "`", _ => "\"" + column + "\"" };
    internal CancellationToken Token => token;
    internal string In(string column, string parameter) => Type == DatabaseType.PostgreSql ? column + " = ANY(" + parameter + ")" : column + " IN " + parameter;

    internal async Task<List<RepositoryRow>> RowsAsync(string sql, object? parameters = null)
    {
        var rows = await connection.QueryAsync(Command(sql, parameters)).ConfigureAwait(false);
        return rows.Select(row => new RepositoryRow((IDictionary<string, object>)row)).ToList();
    }
    internal async Task<RepositoryRow?> OneAsync(string sql, object? parameters = null)
    {
        var rows = await RowsAsync(sql, parameters).ConfigureAwait(false);
        if (rows.Count > 1) throw new DatabaseOperationException(DatabaseErrorCode.InvalidSchema);
        return rows.Count == 0 ? null : rows[0];
    }
    internal Task<int> ExecuteAsync(string sql, object? parameters = null) => connection.ExecuteAsync(Command(sql, parameters));
    internal Task<T?> ScalarAsync<T>(string sql, object? parameters = null) => connection.ExecuteScalarAsync<T>(Command(sql, parameters));

    internal Task<int> InsertAsync(string table, Dictionary<string, object?> values) => ExecuteAsync(
        $"INSERT INTO {T(table)} ({string.Join(',', values.Keys.Select(Q))}) VALUES ({string.Join(',', values.Keys.Select(key => "@" + key))})", Parameters(values));

    internal async Task UpdateAsync(string table, string keyColumn, string key, long expected, Dictionary<string, object?> values)
    {
        var parameters = Parameters(values); parameters.Add("_key", key, DbType.AnsiString); parameters.Add("_expected", expected);
        if (await ExecuteAsync($"UPDATE {T(table)} SET {string.Join(',', values.Keys.Select(column => Q(column) + "=@" + column))} WHERE {Q(keyColumn)}=@_key AND {Q("writeversion")}=@_expected", parameters).ConfigureAwait(false) != 1)
            throw new RepositoryConflictException(RepositoryConflictKind.VersionMismatch);
    }

    internal string PageSql(string select, string order, PageRequest page) => Type == DatabaseType.SqlServer ?
        $"{select} ORDER BY {order} OFFSET {page.Offset} ROWS FETCH NEXT {page.Limit + 1} ROWS ONLY" :
        $"{select} ORDER BY {order} LIMIT {page.Limit + 1} OFFSET {page.Offset}";

    internal string Ordinal(string column, string parameter)
    {
        var comparison = Type switch
        {
            DatabaseType.SqlServer => column + " COLLATE Latin1_General_100_BIN2=" + parameter,
            DatabaseType.MySql => column + " COLLATE utf8mb4_bin=" + parameter,
            _ => column + "=" + parameter
        };
        var length = Type switch { DatabaseType.SqlServer => "DATALENGTH", DatabaseType.MySql => "CHAR_LENGTH", _ => "length" };
        return $"({comparison} AND {length}({column})={length}({parameter}))";
    }
    internal string Contains(string column, string parameter) => (Type switch
    {
        DatabaseType.SqlServer => column + " COLLATE Latin1_General_100_BIN2",
        DatabaseType.MySql => column + " COLLATE utf8mb4_bin",
        DatabaseType.PostgreSql => column + " COLLATE \"C\"",
        _ => column
    }) + " LIKE " + parameter + " ESCAPE '!'";

    private CommandDefinition Command(string sql, object? parameters) => new(sql, parameters, transaction,
        commandTimeout: target.CommandTimeoutSeconds, cancellationToken: token);

    internal static DynamicParameters Parameters(Dictionary<string, object?> values)
    {
        var result = new DynamicParameters();
        foreach (var (key, value) in values)
            result.Add(key, value is bool flag ? flag ? 1 : 0 : value, value is string && key is "id" or "companyid" or "callid" or "partyid" or "addressid" or "sourcehash" or "partykeyhash" or "workstationid" or "assignedcompanyid" ? DbType.AnsiString : null);
        return result;
    }
    internal static Page<T> Page<T>(IReadOnlyList<T> rows, PageRequest page) => new(rows.Take(page.Limit).ToImmutableArray(), rows.Count > page.Limit);
    internal static long Next(long value) => value == long.MaxValue ? throw new RepositoryConflictException(RepositoryConflictKind.VersionExhausted) : checked(value + 1);
    internal static void Expected(RepositoryRow? row, long? expected)
    {
        if (row is null && expected is not null) throw new RepositoryConflictException(RepositoryConflictKind.MissingRecord);
        if (row is not null && expected is null) throw new RepositoryConflictException(RepositoryConflictKind.AlreadyExists);
        if (row is not null && row.Long("writeversion") != expected) throw new RepositoryConflictException(RepositoryConflictKind.VersionMismatch);
    }
}

internal sealed class RepositoryRow(IDictionary<string, object> values)
{
    internal object? Value(string column) => values.TryGetValue(column, out var value) ? value is DBNull ? null : value : throw new DatabaseOperationException(DatabaseErrorCode.InvalidSchema);
    internal string Text(string column) => Value(column) as string ?? throw new DatabaseOperationException(DatabaseErrorCode.InvalidSchema);
    internal string? OptionalText(string column) => Value(column) is null ? null : Text(column);
    internal long Long(string column) => Value(column) switch { long number => number, int number => number, short number => number, byte number => number, uint number => number, _ => throw new DatabaseOperationException(DatabaseErrorCode.InvalidSchema) };
    internal long? OptionalLong(string column) => Value(column) is null ? null : Long(column);
    internal bool Bool(string column) => Long(column) switch { 0 => false, 1 => true, _ => throw new DatabaseOperationException(DatabaseErrorCode.InvalidSchema) };
    internal Guid Id(string column) => DatabaseId.Decode(Text(column));
    internal DateTimeOffset Utc(string column) => DatabaseUtc.Decode(Long(column));
    internal DateTimeOffset? OptionalUtc(string column) => Value(column) is null ? null : Utc(column);
}

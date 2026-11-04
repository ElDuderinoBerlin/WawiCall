using System.Data.Common;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using MySqlConnector;
using Npgsql;

namespace WawiCallPro.Database.Tests;

/// <summary>Explicit native acceptance on disposable local instances only; never invoked by the ordinary test run.</summary>
public static class ServerBackupAcceptance
{
    private static readonly string[] Tables = ["companies", "parties", "partyaddresses", "partyphones", "phonecalls", "callparties", "calllookups", "callnotes", "callevents", "recordings", "numberparselogs", "phonematchsettings", "phonenumberblockrules", "versioninfo", "wawicallpro_metadata"];

    public static async Task RunAsync(DatabaseTarget source, string artifactDirectory, string mysqlBin, string postgresBin)
    {
        var directory = Path.Combine(Path.GetFullPath(artifactDirectory), "backup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var destinationName = "wcp_restore_" + Guid.NewGuid().ToString("N");
        var factory = new DatabaseFactory();
        var catalog = new FluentMigratorEngine(source.Type).Catalog;
        Assert.AreEqual(DatabaseSchemaState.Current, (await new DatabaseStatusService(factory, catalog).GetStatusAsync(source)).State);
        var context = new WawiCallPro.Database.Repositories.DatabaseRepositoryContext(source, factory);
        var company = new WawiCallPro.Interfaces.Companies.CompanyKey(WawiCallPro.Interfaces.Companies.LexwareProductLine.Pro, "Backup Ä 电话", Guid.NewGuid().ToString("N"));
        await new WawiCallPro.Database.Repositories.DapperCompanyRepository(context).WriteAsync(new(new(company, "Backup cache"), null, RepositoryAcceptance.Time));
        var party = new WawiCallPro.Interfaces.Parties.Party
        {
            Key = new(company, WawiCallPro.Interfaces.Parties.PartyKind.Customer, "0001 "), DisplayName = "Stammdaten Ä 电话",
            Addresses = [new() { ExternalId = "0001", Kind = WawiCallPro.Interfaces.Parties.AddressKind.Billing, Street = "Straße Ä", Phones = [new("030 1234567", WawiCallPro.Interfaces.Parties.PhoneKind.Telephone, new("address", "0001", "phone"), "+49301234567", "001")] }]
        };
        await new WawiCallPro.Database.Repositories.DapperPartyCacheRepository(context).ReplaceAsync(new(company, 0, [party], RepositoryAcceptance.Time, "backup-parser", "backup-context"));
        await using var lease = await new DatabaseMigrationLock().AcquireAsync(source, TimeSpan.FromSeconds(20));
        var before = await SnapshotAsync(source);
        DatabaseTarget destination;
        switch (source.Type)
        {
            case DatabaseType.SqlServer:
                var sql = new SqlConnectionStringBuilder(source.ConnectionString) { InitialCatalog = destinationName, Pooling = false };
                destination = DatabaseTarget.Server(source.Type, sql.ConnectionString, 5, 60);
                break;
            case DatabaseType.MySql:
                var mysql = new MySqlConnectionStringBuilder(source.ConnectionString) { Database = destinationName, Pooling = false };
                destination = DatabaseTarget.Server(source.Type, mysql.ConnectionString, 5, 60);
                break;
            case DatabaseType.PostgreSql:
                var pg = new NpgsqlConnectionStringBuilder(source.ConnectionString) { Database = destinationName, Pooling = false };
                destination = DatabaseTarget.Server(source.Type, pg.ConnectionString, 5, 60);
                break;
            default: throw new ArgumentException("Native server only.");
        }
        var destinationOwned = false;
        try
        {
            if (source.Type == DatabaseType.SqlServer)
            {
                var backup = Path.Combine(directory, "snapshot.bak");
                await using var maintenance = factory.CreateMaintenanceConnection(source); await maintenance.OpenAsync();
                await ExecuteAsync(maintenance, "BACKUP DATABASE " + Quote(source.Type, source.DatabaseName) + " TO DISK=@file WITH COPY_ONLY,CHECKSUM,FORMAT", new() { ["file"] = backup });
                await ExecuteAsync(maintenance, "RESTORE VERIFYONLY FROM DISK=@file WITH CHECKSUM", new() { ["file"] = backup });
                using var files = maintenance.CreateCommand(); files.CommandText = "RESTORE FILELISTONLY FROM DISK=@file"; Add(files, "file", backup);
                var moves = new Dictionary<string, object?> { ["file"] = backup };
                var clauses = new List<string>();
                await using (var reader = await files.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var index = clauses.Count; var type = reader.GetString(reader.GetOrdinal("Type"));
                        Assert.IsTrue(type is "D" or "L");
                        moves.Add("logical" + index, reader.GetString(reader.GetOrdinal("LogicalName")));
                        moves.Add("path" + index, Path.Combine(directory, "restored-" + index + (type == "L" ? ".ldf" : ".mdf")));
                        clauses.Add("MOVE @logical" + index + " TO @path" + index);
                    }
                }
                using (var absent = maintenance.CreateCommand())
                {
                    absent.CommandText = "SELECT DB_ID(@name)"; Add(absent, "name", destinationName);
                    Assert.AreEqual(DBNull.Value, await absent.ExecuteScalarAsync());
                }
                // Unique destination on this private instance; no REPLACE and no existing database/file is reused.
                destinationOwned = true;
                await ExecuteAsync(maintenance, "RESTORE DATABASE " + Quote(source.Type, destinationName) + " FROM DISK=@file WITH CHECKSUM,RECOVERY," + string.Join(',', clauses), moves);
                var corrupt = Path.Combine(directory, "truncated.bak"); await File.WriteAllBytesAsync(corrupt, [0, 1, 2, 3]);
                await Assert.ThrowsAsync<DbException>(() => ExecuteAsync(maintenance, "RESTORE VERIFYONLY FROM DISK=@file WITH CHECKSUM", new() { ["file"] = corrupt }));
            }
            else
            {
                await new DatabaseProvisioner(factory).CreateAsync(destination); destinationOwned = true;
                if (source.Type == DatabaseType.MySql)
                {
                    var mysql = new MySqlConnectionStringBuilder(source.ConnectionString);
                    Assert.AreEqual(MySqlSslMode.None, mysql.SslMode); // Private loopback audit, not a production TLS policy.
                    var common = new[] { "--no-defaults", "--protocol=TCP", "--host=" + mysql.Server, "--port=" + mysql.Port, "--user=" + mysql.UserID, "--default-character-set=utf8mb4" };
                    var environment = new Dictionary<string, string> { ["MYSQL_PWD"] = mysql.Password };
                    var dump = Path.Combine(directory, "snapshot.sql");
                    await RunAsync(Path.Combine(mysqlBin, "mysqldump.exe"), [.. common, "--single-transaction", "--quick", "--routines", "--events", "--triggers", "--no-tablespaces", "--set-gtid-purged=OFF", "--skip-add-drop-table", source.DatabaseName], environment, output: dump);
                    await RunAsync(Path.Combine(mysqlBin, "mysql.exe"), [.. common, "--binary-mode", destinationName], environment, input: dump);
                }
                else
                {
                    var pg = new NpgsqlConnectionStringBuilder(source.ConnectionString);
                    Assert.AreEqual(SslMode.Disable, pg.SslMode);
                    var environment = new Dictionary<string, string> { ["PGHOST"] = pg.Host!, ["PGPORT"] = pg.Port.ToString(System.Globalization.CultureInfo.InvariantCulture), ["PGUSER"] = pg.Username!, ["PGPASSWORD"] = pg.Password ?? "", ["PGSSLMODE"] = "disable" };
                    var dump = Path.Combine(directory, "snapshot.dump");
                    await RunAsync(Path.Combine(postgresBin, "pg_dump.exe"), ["--no-password", "--format=custom", "--no-owner", "--no-privileges", "--file=" + dump, "--dbname=" + source.DatabaseName], environment);
                    await RunAsync(Path.Combine(postgresBin, "pg_restore.exe"), ["--no-password", "--single-transaction", "--exit-on-error", "--no-owner", "--no-privileges", "--dbname=" + destinationName, dump], environment);
                }
            }
            Assert.AreEqual(DatabaseSchemaState.Current, (await new DatabaseStatusService(factory, catalog).GetStatusAsync(destination)).State);
            var after = await SnapshotAsync(destination);
            CollectionAssert.AreEqual(before, after);
            CollectionAssert.AreEqual(before, await SnapshotAsync(source));
            await lease.EnsureHeldAsync();
            Console.WriteLine(source.Type + ": native backup/restore preserved all 15 tables including call history, rule evidence, origin, Unicode and UTC ticks.");
        }
        finally
        {
            if (destinationOwned)
            {
                await using var maintenance = factory.CreateMaintenanceConnection(destination); await maintenance.OpenAsync();
                var name = Quote(destination.Type, destinationName);
                if (destination.Type == DatabaseType.SqlServer) await ExecuteAsync(maintenance, "IF DB_ID(@name) IS NOT NULL BEGIN ALTER DATABASE " + name + " SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE " + name + "; END", new() { ["name"] = destinationName });
                else await ExecuteAsync(maintenance, "DROP DATABASE " + name);
            }
        }
    }

    internal static async Task<string[]> SnapshotAsync(DatabaseTarget target)
    {
        await using var db = new DatabaseFactory().CreateConnection(target, readOnly: true); await db.OpenAsync();
        var result = new List<string>();
        foreach (var table in Tables)
        {
            using var command = db.CreateCommand(); command.CommandText = "SELECT * FROM " + Quote(target.Type, table);
            await using var reader = await command.ExecuteReaderAsync(); var rows = new List<string>();
            while (await reader.ReadAsync())
            {
                var values = new object[reader.FieldCount]; reader.GetValues(values);
                rows.Add(JsonSerializer.Serialize(values.Select(value => value is DBNull ? null : value).ToArray()));
            }
            result.Add(table + ":" + string.Join('\n', rows.Order(StringComparer.Ordinal)));
        }
        return result.ToArray();
    }
    private static string Quote(DatabaseType type, string name) => type switch { DatabaseType.SqlServer => "[" + name.Replace("]", "]]", StringComparison.Ordinal) + "]", DatabaseType.MySql => "`" + name.Replace("`", "``", StringComparison.Ordinal) + "`", _ => "\"" + name.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"" };
    private static void Add(DbCommand command, string name, object? value) { var parameter = command.CreateParameter(); parameter.ParameterName = "@" + name; parameter.Value = value ?? DBNull.Value; command.Parameters.Add(parameter); }
    private static async Task ExecuteAsync(DbConnection db, string sql, Dictionary<string, object?>? parameters = null)
    {
        using var command = db.CreateCommand(); command.CommandText = sql; command.CommandTimeout = 60;
        if (parameters is not null) foreach (var (name, value) in parameters) Add(command, name, value);
        await command.ExecuteNonQueryAsync();
    }
    private static async Task RunAsync(string executable, string[] arguments, Dictionary<string, string> environment, string? input = null, string? output = null)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true, RedirectStandardInput = input is not null };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        foreach (var (name, value) in environment) start.Environment[name] = value;
        using var process = Process.Start(start)!;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        var error = process.StandardError.ReadToEndAsync(deadline.Token);
        try
        {
            var read = output is null ? process.StandardOutput.BaseStream.CopyToAsync(Stream.Null, deadline.Token) : CopyOutputAsync();
            if (input is not null)
            {
                await using var file = File.OpenRead(input); await file.CopyToAsync(process.StandardInput.BaseStream, deadline.Token); process.StandardInput.Close();
            }
            await process.WaitForExitAsync(deadline.Token); await read; _ = await error;
            Assert.AreEqual(0, process.ExitCode, "Native backup utility failed; private provider output is intentionally omitted.");
        }
        finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        async Task CopyOutputAsync() { await using var file = new FileStream(output!, FileMode.CreateNew, FileAccess.Write, FileShare.None); await process.StandardOutput.BaseStream.CopyToAsync(file, deadline.Token); }
    }
}

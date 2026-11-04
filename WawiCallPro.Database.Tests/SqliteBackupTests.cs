using Microsoft.Data.Sqlite;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Database.Repositories;
using WawiCallPro.Interfaces.Companies;

namespace WawiCallPro.Database.Tests;

[TestClass]
public sealed class SqliteBackupTests
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WawiCallPro-backup-" + Guid.NewGuid().ToString("N"));
    private readonly ISqliteDatabaseBackup _backup = new SqliteDatabaseBackup(new(), new DatabaseMigrationEngineFactory(), new DatabaseMigrationLock());
    [TestCleanup] public void Cleanup() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
    private DatabaseTarget Target(string file) => DatabaseTarget.Sqlite(Path.Combine(_root, file));

    [TestMethod]
    public async Task BackupIncludesCommittedWalAndRestoreRemainsIndependentOfTheOriginal()
    {
        var source = Target("source.db"); await new DatabaseProvisioner(new()).CreateAsync(source);
        await new DatabaseMigrator(new(), new FluentMigratorEngine(DatabaseType.Sqlite)).MigrateAsync(source);
        var repository = new DapperCompanyRepository(new(source, new()));
        var key = new CompanyKey(LexwareProductLine.Pro, "installation", "0001");
        await repository.WriteAsync(new(new(key, "Before"), null, RepositoryAcceptance.Time));
        await using var writer = (SqliteConnection)new DatabaseFactory().CreateConnection(source); await writer.OpenAsync();
        using (var command = writer.CreateCommand()) { command.CommandText = "PRAGMA journal_mode=WAL; PRAGMA wal_autocheckpoint=0; UPDATE companies SET displayname='WAL Ä 电话'"; await command.ExecuteNonQueryAsync(); }
        Assert.IsTrue(new FileInfo(source.SqlitePath! + "-wal").Length > 0);
        var mainFile = ReadShared(source.SqlitePath!);
        var snapshot = Target("backup.db"); await _backup.CreateAsync(source, snapshot.SqlitePath!);
        CollectionAssert.AreEqual(mainFile, ReadShared(source.SqlitePath!));
        Assert.IsFalse(File.Exists(snapshot.SqlitePath! + "-wal"));
        using (var command = writer.CreateCommand()) { command.CommandText = "UPDATE companies SET displayname='After'"; await command.ExecuteNonQueryAsync(); }
        var restored = Target("restored.db"); await _backup.RestoreNewAsync(snapshot.SqlitePath!, restored);
        Assert.AreEqual("WAL Ä 电话", (await new DapperCompanyRepository(new(restored, new())).GetAsync(key))!.Company.DisplayName);
        Assert.AreEqual("After", (await repository.GetAsync(key))!.Company.DisplayName);
    }

    [TestMethod]
    public async Task ExistingTargetsMissingSourcesCancellationAndForeignSchemasDoNotOverwriteFiles()
    {
        Directory.CreateDirectory(_root); var existing = Target("existing.db"); await File.WriteAllTextAsync(existing.SqlitePath!, "sentinel");
        Assert.AreEqual(DatabaseErrorCode.AlreadyExists, (await Assert.ThrowsExactlyAsync<DatabaseOperationException>(() => _backup.RestoreNewAsync(Target("missing.db").SqlitePath!, existing))).Code);
        Assert.AreEqual("sentinel", await File.ReadAllTextAsync(existing.SqlitePath!));
        var destination = Target("new.db");
        Assert.AreEqual(DatabaseErrorCode.MissingDatabase, (await Assert.ThrowsExactlyAsync<DatabaseOperationException>(() => _backup.CreateAsync(Target("missing.db"), destination.SqlitePath!))).Code);
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => _backup.CreateAsync(existing, destination.SqlitePath!, canceled.Token));
        var foreign = Target("foreign.db"); await new DatabaseProvisioner(new()).CreateAsync(foreign);
        await using (var db = new DatabaseFactory().CreateConnection(foreign)) { await db.OpenAsync(); using var command = db.CreateCommand(); command.CommandText = "CREATE TABLE unrelated(value TEXT)"; await command.ExecuteNonQueryAsync(); }
        Assert.AreEqual(DatabaseErrorCode.ForeignDatabase, (await Assert.ThrowsExactlyAsync<DatabaseOperationException>(() => _backup.CreateAsync(foreign, destination.SqlitePath!))).Code);
        Assert.IsFalse(File.Exists(destination.SqlitePath!));
        await File.WriteAllTextAsync(destination.SqlitePath! + "-wal", "orphaned WAL belongs to another operation");
        Assert.AreEqual(DatabaseErrorCode.AlreadyExists, (await Assert.ThrowsExactlyAsync<DatabaseOperationException>(() => _backup.CreateAsync(foreign, destination.SqlitePath!))).Code);
        Assert.AreEqual("orphaned WAL belongs to another operation", await File.ReadAllTextAsync(destination.SqlitePath! + "-wal"));
        Assert.AreEqual(0, Directory.GetFiles(_root, "*.partial*").Length);
    }

    [TestMethod]
    public async Task PriorOwnedSchemaCanBeBackedUpAndRestoredBeforeUpgrading()
    {
        var old = Target("version5.db"); await new DatabaseProvisioner(new()).CreateAsync(old);
        new FluentMigratorEngine(DatabaseType.Sqlite).MigrateUp(old, 5);
        var saved = Target("v5.backup"); await _backup.CreateAsync(old, saved.SqlitePath!);
        var restored = Target("restored.db"); await _backup.RestoreNewAsync(saved.SqlitePath!, restored);
        var migrator = new DatabaseMigrator(new(), new FluentMigratorEngine(DatabaseType.Sqlite));
        Assert.AreEqual(DatabaseSchemaState.Current, (await migrator.MigrateAsync(restored)).State);
        var originalStatus = await new DatabaseStatusService(new(), new FluentMigratorEngine(DatabaseType.Sqlite).Catalog).GetStatusAsync(old);
        Assert.AreEqual(DatabaseSchemaState.MigrationRequired, originalStatus.State);
        Assert.AreEqual(5L, originalStatus.AppliedVersions[^1]);
    }

    private static byte[] ReadShared(string path)
    {
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var output = new MemoryStream(); input.CopyTo(output); return output.ToArray();
    }
}

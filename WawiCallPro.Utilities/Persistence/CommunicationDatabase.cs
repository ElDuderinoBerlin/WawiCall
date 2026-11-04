using Microsoft.Extensions.Logging;
using WawiCallPro.Database;

namespace WawiCallPro.Utilities.Persistence;

public interface ICommunicationDatabase
{
    Task<DatabaseMigrationStatus> GetStatusAsync(CancellationToken cancellationToken = default);
    Task<DatabaseMigrationStatus> CreateAndMigrateAsync(IProgress<DatabaseMigrationProgress>? progress = null, CancellationToken cancellationToken = default);
    Task<DatabaseMigrationStatus> MigrateAsync(bool approveEmpty = false, IProgress<DatabaseMigrationProgress>? progress = null, CancellationToken cancellationToken = default);
    Task BackupSqliteAsync(string destinationFile, CancellationToken cancellationToken = default);
    Task RestoreSqliteNewAsync(string backupFile, CancellationToken cancellationToken = default);
}

/// <summary>Shared host facade; all I/O remains explicit and independent of ERP or SIP startup.</summary>
public sealed class CommunicationDatabase(IDatabaseTargetSource targets, DatabaseFactory factory, IDatabaseMigrationEngineFactory engines,
    IDatabaseMigrationLock gate, IDatabaseMigrationPermissions permissions, IWorkstationIdentity workstation,
    ISqliteDatabaseBackup backup, ILogger<DatabaseMigrator> logger) : ICommunicationDatabase
{
    public Task<DatabaseMigrationStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var target = targets.GetTarget();
        return new DatabaseStatusService(factory, engines.GetEngine(target.Type).Catalog).GetStatusAsync(target, cancellationToken);
    }
    public async Task<DatabaseMigrationStatus> CreateAndMigrateAsync(IProgress<DatabaseMigrationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var target = targets.GetTarget();
        var receipt = await new DatabaseProvisioner(factory, gate, permissions).CreateAsync(target, cancellationToken).ConfigureAwait(false);
        var status = await Migrator(target).MigrateAsync(target, receipt, progress, cancellationToken).ConfigureAwait(false);
        await workstation.InitializeAsync(cancellationToken).ConfigureAwait(false);
        return status;
    }
    public async Task<DatabaseMigrationStatus> MigrateAsync(bool approveEmpty = false, IProgress<DatabaseMigrationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var target = targets.GetTarget();
        var receipt = approveEmpty ? await new DatabaseProvisioner(factory, gate, permissions).ApproveEmptyAsync(target, cancellationToken).ConfigureAwait(false) : null;
        return await Migrator(target).MigrateAsync(target, receipt, progress, cancellationToken).ConfigureAwait(false);
    }
    public Task BackupSqliteAsync(string destinationFile, CancellationToken cancellationToken = default) => backup.CreateAsync(targets.GetTarget(), destinationFile, cancellationToken);
    public Task RestoreSqliteNewAsync(string backupFile, CancellationToken cancellationToken = default) => backup.RestoreNewAsync(backupFile, targets.GetTarget(), cancellationToken);
    private DatabaseMigrator Migrator(DatabaseTarget target) => new(factory, engines.GetEngine(target.Type), gate, permissions, TimeSpan.FromSeconds(30), logger);
}

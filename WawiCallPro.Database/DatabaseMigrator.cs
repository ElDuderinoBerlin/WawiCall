using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace WawiCallPro.Database;

/// <summary>
/// Orchestrates the standard runner adapter, never provisions or downgrades. Cross-process database leases are
/// a separate integration step; this instance rejects overlapping calls rather than running two local updates.
/// </summary>
public sealed class DatabaseMigrator
{
    private readonly IDatabaseMigrationEngine _engine;
    private readonly DatabaseStatusService _status;
    private readonly ILogger<DatabaseMigrator> _logger;
    private int _running;

    public DatabaseMigrator(DatabaseFactory factory, IDatabaseMigrationEngine engine, ILogger<DatabaseMigrator>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(engine);
        _engine = engine;
        _status = new DatabaseStatusService(factory, engine.Catalog);
        _logger = logger ?? NullLogger<DatabaseMigrator>.Instance;
    }

    public async Task<DatabaseMigrationStatus> MigrateAsync(DatabaseTarget target, DatabaseCreation? creation = null,
        IProgress<DatabaseMigrationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        cancellationToken.ThrowIfCancellationRequested();
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
            throw new DatabaseOperationException(DatabaseErrorCode.MigrationBusy);
        var completed = 0;
        var total = 0;
        long? applying = null;
        try
        {
            if (_engine.Catalog.Versions.IsEmpty) throw new DatabaseOperationException(DatabaseErrorCode.MigrationDefinitionsMissing);
            Report(progress, DatabaseMigrationPhase.Inspecting, null, 0, 0);
            var status = await _status.GetStatusAsync(target, cancellationToken).ConfigureAwait(false);
            EnsureMigratable(status);
            if (status.State == DatabaseSchemaState.EmptyDatabase && target.Type != DatabaseType.Sqlite && creation?.Matches(target) != true)
                throw new DatabaseOperationException(DatabaseErrorCode.OwnershipUnverified);
            total = status.PendingVersions.Length;
            var alreadyApplied = status.AppliedVersions.Length;
            foreach (var version in status.PendingVersions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                applying = version;
                Report(progress, DatabaseMigrationPhase.Applying, version, completed, total);
                // A synchronous FluentMigrator step cannot be abandoned midway by a UI cancellation token.
                await Task.Run(() => _engine.MigrateUp(target, version), CancellationToken.None).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                Report(progress, DatabaseMigrationPhase.Verifying, version, completed, total);
                status = await _status.GetStatusAsync(target, cancellationToken).ConfigureAwait(false);
                EnsureMigratable(status);
                var expected = _engine.Catalog.Versions.Take(alreadyApplied + completed + 1);
                if (!status.AppliedVersions.SequenceEqual(expected))
                    throw new DatabaseOperationException(DatabaseErrorCode.MigrationVerificationFailed);
                completed++;
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (status.State != DatabaseSchemaState.Current) throw new DatabaseOperationException(DatabaseErrorCode.MigrationVerificationFailed);
            Report(progress, DatabaseMigrationPhase.Completed, status.LatestAvailableVersion, completed, total);
            return status;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            TryReport(progress, DatabaseMigrationPhase.Cancelled, applying, completed, total);
            throw;
        }
        catch (Exception exception)
        {
            var safe = exception as DatabaseOperationException ?? new DatabaseOperationException(DatabaseErrorCode.MigrationFailed);
            _logger.LogError(new EventId(3199), safe, "Database migration failed: {Code}", safe.Code);
            TryReport(progress, DatabaseMigrationPhase.Failed, applying, completed, total);
            throw safe;
        }
        finally { Volatile.Write(ref _running, 0); }
    }

    private static void EnsureMigratable(DatabaseMigrationStatus status)
    {
        if (status.State is DatabaseSchemaState.EmptyDatabase or DatabaseSchemaState.MigrationRequired or DatabaseSchemaState.Current) return;
        throw new DatabaseOperationException(status.Error ?? DatabaseErrorCode.InvalidSchema);
    }

    private void Report(IProgress<DatabaseMigrationProgress>? progress, DatabaseMigrationPhase phase, long? version, int completed, int total)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation(new EventId(3100 + (int)phase), "Database migration progress: {Phase}, version {Version}, completed {Completed}/{Total}", phase, version, completed, total);
        try { progress?.Report(new DatabaseMigrationProgress(phase, version, completed, total)); }
        catch { throw new DatabaseOperationException(DatabaseErrorCode.ProgressFailed); }
    }

    private void TryReport(IProgress<DatabaseMigrationProgress>? progress, DatabaseMigrationPhase phase, long? version, int completed, int total)
    {
        try { Report(progress, phase, version, completed, total); }
        catch (DatabaseOperationException) { /* The original operation failure/cancellation remains observable. */ }
    }
}

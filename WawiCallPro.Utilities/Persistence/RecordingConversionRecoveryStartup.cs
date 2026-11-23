using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WawiCallPro.Database;
using WawiCallPro.Utilities.Operations;

namespace WawiCallPro.Utilities.Persistence;

/// <summary>One observed background delivery pass after configuration startup. No SIP lease or encoder is required.</summary>
public sealed class RecordingConversionRecoveryStartup(
    IRecordingConversionRecovery recovery,
    ILogger<RecordingConversionRecoveryStartup> log,
    IInterruptedMp3Recovery interrupted) : BackgroundService, IAsyncDisposable
{
    private readonly IRecordingConversionQueue? _queue;
    private readonly ICommunicationDatabase? _database;
    private readonly ActiveDatabaseReady? _databaseReady;

    public RecordingConversionRecoveryStartup(IRecordingConversionRecovery recovery, ILogger<RecordingConversionRecoveryStartup> log,
        IInterruptedMp3Recovery interrupted, IRecordingConversionQueue queue, ICommunicationDatabase? database = null, ActiveDatabaseReady? databaseReady = null)
        : this(recovery, log, interrupted)
    {
        _queue = queue;
        _database = database;
        _databaseReady = databaseReady;
    }
    private int _disposed;
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await CheckAndRecoverAsync(stoppingToken).ConfigureAwait(false);
        if (_databaseReady is null) return;
        try
        {
            await foreach (var _ in _databaseReady.ReadAllAsync(stoppingToken).ConfigureAwait(false))
                await CheckAndRecoverAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    private async Task CheckAndRecoverAsync(CancellationToken stoppingToken)
    {
        try
        {
            if (_database is not null)
            {
                var status = await _database.GetStatusAsync(stoppingToken).ConfigureAwait(false);
                if (status.State != DatabaseSchemaState.Current)
                {
                    var operation = status.State switch
                    {
                        DatabaseSchemaState.MigrationRequired => OperationEvent.RecordingConversionSchemaUpdateRequired,
                        DatabaseSchemaState.NewerSchema => OperationEvent.RecordingConversionNewerSchema,
                        _ => OperationEvent.RecordingConversionDatabaseUnavailable
                    };
                    OperationEvents.Write(log, operation);
                    return;
                }
            }
            var delivered = await recovery.RecoverAsync(stoppingToken).ConfigureAwait(false);
            stoppingToken.ThrowIfCancellationRequested();
            var recovered = await interrupted.RecoverAsync(stoppingToken).ConfigureAwait(false);
            stoppingToken.ThrowIfCancellationRequested();
            if (delivered.Failed == 0 && delivered.Deferred == 0 && recovered.Failed == 0 && recovered.Deferred == 0 && !recovered.HasMore) _queue?.Request();
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception) { OperationEvents.Write(log, OperationEvent.RecordingConversionCheckFailed); }
    }
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        try { await StopAsync(CancellationToken.None).ConfigureAwait(false); }
        finally { base.Dispose(); }
    }
}

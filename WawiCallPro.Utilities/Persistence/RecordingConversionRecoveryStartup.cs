using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WawiCallPro.Utilities.Operations;

namespace WawiCallPro.Utilities.Persistence;

/// <summary>One observed background delivery pass after configuration startup. No SIP lease or encoder is required.</summary>
public sealed class RecordingConversionRecoveryStartup(
    IRecordingConversionRecovery recovery,
    ILogger<RecordingConversionRecoveryStartup> log,
    IInterruptedMp3Recovery interrupted) : BackgroundService, IAsyncDisposable
{
    private readonly IRecordingConversionQueue? _queue;
    public RecordingConversionRecoveryStartup(IRecordingConversionRecovery recovery, ILogger<RecordingConversionRecoveryStartup> log,
        IInterruptedMp3Recovery interrupted, IRecordingConversionQueue queue) : this(recovery, log, interrupted) => _queue = queue;
    private int _disposed;
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var delivered = await recovery.RecoverAsync(stoppingToken).ConfigureAwait(false);
            stoppingToken.ThrowIfCancellationRequested();
            var recovered = await interrupted.RecoverAsync(stoppingToken).ConfigureAwait(false);
            stoppingToken.ThrowIfCancellationRequested();
            if (delivered.Failed == 0 && delivered.Deferred == 0 && recovered.Failed == 0 && recovered.Deferred == 0 && !recovered.HasMore) _queue?.Request();
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception) { OperationEvents.Write(log, OperationEvent.RecordingConversionRecoveryIncomplete); }
    }
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        try { await StopAsync(CancellationToken.None).ConfigureAwait(false); }
        finally { base.Dispose(); }
    }
}

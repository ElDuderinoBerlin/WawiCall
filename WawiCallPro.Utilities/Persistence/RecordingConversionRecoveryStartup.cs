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
    private int _disposed;
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await recovery.RecoverAsync(stoppingToken).ConfigureAwait(false);
            stoppingToken.ThrowIfCancellationRequested();
            await interrupted.RecoverAsync(stoppingToken).ConfigureAwait(false);
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

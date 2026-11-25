using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WawiCallPro.Utilities.Operations;

namespace WawiCallPro.Utilities.Persistence;

/// <summary>Queues a coalesced audit request without blocking telephony startup.</summary>
public interface IRecordingArtifactAuditScheduler
{
    void Request();
}

/// <summary>One owned background audit and one coalesced pending request; startup itself performs no file/database work.</summary>
public sealed class RecordingArtifactAuditScheduler(IRecordingArtifactReconciliation audit, ILogger<RecordingArtifactAuditScheduler> log)
    : BackgroundService, IRecordingArtifactAuditScheduler, IAsyncDisposable
{
    private readonly IRecordingDirectoryInventory? _inventory;
    public RecordingArtifactAuditScheduler(IRecordingArtifactReconciliation audit, ILogger<RecordingArtifactAuditScheduler> log,
        IRecordingDirectoryInventory inventory) : this(audit, log) => _inventory = inventory;

    private readonly Channel<byte> _requests = Channel.CreateBounded<byte>(new BoundedChannelOptions(1)
        { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });
    private int _disposed;
    public void Request() => _requests.Writer.TryWrite(0);
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var request in _requests.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
            {
                if (stoppingToken.IsCancellationRequested) break;
                try
                {
                    await audit.ReconcileAsync(stoppingToken).ConfigureAwait(false);
                    if (_inventory is not null) await InspectDirectoryAsync(stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception) { OperationEvents.Write(log, OperationEvent.RecordingArtifactAuditIncomplete); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
    private async Task InspectDirectoryAsync(CancellationToken token)
    {
        try
        {
            await _inventory!.ReadAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception) { OperationEvents.Write(log, OperationEvent.RecordingDirectoryInventoryIncomplete); }
    }
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _requests.Writer.TryComplete(); await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        try { await StopAsync(CancellationToken.None).ConfigureAwait(false); }
        finally { base.Dispose(); }
    }
}

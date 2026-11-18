using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Utilities.Operations;

namespace WawiCallPro.Utilities.Persistence;

public sealed record RecordingConversionBatchResult(int Completed, int Failed, int Deferred);
public interface IRecordingConversionBatch
{
    Task<RecordingConversionBatchResult> RunAsync(CancellationToken cancellationToken = default);
}

/// <summary>Consumes only existing local Pending jobs. The session factory rechecks ownership, files and attempt state.</summary>
public sealed class RecordingConversionBatch(IRecordingConversionRepository jobs, IWorkstationIdentity workstation,
    IPersistedMp3ConversionFactory sessions) : IRecordingConversionBatch
{
    private readonly SemaphoreSlim _gate = new(1);
    public async Task<RecordingConversionBatchResult> RunAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var local = await workstation.GetAsync(cancellationToken).ConfigureAwait(false);
            if (local is null) return new(0, 0, 1);
            var completed = 0; var failed = 0; var deferred = 0; Guid? after = null;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var page = await jobs.GetPageAsync(local.Value, after, 100, cancellationToken).ConfigureAwait(false);
                if (page.Items.IsDefault || page.Items.Length > 100 || page.HasMore && page.Items.IsEmpty)
                    throw new InvalidOperationException("Invalid conversion page.");
                var previous = after;
                foreach (var row in page.Items)
                {
                    if (row is null || previous is { } id && StringComparer.Ordinal.Compare(DatabaseId.Encode(row.Job.Id), DatabaseId.Encode(id)) <= 0)
                        throw new InvalidOperationException("Conversion page did not advance.");
                    previous = row.Job.Id;
                }
                foreach (var saved in page.Items)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (saved.Job.State != RecordingConversionState.Pending) continue;
                    try
                    {
                        StoredRecordingConversionJournal result;
                        await using (var session = await sessions.StartAsync(saved.Job.Id, cancellationToken).ConfigureAwait(false))
                            result = await session.RunAsync(cancellationToken).ConfigureAwait(false);
                        if (result.Conversion.Job.State == RecordingConversionState.Completed) completed++; else failed++;
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                    catch (Exception) { deferred++; }
                }
                if (!page.HasMore) return new(completed, failed, deferred);
                after = page.Items[^1].Job.Id;
            }
        }
        finally { _gate.Release(); }
    }
}

public interface IRecordingConversionQueue
{
    /// <summary>Requests a coalesced pass with bounded pages. Does not create or requeue durable jobs.</summary>
    void Request();
}

/// <summary>One owned encoder pass and at most one pending request; starting the worker alone performs no conversion.</summary>
public sealed class RecordingConversionQueue(IRecordingConversionBatch batch, ILogger<RecordingConversionQueue> log)
    : BackgroundService, IRecordingConversionQueue, IAsyncDisposable
{
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
                try
                {
                    var result = await batch.RunAsync(stoppingToken).ConfigureAwait(false);
                    if (result.Failed > 0 || result.Deferred > 0) OperationEvents.Write(log, OperationEvent.RecordingConversionQueueIncomplete);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception) { OperationEvents.Write(log, OperationEvent.RecordingConversionQueueIncomplete); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
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

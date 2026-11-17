using WawiCallPro.Database.Contracts;
using WawiCallPro.Database.Repositories;

namespace WawiCallPro.Utilities.Persistence;

/// <summary>Protects the exact terminal pair before DB delivery; only confirmed pairs are acknowledged.</summary>
public sealed class DurableRecordingConversionJournal(DapperRecordingConversionJournal inner, IRecordingConversionOutbox pending) : IRecordingConversionJournal
{
    public async Task<StoredRecordingConversionJournal> BeginAsync(RecordingArtifactWrite target, RecordingConversionWrite conversion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conversion);
        cancellationToken.ThrowIfCancellationRequested();
        await using var empty = pending.AcquireEmpty(conversion.Job.Id);
        return await inner.BeginAsync(target, conversion, cancellationToken).ConfigureAwait(false);
    }
    public async Task<StoredRecordingConversionJournal> WriteAsync(RecordingArtifactWrite target, RecordingConversionWrite conversion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conversion);
        if (conversion.Job.State == RecordingConversionState.Pending)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var empty = pending.AcquireEmpty(conversion.Job.Id);
            return await inner.WriteAsync(target, conversion, cancellationToken).ConfigureAwait(false);
        }
        await using var delivery = await pending.StageAsync(target, conversion, cancellationToken).ConfigureAwait(false);
        var saved = await inner.WriteAsync(delivery.Target, delivery.Conversion, cancellationToken).ConfigureAwait(false);
        delivery.Acknowledge(); return saved;
    }
}

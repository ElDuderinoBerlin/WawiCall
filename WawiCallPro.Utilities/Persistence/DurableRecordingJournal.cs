using WawiCallPro.Database.Contracts;
using WawiCallPro.Database.Repositories;
using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Utilities.Persistence;

/// <summary>Stages terminal metadata durably before database delivery. Unacknowledged entries survive process loss.</summary>
public sealed class DurableRecordingJournal(
    DapperRecordingJournal inner,
    IRecordingJournalOutbox pending,
    IRecordingCompletionPublisher completions) : IRecordingJournal
{
    private readonly IRecordingConversionQueue? _conversions;
    public DurableRecordingJournal(DapperRecordingJournal inner, IRecordingJournalOutbox pending,
        IRecordingCompletionPublisher completions, IRecordingConversionQueue conversions) : this(inner, pending, completions) => _conversions = conversions;
    public Task<StoredRecordingJournal> BeginAsync(RecordingWrite recording, RecordingCaptureWrite capture, CancellationToken cancellationToken = default) =>
        inner.BeginAsync(recording, capture, cancellationToken);
    public async Task<StoredRecordingJournal> WriteAsync(RecordingWrite recording, RecordingCaptureWrite capture, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recording); ArgumentNullException.ThrowIfNull(capture);
        if (capture.Capture.State is PcmRecordingState.Capturing or PcmRecordingState.Stopping)
            return await inner.WriteAsync(recording, capture, cancellationToken).ConfigureAwait(false);
        StoredRecordingJournal result;
        await using (var delivery = await pending.StageAsync(recording, capture, cancellationToken).ConfigureAwait(false))
        {
            completions.Validate(delivery, cancellationToken);
            result = await inner.WriteAsync(delivery.Recording, delivery.Capture, cancellationToken).ConfigureAwait(false);
            await completions.PublishAsync(delivery, cancellationToken).ConfigureAwait(false);
            delivery.Acknowledge();
        }
        if (capture.Capture.State == PcmRecordingState.Completed) _conversions?.Request();
        return result;
    }
}

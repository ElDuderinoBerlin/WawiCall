namespace WawiCallPro.Utilities.Persistence;

/// <summary>Confirms the WAV artifact while its protected delivery and file read lock are still owned.
/// A failed confirmation must leave the delivery unacknowledged; non-completed captures publish no artifact.</summary>
public interface IRecordingCompletionPublisher
{
    /// <summary>Checks PCM result metadata before recording/capture confirmation. Performs no database writes.</summary>
    void Validate(IRecordingJournalDelivery delivery, CancellationToken cancellationToken = default);
    Task PublishAsync(IRecordingJournalDelivery delivery, CancellationToken cancellationToken = default);
}

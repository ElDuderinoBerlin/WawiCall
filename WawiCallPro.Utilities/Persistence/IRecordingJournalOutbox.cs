using WawiCallPro.Database.Contracts;

namespace WawiCallPro.Utilities.Persistence;

public interface IRecordingJournalOutbox
{
    IReadOnlyList<Guid> GetPending();
    Task<IRecordingJournalDelivery> StageAsync(RecordingWrite recording, RecordingCaptureWrite capture, CancellationToken cancellationToken = default);
    Task<IRecordingJournalDelivery> OpenAsync(Guid recordingId, CancellationToken cancellationToken = default);
    /// <summary>Shares the publisher's exclusive recording lease; an existing completion must take precedence over audit.</summary>
    IRecordingArtifactAuditLease AcquireInspection(Guid recordingId);
}

public interface IRecordingArtifactAuditLease : IAsyncDisposable
{
    bool HasPending { get; }
}

/// <summary>Owns a per-entry lease and, for completed captures, a read lock on the verified WAV.
/// Acknowledge only after database confirmation. Disposal alone retains the entry.</summary>
public interface IRecordingJournalDelivery : IAsyncDisposable
{
    RecordingWrite Recording { get; }
    RecordingCaptureWrite Capture { get; }
    Stream? Wave { get; }
    /// <summary>Original protected SHA-256 for a completed WAV; null for failure/abort deliveries.</summary>
    string? WaveSha256 { get; }
    void Acknowledge();
}

public sealed class RecordingOutboxException() : Exception("Das geschützte Aufnahmejournal ist belegt, beschädigt oder gehört zu einer anderen Datenbank. Es wurde nicht verworfen.");

using WawiCallPro.Database.Contracts;

namespace WawiCallPro.Utilities.Persistence;

public interface IRecordingConversionOutbox
{
    IReadOnlyList<Guid> GetPending();
    bool HasPending(Guid jobId);
    /// <summary>Owns the job lease while no completion is pending. Use through enqueue/requeue/claim.</summary>
    IAsyncDisposable AcquireEmpty(Guid jobId);
    /// <summary>Durably records the completed pair before publication, while the original output is exclusively open.</summary>
    void StagePrepared(RecordingArtifactWrite target, RecordingConversionWrite conversion, string temporaryRelativePath,
        FileStream original, CancellationToken cancellationToken = default);
    Task<IRecordingConversionDelivery> StageAsync(RecordingArtifactWrite target, RecordingConversionWrite conversion, CancellationToken cancellationToken = default);
    Task<IRecordingConversionDelivery> OpenAsync(Guid jobId, CancellationToken cancellationToken = default);
    /// <summary>May publish a prepared original only for the caller's current, locally owned running attempt.</summary>
    Task<IRecordingConversionDelivery> ResumeAsync(StoredRecordingArtifact target, StoredRecordingConversion conversion, CancellationToken cancellationToken = default);
}

/// <summary>Owns the job delivery lease and verified MP3 read lock. Caller also holds the recording lease.
/// Acknowledge only after both metadata writes are confirmed; disposal retains the pending entry.</summary>
public interface IRecordingConversionDelivery : IAsyncDisposable
{
    RecordingArtifactWrite Target { get; }
    RecordingConversionWrite Conversion { get; }
    void Acknowledge();
}

public sealed class RecordingConversionOutboxException() : Exception("Der geschützte MP3-Abschluss konnte nicht bestätigt werden. Auftrag und Dateien bleiben erhalten.");

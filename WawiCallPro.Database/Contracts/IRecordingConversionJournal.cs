namespace WawiCallPro.Database.Contracts;

public sealed record StoredRecordingConversionJournal(StoredRecordingArtifact Target, StoredRecordingConversion Conversion);

/// <summary>Atomic MP3 artifact/job metadata writes. No file access, encoding or automatic owner recovery.</summary>
public interface IRecordingConversionJournal
{
    /// <summary>Inserts a new Pending MP3 target and Pending job together. Exact replay of the last committed
    /// pair is idempotent; an independently registered target or job is never silently adopted.</summary>
    Task<StoredRecordingConversionJournal> BeginAsync(RecordingArtifactWrite target, RecordingConversionWrite conversion, CancellationToken cancellationToken = default);
    /// <summary>Confirms completion, failure or requeue together. Both current versions must match, or both
    /// must be an exact replay of the previous pair. The caller must validate actual MP3 bytes before completion.</summary>
    Task<StoredRecordingConversionJournal> WriteAsync(RecordingArtifactWrite target, RecordingConversionWrite conversion, CancellationToken cancellationToken = default);
}

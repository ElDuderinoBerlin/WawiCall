using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Interfaces.Common;

namespace WawiCallPro.Database.Contracts;

public enum RecordingConversionState { Pending = 0, Running = 1, Completed = 2, Failed = 3 }

/// <summary>Stable, display-safe diagnostics. No native exception text or absolute paths are persisted.</summary>
public enum RecordingConversionFailure
{
    EncoderUnavailable = 1, UnsupportedFormat, InvalidWave, SourceUnavailable, DestinationUnavailable,
    EncodingFailed, CleanupFailed, Cancelled, Interrupted
}

/// <summary>One durable conversion job for a WAV and a canonical compressed target. Does not access files or run an encoder.</summary>
public sealed record RecordingConversionJob
{
    public RecordingConversionJob(Guid id, Guid sourceArtifactId, Guid targetArtifactId, Mp3EncodingFormat format,
        RecordingConversionState state, int attempts = 0, Guid? attemptId = null, RecordingConversionFailure? failure = null)
    {
        _ = DatabaseId.Encode(id); _ = DatabaseId.Encode(sourceArtifactId); _ = DatabaseId.Encode(targetArtifactId);
        if (sourceArtifactId == targetArtifactId) throw new ArgumentException("Conversion source and target must differ.");
        ArgumentNullException.ThrowIfNull(format);
        if (format.SampleRate is not (8000 or 16000 or 32000 or 44100 or 48000) || format.Channels is not (1 or 2) ||
            format.BitRate is < 8000 or > 320000 || format.BitRate % 1000 != 0) throw new ArgumentOutOfRangeException(nameof(format));
        if (!Enum.IsDefined(state) || failure is { } code && !Enum.IsDefined(code)) throw new ArgumentOutOfRangeException(nameof(state));
        if (attemptId is { } attempt) _ = DatabaseId.Encode(attempt);
        if (attempts < 0 || state != RecordingConversionState.Pending && (attempts == 0 || attemptId is null) ||
            state == RecordingConversionState.Pending && attemptId is not null ||
            (state == RecordingConversionState.Failed) != (failure is not null)) throw new ArgumentException("Inconsistent conversion state.");
        Id = id; SourceArtifactId = sourceArtifactId; TargetArtifactId = targetArtifactId; Format = format;
        State = state; Attempts = attempts; AttemptId = attemptId; Failure = failure;
    }
    public Guid Id { get; }
    public Guid SourceArtifactId { get; }
    public Guid TargetArtifactId { get; }
    public Mp3EncodingFormat Format { get; }
    public RecordingConversionState State { get; }
    public int Attempts { get; }
    public Guid? AttemptId { get; }
    public RecordingConversionFailure? Failure { get; }
    public override string ToString() => nameof(RecordingConversionJob);
}

public sealed record RecordingConversionWrite
{
    public RecordingConversionWrite(RecordingConversionJob job, long? expectedVersion, DateTimeOffset changedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(job); StorageContract.ExpectedVersion(expectedVersion); StorageContract.Utc(changedAtUtc);
        Job = job; ExpectedVersion = expectedVersion; ChangedAtUtc = changedAtUtc;
    }
    public RecordingConversionJob Job { get; }
    public long? ExpectedVersion { get; }
    public DateTimeOffset ChangedAtUtc { get; }
    public override string ToString() => nameof(RecordingConversionWrite);
}

public sealed record StoredRecordingConversion(RecordingConversionJob Job, long Version, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);

/// <summary>Metadata only, with CAS and exact replay. A running attempt must be explicitly failed as Interrupted
/// before requeueing after a proven owner shutdown. Recovery must acquire the recording lease and defer
/// protected completions; reading alone never resets another worker's job.</summary>
public interface IRecordingConversionRepository
{
    Task<StoredRecordingConversion?> GetAsync(Guid jobId, CancellationToken cancellationToken = default);
    /// <summary>Looks up the MP3 job for a WAV, regardless of other targets.</summary>
    Task<StoredRecordingConversion?> GetForSourceAsync(Guid sourceArtifactId, CancellationToken cancellationToken = default);
    Task<StoredRecordingConversion?> GetForTargetAsync(Guid targetArtifactId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
    Task<Page<StoredRecordingConversion>> GetPageAsync(Guid workstationId, Guid? afterJobId = null, int limit = 100, CancellationToken cancellationToken = default);
    /// <summary>Requires existing canonical artifacts of the same completed capture. Initial state is Pending,
    /// each Running transition owns a fresh attempt ID. Completion requires an already confirmed MP3 artifact.</summary>
    Task<StoredRecordingConversion> WriteAsync(RecordingConversionWrite write, CancellationToken cancellationToken = default);
}

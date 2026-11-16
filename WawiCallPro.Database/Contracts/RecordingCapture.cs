using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Database.Contracts;

/// <summary>Stable persisted error codes; never store native exception messages or file paths as diagnostics.</summary>
public enum RecordingCaptureFailure
{
    InvalidBlock = 1, BufferOverflow, TimelineConflict, SegmentLimit, FileFailure, MixingFailed,
    Cancelled, Interrupted, StartupFailed
}

/// <summary>Capture metadata, separate from call duration and file availability. An unknown end stays null.</summary>
public sealed record RecordingCapture
{
    public RecordingCapture(Guid recordingId, Guid callId, DateTimeOffset startedAtUtc, DateTimeOffset? endedAtUtc,
        PcmRecordingState state, int sampleRate, int channels, long? frameCount = null, RecordingCaptureFailure? failure = null)
    {
        _ = DatabaseId.Encode(recordingId); _ = DatabaseId.Encode(callId); StorageContract.Utc(startedAtUtc);
        if (endedAtUtc is { } end) { StorageContract.Utc(end); if (end < startedAtUtc) throw new ArgumentException("Capture end precedes its start."); }
        if (!Enum.IsDefined(state) || failure is { } code && !Enum.IsDefined(code)) throw new ArgumentOutOfRangeException(nameof(state));
        if (sampleRate is not (8000 or 16000 or 32000 or 44100 or 48000) || channels is not (1 or 2)) throw new ArgumentOutOfRangeException(nameof(sampleRate));
        var valid = state switch
        {
            PcmRecordingState.Capturing or PcmRecordingState.Stopping => endedAtUtc is null && frameCount is null && failure is null,
            PcmRecordingState.Completed => endedAtUtc is not null && frameCount is > 0 && failure is null,
            _ => frameCount is null && failure is not null
        };
        if (!valid || frameCount is { } frames && (frames > sampleRate * 28800L || frames > (int.MaxValue - 44L) / (channels * 2)))
            throw new ArgumentException("Capture state and result metadata are inconsistent.");
        RecordingId = recordingId; CallId = callId; StartedAtUtc = startedAtUtc; EndedAtUtc = endedAtUtc;
        State = state; SampleRate = sampleRate; Channels = channels; FrameCount = frameCount; Failure = failure;
    }
    public Guid RecordingId { get; }
    public Guid CallId { get; }
    public DateTimeOffset StartedAtUtc { get; }
    public DateTimeOffset? EndedAtUtc { get; }
    public PcmRecordingState State { get; }
    public int SampleRate { get; }
    public int Channels { get; }
    public long? FrameCount { get; }
    public RecordingCaptureFailure? Failure { get; }
    public TimeSpan? Duration => FrameCount is { } frames ? TimeSpan.FromTicks((frames * TimeSpan.TicksPerSecond + SampleRate / 2) / SampleRate) : null;
    public override string ToString() => nameof(RecordingCapture);
}

public sealed record RecordingCaptureWrite
{
    public RecordingCaptureWrite(RecordingCapture capture, long? expectedVersion, DateTimeOffset changedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(capture); StorageContract.ExpectedVersion(expectedVersion); StorageContract.Utc(changedAtUtc);
        if (changedAtUtc < capture.StartedAtUtc || capture.EndedAtUtc > changedAtUtc) throw new ArgumentException("Capture change precedes its media times.");
        Capture = capture; ExpectedVersion = expectedVersion; ChangedAtUtc = changedAtUtc;
    }
    public RecordingCapture Capture { get; }
    public long? ExpectedVersion { get; }
    public DateTimeOffset ChangedAtUtc { get; }
    public override string ToString() => nameof(RecordingCaptureWrite);
}

public sealed record StoredRecordingCapture(RecordingCapture Capture, long Version, DateTimeOffset UpdatedAtUtc);

public interface IRecordingCaptureRepository
{
    Task<StoredRecordingCapture?> GetAsync(Guid recordingId, CancellationToken cancellationToken = default);
    /// <summary>Compare-and-swap lifecycle write; exact retries of the immediately preceding write are idempotent.
    /// Does not create files or change recording availability. Capture identity/format/start are immutable.</summary>
    Task<StoredRecordingCapture> WriteAsync(RecordingCaptureWrite write, CancellationToken cancellationToken = default);
    /// <summary>Marks only unfinished captures of an already ended call interrupted. Never invents a media end/duration.
    /// Repeating recovery changes no terminal captures. Live calls are rejected; unselected calls stay untouched.</summary>
    Task<int> RecoverEndedCallAsync(Guid callId, DateTimeOffset recoveredAtUtc, CancellationToken cancellationToken = default);
}

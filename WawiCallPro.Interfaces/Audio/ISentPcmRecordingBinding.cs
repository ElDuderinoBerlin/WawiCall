namespace WawiCallPro.Interfaces.Audio;

/// <summary>Caller-provided mapping of a local RTP timestamp to the shared recording-relative timeline.
/// A negative offset permits recording to begin inside a source block. No arrival-time calibration is implied.</summary>
public sealed record SentPcmTimelineAnchor(uint SynchronizationSource, uint Timestamp, int RtpClockRate, TimeSpan RecordingOffset)
{
    public override string ToString() => nameof(SentPcmTimelineAnchor);
}

public enum SentPcmRecordingFailure
{
    InvalidPcm = 1, MetadataUnavailable, SourceChanged, ClockChanged, TimestampDiscontinuity,
    Overlap, ConcurrentInput, RecordingRejected, ObservationFailed
}

public interface ISentPcmRecordingBindingFactory
{
    /// <summary>Explicitly attaches local RTP-timed source PCM to a borrowed capturing session with an input guard.
    /// The caller supplies the anchor; no sequence numbers, remote synchronization or recording lifetime are inferred.</summary>
    ISentPcmRecordingBinding Attach(IConversationPcmFailureSource source, IPcmRecordingSession session, SentPcmTimelineAnchor anchor);
}

/// <summary>Owns one source subscription. Await disposal/Completion before stopping the borrowed recording session.
/// Normal source closure completes this binding after its in-flight callbacks. It never stops/disposes the source or session.</summary>
public interface ISentPcmRecordingBinding : IAsyncDisposable
{
    SentPcmRecordingFailure? Failure { get; }
    TimeSpan LastWindowEnd { get; }
    long TrimmedSamples { get; }
    Task<SentPcmRecordingFailure?> Completion { get; }
}

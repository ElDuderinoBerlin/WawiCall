namespace WawiCallPro.Interfaces.Audio;

/// <summary>Explicit mapping of an expected RTP packet to a recording-relative position.
/// A negative offset allows the recording to begin inside that packet. This is not an arrival-time calibration.</summary>
public sealed record RtpPcmTimelineAnchor(uint SynchronizationSource, ushort SequenceNumber, uint Timestamp,
    int RtpClockRate, TimeSpan RecordingOffset)
{
    public override string ToString() => nameof(RtpPcmTimelineAnchor);
}

public readonly record struct RtpPcmPacketPosition(uint SynchronizationSource, ushort SequenceNumber, uint Timestamp, int RtpClockRate);
public enum RtpPcmAppendResult { Accepted, Duplicate, Late, BeforeRecording, Closed, Failed }
public enum RtpPcmFeedFailure { InvalidPacket = 1, SourceChanged, ClockChanged, SequenceConflict, TimestampDiscontinuity, Overlap, ConcurrentInput, RecordingRejected }

/// <summary>Optional input-failure boundary. Metadata failures must also prevent a truncated recording from succeeding.</summary>
public interface IPcmRecordingInputGuard
{
    void RejectInput(PcmRecordingFailure failure);
}

public interface IRtpPcmRecordingFeedFactory
{
    /// <summary>Creates one owned feed per side, borrowing a capturing session with an input guard.
    /// Dispose/drain feeds before stopping the session. Caller provides already ordered, decoded mono PCM.</summary>
    IRtpPcmRecordingFeed Create(IPcmRecordingSession session, ConversationAudioSide side, RtpPcmTimelineAnchor anchor);
}

/// <summary>Validates sequence/time metadata, preserves timestamp gaps and splits PCM into at most 100-ms chunks.
/// It does not decode, reorder encoded packets, infer an anchor or own/stop the recording session.</summary>
public interface IRtpPcmRecordingFeed : IAsyncDisposable
{
    RtpPcmFeedFailure? Failure { get; }
    long DuplicatePackets { get; }
    long LatePackets { get; }
    long TrimmedSamples { get; }
    TimeSpan LastPacketEnd { get; }
    RtpPcmAppendResult TryAppend(RtpPcmPacketPosition packet, int pcmSampleRate, ReadOnlySpan<short> samples);
}

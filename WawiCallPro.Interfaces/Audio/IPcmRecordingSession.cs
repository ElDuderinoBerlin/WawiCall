using System.Collections.Immutable;

namespace WawiCallPro.Interfaces.Audio;

/// <summary>Explicit recording only. Segment files are retained in a unique sibling directory, including after failure.</summary>
public sealed record PcmRecordingRequest(Guid CallId, Guid RecordingId, string WavePath, int SampleRate = 16000,
    ConversationWaveLayout Layout = ConversationWaveLayout.SeparatedStereo, int QueueCapacity = 64)
{
    public override string ToString() => nameof(PcmRecordingRequest);
}

public enum PcmRecordingState { Capturing, Stopping, Completed, Failed, Aborted }
public enum PcmRecordingFailure { InvalidBlock = 1, BufferOverflow, TimelineConflict, SegmentLimit, FileFailure, MixingFailed }

public sealed record PcmRecordingResult(ConversationWaveMixResult Wave, ImmutableArray<ConversationWaveSegment> Segments)
{
    public override string ToString() => nameof(PcmRecordingResult);
}

public interface IPcmRecordingSessionFactory
{
    /// <summary>Creates one owned session on a worker. The token controls the entire session lifetime, not just startup.</summary>
    Task<IPcmRecordingSession> StartAsync(PcmRecordingRequest request, CancellationToken cancellationToken = default);
}

/// <summary>The caller must stop or asynchronously dispose every session. No microphone or SIP source is opened here.</summary>
public interface IPcmRecordingSession : IAsyncDisposable
{
    PcmRecordingState State { get; }
    PcmRecordingFailure? Failure { get; }
    string SegmentDirectory { get; }
    /// <summary>Ends when capture has drained and source files are finalized, or faults/cancels on capture failure.</summary>
    Task CaptureCompletion { get; }

    /// <summary>Copies a mono PCM16 block of at most 100 ms and tries to enqueue it without waiting or performing IO.
    /// Start uses a shared recording-relative media timeline, never independent packet arrival timestamps.
    /// Blocks on each side must arrive in timeline order. Rejection while capturing fails the recording explicitly.</summary>
    bool TryAppend(ConversationAudioSide side, TimeSpan start, int sampleRate, ReadOnlySpan<short> samples);

    /// <summary>Closes input, drains, finalizes and mixes. First invocation's duration/token win; subsequent calls share its task.
    /// Duration is relative to recording start. A canceled stop preserves source files and publishes no new mix before its commit.</summary>
    Task<PcmRecordingResult> StopAsync(TimeSpan duration, CancellationToken cancellationToken = default);
}

/// <summary>Safe diagnostic; file paths, PCM contents and native exceptions are not attached.</summary>
public sealed class PcmRecordingException(PcmRecordingFailure failure) : Exception(failure switch
{
    PcmRecordingFailure.InvalidBlock => "Die Aufnahme hat einen ungültigen PCM-Audioblock erhalten.",
    PcmRecordingFailure.BufferOverflow => "Die Aufnahme wurde wegen eines überfüllten Audiopuffers beendet.",
    PcmRecordingFailure.TimelineConflict => "Die Zeitpositionen der Aufnahme sind widersprüchlich oder überlappen.",
    PcmRecordingFailure.SegmentLimit => "Die Aufnahme überschreitet die unterstützte Anzahl von Audiosegmenten.",
    PcmRecordingFailure.MixingFailed => "Die Aufnahme konnte nicht gemischt werden. Vorhandene Quellsegmente bleiben erhalten.",
    _ => "Eine Aufnahmedatei konnte nicht verarbeitet werden. Vorhandene Quellsegmente bleiben erhalten."
})
{
    public PcmRecordingFailure Failure { get; } = failure;
}

namespace WawiCallPro.Interfaces.Audio;

public enum RecordingTimelineBasis { CallerSamplingReferences, LocalCodecObservation }

/// <summary>Both anchors refer to one explicit recording epoch. The basis distinguishes verified caller references
/// from first local codec observations; the latter does not claim synchronization of remote sampling clocks.</summary>
public sealed record DuplexPcmRecordingRequest(PcmRecordingRequest Recording, SentPcmTimelineAnchor Sent, RtpPcmTimelineAnchor Received)
{
    /// <summary>Optional for standalone audio. Persisted recording requires the explicit UTC instant at zero
    /// on the declared timeline basis. LocalCodecObservation correlates the local monotonic clock only;
    /// CallerSamplingReferences requires the caller's verified sampling references, not report arrival.</summary>
    public DateTimeOffset? RecordingEpochUtc { get; init; }
    public RecordingTimelineBasis TimelineBasis { get; init; } = RecordingTimelineBasis.CallerSamplingReferences;
    public override string ToString() => nameof(DuplexPcmRecordingRequest);
}

public interface IDuplexPcmRecordingFactory
{
    /// <summary>Explicitly creates an owned recording and both bindings, using one borrowed source subscription.
    /// The token controls the entire capture lifetime. No files are created before argument validation.</summary>
    Task<IDuplexPcmRecording> StartAsync(IConversationPcmFailureSource source, DuplexPcmRecordingRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Owns the recording and both bindings; the source remains borrowed.
/// Dispose without Stop aborts without publishing a mix. Dispose during Stop awaits that Stop.</summary>
public interface IDuplexPcmRecording : IAsyncDisposable
{
    PcmRecordingState State { get; }
    PcmRecordingFailure? Failure { get; }
    /// <summary>Completes after source callbacks and both bindings have drained. Does not finalize the file.
    /// Normal source closure still requires explicit Stop with a known recording-relative duration.</summary>
    Task InputCompletion { get; }
    /// <summary>Retires/drains the source subscription and bindings, then finalizes the recording.
    /// First valid invocation's duration/token win. Calls share one task. Invalid duration leaves capture active.</summary>
    Task<PcmRecordingResult> StopAsync(TimeSpan duration, CancellationToken cancellationToken = default);
}

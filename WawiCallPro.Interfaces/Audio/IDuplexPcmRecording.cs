namespace WawiCallPro.Interfaces.Audio;

/// <summary>Both anchors must refer to the same caller-established recording epoch. No automatic calibration is implied.</summary>
public sealed record DuplexPcmRecordingRequest(PcmRecordingRequest Recording, SentPcmTimelineAnchor Sent, RtpPcmTimelineAnchor Received)
{
    /// <summary>Optional for standalone audio. Persisted call recording requires the caller-verified UTC instant
    /// corresponding to zero on the shared recording timeline, not call start or packet arrival.</summary>
    public DateTimeOffset? RecordingEpochUtc { get; init; }
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

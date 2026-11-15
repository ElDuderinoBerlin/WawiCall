namespace WawiCallPro.Interfaces.Audio;

public enum ReceivedPcmRecordingFailure { InvalidPcm = 1, MetadataUnavailable, ObservationFailed, RecordingRejected }

public interface IReceivedPcmRecordingBindingFactory
{
    /// <summary>Explicitly attaches already ordered receive PCM to a borrowed capturing session.
    /// The caller supplies the RTP-to-recording anchor; no arrival-time calibration or send mapping is inferred.</summary>
    Task<IReceivedPcmRecordingBinding> AttachAsync(IConversationPcmFailureSource source,
        IPcmRecordingSession session, RtpPcmTimelineAnchor anchor);
}

/// <summary>Owns the subscription and receive feed, borrowing the source and recording session.
/// Await disposal/Completion before stopping the session. Media must finish its queued decode before source closure.
/// This binding neither starts/stops the recording nor owns devices, transports or the recording session.</summary>
public interface IReceivedPcmRecordingBinding : IAsyncDisposable
{
    ReceivedPcmRecordingFailure? Failure { get; }
    TimeSpan LastPacketEnd { get; }
    /// <summary>Ends after callbacks and the receive feed drain, including on normal source closure.
    /// A fixed failure is also applied synchronously to the recording input guard.</summary>
    Task<ReceivedPcmRecordingFailure?> Completion { get; }
}

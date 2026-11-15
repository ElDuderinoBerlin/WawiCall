namespace WawiCallPro.Interfaces.Audio;

public enum PcmObservationFailure { InvalidFormat = 1, ObserverRejected, ObserverFailed, ReceivedMetadataUnavailable }

/// <summary>Codec-boundary PCM, not a media timeline. Copy synchronously if buffers must outlive this call.
/// Implementations must not wait or perform IO. False retires this observer and reports ObserverRejected.</summary>
public interface IConversationPcmObserver
{
    bool TryObserve(ConversationAudioSide side, int sampleRate, int channels, ReadOnlySpan<short> samples);
}

/// <summary>Optional receive-packet capability on the existing subscription. Sent PCM still uses TryObserve.
/// Received PCM requires the original RTP header; absence retires the observer instead of inventing media time.
/// This does not establish a common send/receive epoch or guarantee packet ordering before the decoder.</summary>
public interface IReceivedRtpPcmObserver : IConversationPcmObserver
{
    bool TryObserveReceived(RtpPcmPacketPosition packet, int sampleRate, int channels, ReadOnlySpan<short> samples);
}

/// <summary>Disposal retires the observer immediately and awaits callbacks already in progress.
/// Completion reports rejection/failure, or null after normal retirement; no exception escapes into audio processing.</summary>
public interface IConversationPcmSubscription : IAsyncDisposable
{
    Task<PcmObservationFailure?> Completion { get; }
}

/// <summary>Optional passive observation of PCM already processed by the live codec.
/// Basic observers have no timestamps; IReceivedRtpPcmObserver can require correlated receive headers.
/// No recording, common media epoch, packet reordering or wall-clock/arrival-time substitution is implied.</summary>
public interface IConversationPcmSource
{
    long FailureCount { get; }
    PcmObservationFailure? LastFailure { get; }
    IConversationPcmSubscription Subscribe(IConversationPcmObserver observer);
}

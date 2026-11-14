namespace WawiCallPro.Interfaces.Audio;

public enum PcmObservationFailure { InvalidFormat = 1, ObserverRejected, ObserverFailed }

/// <summary>Codec-boundary PCM, not a media timeline. Copy synchronously if buffers must outlive this call.
/// Implementations must not wait or perform IO. False retires this observer and reports ObserverRejected.</summary>
public interface IConversationPcmObserver
{
    bool TryObserve(ConversationAudioSide side, int sampleRate, int channels, ReadOnlySpan<short> samples);
}

/// <summary>Disposal retires the observer immediately and awaits callbacks already in progress.
/// Completion reports rejection/failure, or null after normal retirement; no exception escapes into audio processing.</summary>
public interface IConversationPcmSubscription : IAsyncDisposable
{
    Task<PcmObservationFailure?> Completion { get; }
}

/// <summary>Optional passive observation of PCM already processed by the live codec.
/// No recording, RTP timestamps, packet reordering or wall-clock/arrival-time substitution is implied.</summary>
public interface IConversationPcmSource
{
    long FailureCount { get; }
    PcmObservationFailure? LastFailure { get; }
    IConversationPcmSubscription Subscribe(IConversationPcmObserver observer);
}

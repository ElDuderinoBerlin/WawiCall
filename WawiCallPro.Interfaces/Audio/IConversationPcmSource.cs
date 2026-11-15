namespace WawiCallPro.Interfaces.Audio;

public enum PcmObservationFailure { InvalidFormat = 1, ObserverRejected, ObserverFailed, ReceivedMetadataUnavailable, SentMetadataUnavailable }

/// <summary>Codec-boundary PCM, not a media timeline. Copy synchronously if buffers must outlive this call.
/// Implementations must not wait or perform IO. False retires this observer and reports ObserverRejected.</summary>
public interface IConversationPcmObserver
{
    bool TryObserve(ConversationAudioSide side, int sampleRate, int channels, ReadOnlySpan<short> samples);
}

/// <summary>Optional receive-packet capability on the existing subscription. Sent PCM uses TryObserve unless
/// the observer additionally opts into ISentRtpPcmObserver.
/// Received PCM requires the original RTP header; absence retires the observer instead of inventing media time.
/// This does not establish a common send/receive epoch or guarantee packet ordering before the decoder.</summary>
public interface IReceivedRtpPcmObserver : IConversationPcmObserver
{
    bool TryObserveReceived(RtpPcmPacketPosition packet, int sampleRate, int channels, ReadOnlySpan<short> samples);
}

/// <summary>The local RTP window assigned to one encoded source block; not a delivery acknowledgement.
/// Sequence numbers/packet boundaries are not inferred: one block may be fragmented into several packets.</summary>
public readonly record struct SentRtpPcmPosition(uint SynchronizationSource, uint Timestamp, int RtpClockRate, uint DurationRtpUnits);

/// <summary>Optional microphone/source PCM with a correlated local RTP send window.
/// Receive PCM still uses the existing receive/base callbacks. No shared send/receive epoch is implied.</summary>
public interface ISentRtpPcmObserver : IConversationPcmObserver
{
    bool TryObserveSent(SentRtpPcmPosition position, int sampleRate, int channels, ReadOnlySpan<short> samples);
}

/// <summary>Optional synchronous failure boundary. Do not wait, perform IO or throw from this callback.
/// A failure may arrive while a PCM callback is still draining; implementations must be thread-safe.</summary>
public interface IConversationPcmFailureObserver
{
    void OnPcmObservationFailed(PcmObservationFailure failure);
}

/// <summary>Promises to notify failure-aware observers before the failing media call returns and before
/// subscription Completion. This allows a recording to reject input before a caller can stop/publish it.</summary>
public interface IConversationPcmFailureSource : IConversationPcmSource { }

/// <summary>Disposal retires the observer immediately and awaits callbacks already in progress.
/// Completion reports rejection/failure, or null after normal retirement; no exception escapes into audio processing.</summary>
public interface IConversationPcmSubscription : IAsyncDisposable
{
    Task<PcmObservationFailure?> Completion { get; }
}

/// <summary>Optional passive observation of PCM already processed by the live codec.
/// Basic observers have no timestamps; optional observers can require receive headers or local send windows.
/// No recording, common media epoch, packet reordering or wall-clock/arrival-time substitution is implied.</summary>
public interface IConversationPcmSource
{
    long FailureCount { get; }
    PcmObservationFailure? LastFailure { get; }
    IConversationPcmSubscription Subscribe(IConversationPcmObserver observer);
}

using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Core.Audio;

/// <summary>Both private binding subscriptions are prepared before this single observer is attached to live PCM.
/// The owner drains that live subscription before completing/disposing either private subscription.</summary>
internal sealed class DuplexPcmRecordingSource(IPcmRecordingInputGuard guard) : IReceivedRtpPcmObserver, ISentRtpPcmObserver, IConversationPcmFailureObserver
{
    internal ForwardedSource Sent { get; } = new();
    internal ForwardedSource Received { get; } = new();
    public bool TryObserve(ConversationAudioSide side, int rate, int channels, ReadOnlySpan<short> samples)
    {
        OnPcmObservationFailed(side == ConversationAudioSide.Sent ? PcmObservationFailure.SentMetadataUnavailable : PcmObservationFailure.ReceivedMetadataUnavailable);
        return false;
    }
    public bool TryObserveSent(SentRtpPcmPosition position, int rate, int channels, ReadOnlySpan<short> samples) =>
        Sent.Observer is ISentRtpPcmObserver observer && observer.TryObserveSent(position, rate, channels, samples);
    public bool TryObserveReceived(RtpPcmPacketPosition position, int rate, int channels, ReadOnlySpan<short> samples) =>
        Received.Observer is IReceivedRtpPcmObserver observer && observer.TryObserveReceived(position, rate, channels, samples);
    public void OnPcmObservationFailed(PcmObservationFailure failure)
    {
        try { guard.RejectInput(failure == PcmObservationFailure.InvalidFormat ? PcmRecordingFailure.InvalidBlock : PcmRecordingFailure.TimelineConflict); }
        catch (Exception) { }
        Sent.Notify(failure); Received.Notify(failure);
    }
    internal void Complete(PcmObservationFailure? failure) { Sent.Complete(failure); Received.Complete(failure); }

    internal sealed class ForwardedSource : IConversationPcmFailureSource, IConversationPcmSubscription
    {
        private IConversationPcmObserver? _observer;
        private readonly TaskCompletionSource<PcmObservationFailure?> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal IConversationPcmObserver? Observer => Volatile.Read(ref _observer);
        public long FailureCount => 0;
        public PcmObservationFailure? LastFailure => null;
        public Task<PcmObservationFailure?> Completion => _completion.Task;
        public IConversationPcmSubscription Subscribe(IConversationPcmObserver observer)
        {
            ArgumentNullException.ThrowIfNull(observer);
            if (Interlocked.CompareExchange(ref _observer, observer, null) is not null)
                throw new InvalidOperationException("Diese interne Aufnahmequelle besitzt bereits einen Beobachter.");
            return this;
        }
        internal void Notify(PcmObservationFailure failure)
        {
            if (Observer is IConversationPcmFailureObserver observer)
            {
                try { observer.OnPcmObservationFailed(failure); }
                catch (Exception) { }
            }
        }
        internal void Complete(PcmObservationFailure? failure)
        {
            if (failure is { } reason) Notify(reason);
            Interlocked.Exchange(ref _observer, null);
            _completion.TrySetResult(failure);
        }
        // Only the owner disposes child bindings, after their live subscription has fully drained.
        public ValueTask DisposeAsync() { Complete(null); return new(_completion.Task); }
    }
}

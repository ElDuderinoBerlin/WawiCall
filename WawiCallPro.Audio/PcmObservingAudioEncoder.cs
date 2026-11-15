using SIPSorceryMedia.Abstractions;
using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Audio;

/// <summary>Observes the exact encode input/decode output of an owned SIPSorcery codec.
/// Codec exceptions retain their normal behavior; observer failures are isolated and observable.</summary>
public sealed class PcmObservingAudioEncoder(IAudioEncoder inner) : IAudioEncoder, IConversationPcmFailureSource, IAsyncDisposable
{
    private readonly object _gate = new();
    private Subscription[] _subscriptions = [];
    private readonly HashSet<Subscription> _retiring = [];
    private Task? _dispose;
    private bool _closed;
    private long _failureCount;
    private int _lastFailure;
    private int _receivedMetadataUnavailable;
    private readonly AsyncLocal<ReceiveContext?> _receiveContext = new();

    public List<AudioFormat> SupportedFormats => inner.SupportedFormats;
    public long FailureCount => Interlocked.Read(ref _failureCount);
    public PcmObservationFailure? LastFailure
    {
        get { var failure = Volatile.Read(ref _lastFailure); return failure == 0 ? null : (PcmObservationFailure)failure; }
    }

    public byte[] EncodeAudio(short[] pcm, AudioFormat format)
    {
        var encoded = inner.EncodeAudio(pcm, format);
        Publish(ConversationAudioSide.Sent, format, pcm);
        return encoded;
    }

    public short[] DecodeAudio(byte[] encoded, AudioFormat format)
    {
        var context = _receiveContext.Value;
        RtpPcmPacketPosition? position = context is not null && context.TryConsume(encoded, format) ? context.Packet : null;
        try
        {
            var pcm = inner.DecodeAudio(encoded, format);
            if (context is not null && pcm.Length == 0) RejectReceivedMetadata();
            Publish(ConversationAudioSide.Received, format, pcm, position);
            return pcm;
        }
        catch
        {
            RejectReceivedMetadata();
            throw;
        }
    }

    internal void DeliverReceivedFrame(RtpPcmPacketPosition packet, EncodedAudioFrame frame, IAudioSink sink)
    {
        var previous = _receiveContext.Value;
        var context = new ReceiveContext(packet, frame.EncodedAudio, frame.AudioFormat);
        _receiveContext.Value = context;
        try
        {
            sink.GotEncodedMediaFrame(frame);
            if (!context.WasConsumed) RejectReceivedMetadata();
        }
        finally { context.Retire(); _receiveContext.Value = previous; }
    }

    internal void RejectReceivedMetadata()
    {
        Volatile.Write(ref _receivedMetadataUnavailable, 1);
        foreach (var subscription in Volatile.Read(ref _subscriptions))
        {
            if (!subscription.TryEnter(out var observer)) continue;
            try { if (observer is IReceivedRtpPcmObserver) Retire(subscription, PcmObservationFailure.ReceivedMetadataUnavailable); }
            finally { subscription.Exit(); }
        }
    }

    public IConversationPcmSubscription Subscribe(IConversationPcmObserver observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        Subscription subscription;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_closed, this);
            if (_subscriptions.Length + _retiring.Count >= 8) throw new InvalidOperationException("Zu viele PCM-Beobachter für diese Mediensitzung.");
            subscription = new Subscription(this, observer);
            Volatile.Write(ref _subscriptions, [.. _subscriptions, subscription]);
        }
        if (observer is IReceivedRtpPcmObserver && Volatile.Read(ref _receivedMetadataUnavailable) != 0)
            Retire(subscription, PcmObservationFailure.ReceivedMetadataUnavailable);
        return subscription;
    }

    private void Publish(ConversationAudioSide side, AudioFormat format, short[] pcm, RtpPcmPacketPosition? packet = null)
    {
        var subscriptions = Volatile.Read(ref _subscriptions);
        if (subscriptions.Length == 0 || pcm.Length == 0) return;
        var invalid = format.ClockRate is < 8000 or > 192000 || format.ChannelCount is not (1 or 2) || pcm.Length % format.ChannelCount != 0;
        foreach (var subscription in subscriptions)
        {
            if (!subscription.TryEnter(out var observer)) continue;
            try
            {
                if (invalid) Retire(subscription, PcmObservationFailure.InvalidFormat);
                else if (side == ConversationAudioSide.Received && observer is IReceivedRtpPcmObserver received)
                {
                    if (packet is null || Volatile.Read(ref _receivedMetadataUnavailable) != 0)
                        Retire(subscription, PcmObservationFailure.ReceivedMetadataUnavailable);
                    else if (!received.TryObserveReceived(packet.Value, format.ClockRate, format.ChannelCount, pcm))
                        Retire(subscription, PcmObservationFailure.ObserverRejected);
                }
                else if (!observer.TryObserve(side, format.ClockRate, format.ChannelCount, pcm))
                    Retire(subscription, PcmObservationFailure.ObserverRejected);
            }
            catch (Exception) { Retire(subscription, PcmObservationFailure.ObserverFailed); }
            finally { subscription.Exit(); }
        }
    }

    private void Retire(Subscription subscription, PcmObservationFailure? failure)
    {
        if (!subscription.Retire(failure)) return;
        if (failure is { } value)
        {
            Volatile.Write(ref _lastFailure, (int)value);
            Interlocked.Increment(ref _failureCount);
        }
        lock (_gate)
        {
            _retiring.Add(subscription);
            if (subscription.Completion.IsCompleted) _retiring.Remove(subscription);
            Volatile.Write(ref _subscriptions, _subscriptions.Where(item => !ReferenceEquals(item, subscription)).ToArray());
        }
    }

    private void OnCompleted(Subscription subscription) { lock (_gate) _retiring.Remove(subscription); }

    private sealed class ReceiveContext(RtpPcmPacketPosition packet, byte[] payload, AudioFormat format)
    {
        private int _active = 1, _remaining = 1;
        private byte[]? _payload = payload;
        internal RtpPcmPacketPosition Packet { get; } = packet;
        internal bool WasConsumed => Volatile.Read(ref _remaining) == 0;
        internal bool TryConsume(byte[] encoded, AudioFormat actual) => Volatile.Read(ref _active) == 1
            && ReferenceEquals(encoded, Volatile.Read(ref _payload)) && actual.FormatID == format.FormatID && actual.Codec == format.Codec
            && actual.ClockRate == format.ClockRate && actual.RtpClockRate == format.RtpClockRate && actual.ChannelCount == format.ChannelCount
            && Interlocked.Exchange(ref _remaining, 0) == 1;
        internal void Retire() { Volatile.Write(ref _active, 0); Interlocked.Exchange(ref _payload, null); }
    }

    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_dispose is not null) return new(_dispose);
            _closed = true;
            var subscriptions = _subscriptions.Concat(_retiring).Distinct().ToArray();
            Volatile.Write(ref _subscriptions, []);
            foreach (var subscription in subscriptions) subscription.Retire(null);
            _dispose = Task.WhenAll(subscriptions.Select(subscription => subscription.Completion));
            return new(_dispose);
        }
    }

    private sealed class Subscription(PcmObservingAudioEncoder owner, IConversationPcmObserver observer) : IConversationPcmSubscription
    {
        private IConversationPcmObserver? _observer = observer;
        private readonly TaskCompletionSource<PcmObservationFailure?> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        // 1 = active, -1 = retired normally, 100 + code = retired with a failure.
        private int _status = 1;
        private int _inflight;
        private int _retirements;
        public Task<PcmObservationFailure?> Completion => _completion.Task;

        internal bool TryEnter(out IConversationPcmObserver observer)
        {
            observer = null!;
            if (Volatile.Read(ref _status) != 1) return false;
            Interlocked.Increment(ref _inflight);
            if (Volatile.Read(ref _status) == 1) { observer = _observer!; return true; }
            Exit(); return false;
        }

        internal void Exit()
        {
            if (Interlocked.Decrement(ref _inflight) == 0 && Volatile.Read(ref _status) != 1) Complete();
        }

        internal bool Retire(PcmObservationFailure? failure)
        {
            Interlocked.Increment(ref _retirements);
            var observer = Volatile.Read(ref _observer);
            try
            {
                while (true)
                {
                    var status = Volatile.Read(ref _status);
                    if (status >= 100 || status == -1 && (failure is null || Volatile.Read(ref _inflight) == 0)) return false;
                    var retired = failure is { } value ? 100 + (int)value : -1;
                    if (Interlocked.CompareExchange(ref _status, retired, status) == status) break;
                }
                if (failure is { } reason && observer is IConversationPcmFailureObserver boundary)
                {
                    try { boundary.OnPcmObservationFailed(reason); }
                    catch (Exception) { /* A faulty boundary must not throw into media processing. */ }
                }
                return true;
            }
            finally
            {
                if (Interlocked.Decrement(ref _retirements) == 0 && Volatile.Read(ref _inflight) == 0 && Volatile.Read(ref _status) != 1)
                    Complete();
            }
        }

        private void Complete()
        {
            if (Volatile.Read(ref _retirements) != 0 || Volatile.Read(ref _inflight) != 0) return;
            Interlocked.Exchange(ref _observer, null);
            var status = Volatile.Read(ref _status);
            _completion.TrySetResult(status < 100 ? null : (PcmObservationFailure)(status - 100));
            owner.OnCompleted(this);
        }

        public ValueTask DisposeAsync()
        {
            owner.Retire(this, null);
            return new(_completion.Task);
        }
    }
}

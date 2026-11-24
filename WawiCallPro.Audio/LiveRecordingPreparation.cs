using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Audio;

/// <summary>Anchors each RTP stream at its first local codec observation after explicit start.
/// This is a local monitoring timeline, NOT remote sampling-clock synchronization or RTCP/NTP calibration.
/// Later spacing comes exclusively from real RTP counters; callback scheduling cannot compress gaps.</summary>
public sealed class LiveRecordingPreparation : IConversationPcmFailureSource, ISentRtpPcmObserver,
    IReceivedRtpPcmObserver, IConversationPcmFailureObserver, IAsyncDisposable
{
    internal static readonly TimeSpan MaximumInputLead = TimeSpan.FromMilliseconds(500);
    private readonly object _gate = new();
    private readonly TimeProvider _clock;
    private readonly long _origin;
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<PcmObservationFailure?> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Queue<Block> _pending = new();
    private readonly IConversationPcmSubscription _native;
    private IConversationPcmObserver? _observer;
    private SentPcmTimelineAnchor? _sent;
    private RtpPcmTimelineAnchor? _received;
    private bool _leased, _retired, _closed;
    private int _sampleCount;
    private Task? _dispose;
    public DateTimeOffset EpochUtc { get; }
    public Task Ready => _ready.Task;
    public TimeSpan GetElapsed() => _clock.GetElapsedTime(_origin, _clock.GetTimestamp());
    public async Task WaitForInputEndAsync(TimeSpan end)
    {
        var remaining = end - GetElapsed();
        if (remaining > MaximumInputLead) throw new PcmRecordingException(PcmRecordingFailure.TimelineConflict);
        if (remaining > TimeSpan.Zero) await Task.Delay(remaining + TimeSpan.FromMilliseconds(1), _clock).ConfigureAwait(false);
        if (end > GetElapsed()) throw new PcmRecordingException(PcmRecordingFailure.TimelineConflict);
    }
    public long FailureCount => LastFailure is null ? 0 : 1;
    public PcmObservationFailure? LastFailure { get; private set; }

    public LiveRecordingPreparation(IConversationPcmFailureSource source, TimeProvider clock)
    {
        _clock = clock;
        var before = clock.GetTimestamp(); EpochUtc = clock.GetUtcNow();
        _origin = before + (clock.GetTimestamp() - before) / 2;
        _native = source.Subscribe(this);
        _ = ObserveCompletionAsync();
    }

    public DuplexPcmRecordingRequest CreateRequest(PcmRecordingRequest recording)
    {
        lock (_gate)
        {
            if (_closed || LastFailure is not null || _sent is null || _received is null)
                throw new PcmRecordingException(PcmRecordingFailure.TimelineConflict);
            return new(recording, _sent, _received) { RecordingEpochUtc = EpochUtc, TimelineBasis = RecordingTimelineBasis.LocalCodecObservation };
        }
    }

    public bool TryObserveSent(SentRtpPcmPosition position, int rate, int channels, ReadOnlySpan<short> samples)
    {
        lock (_gate)
        {
            _sent ??= new(position.SynchronizationSource, position.Timestamp, position.RtpClockRate, Offset());
            return Observe(ConversationAudioSide.Sent, rate, channels, samples, position, null);
        }
    }
    public bool TryObserveReceived(RtpPcmPacketPosition position, int rate, int channels, ReadOnlySpan<short> samples)
    {
        lock (_gate)
        {
            _received ??= new(position.SynchronizationSource, position.SequenceNumber, position.Timestamp, position.RtpClockRate, Offset());
            return Observe(ConversationAudioSide.Received, rate, channels, samples, null, position);
        }
    }
    private TimeSpan Offset() => _clock.GetElapsedTime(_origin, _clock.GetTimestamp());
    public bool TryObserve(ConversationAudioSide side, int rate, int channels, ReadOnlySpan<short> samples)
    { OnPcmObservationFailed(side == ConversationAudioSide.Sent ? PcmObservationFailure.SentMetadataUnavailable : PcmObservationFailure.ReceivedMetadataUnavailable); return false; }

    private bool Observe(ConversationAudioSide side, int rate, int channels, ReadOnlySpan<short> samples,
        SentRtpPcmPosition? sent, RtpPcmPacketPosition? received)
    {
        if (_closed || _retired) return true;
        if (LastFailure is not null) return false;
        if (rate is < 8000 or > 192000 || channels != 1 || samples.IsEmpty || samples.Length > rate * 2)
        { Fail(PcmObservationFailure.InvalidFormat); return false; }
        if (_observer is { } observer)
        {
            try
            {
                if (!Forward(observer, side, rate, channels, samples, sent, received)) { Fail(PcmObservationFailure.ObserverRejected); return false; }
            }
            catch { Fail(PcmObservationFailure.ObserverFailed); return false; }
        }
        else
        {
            if (_pending.Count >= 128 || _sampleCount + samples.Length > 524288)
            { Fail(PcmObservationFailure.ObserverRejected); return false; }
            _pending.Enqueue(new(side, rate, channels, samples.ToArray(), sent, received));
            _sampleCount += samples.Length;
        }
        if (_sent is not null && _received is not null) _ready.TrySetResult();
        return true;
    }
    private static bool Forward(IConversationPcmObserver observer, ConversationAudioSide side, int rate, int channels,
        ReadOnlySpan<short> samples, SentRtpPcmPosition? sent, RtpPcmPacketPosition? received) =>
        sent is { } s && observer is ISentRtpPcmObserver send ? send.TryObserveSent(s, rate, channels, samples)
        : received is { } r && observer is IReceivedRtpPcmObserver receive ? receive.TryObserveReceived(r, rate, channels, samples)
        : observer.TryObserve(side, rate, channels, samples);

    public IConversationPcmSubscription Subscribe(IConversationPcmObserver observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_closed, this);
            if (_leased) throw new InvalidOperationException("Die vorbereitete Aufnahmequelle ist bereits gebunden.");
            _leased = true; _observer = observer;
            if (LastFailure is { } failure) Fail(failure);
            while (_pending.TryDequeue(out var block))
            {
                try
                {
                    if (LastFailure is null && !Forward(observer, block.Side, block.Rate, block.Channels, block.Samples, block.Sent, block.Received))
                        Fail(PcmObservationFailure.ObserverRejected);
                }
                catch { Fail(PcmObservationFailure.ObserverFailed); }
                finally { Array.Clear(block.Samples); }
            }
            _sampleCount = 0;
            return new Lease(this);
        }
    }
    public void OnPcmObservationFailed(PcmObservationFailure failure) { lock (_gate) Fail(failure); }
    private void Fail(PcmObservationFailure failure)
    {
        LastFailure ??= failure;
        if (_observer is IConversationPcmFailureObserver boundary)
            try { boundary.OnPcmObservationFailed(failure); } catch { }
        _ready.TrySetException(new PcmRecordingException(PcmRecordingFailure.TimelineConflict));
        while (_pending.TryDequeue(out var block)) Array.Clear(block.Samples);
        _sampleCount = 0;
    }
    private async Task ObserveCompletionAsync()
    {
        try
        {
            var failure = await _native.Completion.ConfigureAwait(false);
            lock (_gate)
            {
                if (failure is { } value) Fail(value);
                if (!_ready.Task.IsCompleted) _ready.TrySetException(new PcmRecordingException(PcmRecordingFailure.TimelineConflict));
                _completion.TrySetResult(LastFailure);
            }
        }
        catch { lock (_gate) { Fail(PcmObservationFailure.ObserverFailed); _completion.TrySetResult(LastFailure); } }
    }
    public ValueTask DisposeAsync() { lock (_gate) return new(_dispose ??= DisposeCoreAsync()); }
    private async Task DisposeCoreAsync()
    {
        lock (_gate) { _closed = true; _ready.TrySetCanceled(); while (_pending.TryDequeue(out var block)) Array.Clear(block.Samples); }
        await _native.DisposeAsync().ConfigureAwait(false);
        await _native.Completion.ConfigureAwait(false);
        lock (_gate) { _observer = null; _completion.TrySetResult(LastFailure); }
    }
    private sealed class Lease(LiveRecordingPreparation owner) : IConversationPcmSubscription
    {
        public Task<PcmObservationFailure?> Completion => owner._completion.Task;
        public ValueTask DisposeAsync()
        {
            lock (owner._gate)
            { owner._retired = true; owner._observer = null; owner._completion.TrySetResult(owner.LastFailure); }
            return ValueTask.CompletedTask;
        }
    }
    private sealed record Block(ConversationAudioSide Side, int Rate, int Channels, short[] Samples,
        SentRtpPcmPosition? Sent, RtpPcmPacketPosition? Received);
}

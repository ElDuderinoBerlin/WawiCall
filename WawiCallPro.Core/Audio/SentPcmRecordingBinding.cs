using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Core.Audio;

internal sealed class SentPcmRecordingBinding : ISentPcmRecordingBinding, ISentRtpPcmObserver, IConversationPcmFailureObserver
{
    private readonly IPcmRecordingSession _session;
    private readonly IPcmRecordingInputGuard _guard;
    private readonly SentPcmTimelineAnchor _anchor;
    private readonly IConversationPcmSubscription _subscription;
    private readonly object _gate = new();
    private Task? _dispose;
    private int _failure, _busy;
    private bool _started;
    private uint _lastTimestamp, _previousDuration;
    private int _previousSamples, _previousRate;
    private long _units, _lastEnd, _trimmed;
    public SentPcmRecordingFailure? Failure
    { get { var value = Volatile.Read(ref _failure); return value == 0 ? null : (SentPcmRecordingFailure)value; } }
    public TimeSpan LastWindowEnd => TimeSpan.FromTicks(Interlocked.Read(ref _lastEnd));
    public long TrimmedSamples => Interlocked.Read(ref _trimmed);
    public Task<SentPcmRecordingFailure?> Completion { get; }

    internal SentPcmRecordingBinding(IConversationPcmFailureSource source, IPcmRecordingSession session,
        IPcmRecordingInputGuard guard, SentPcmTimelineAnchor anchor)
    {
        _session = session; _guard = guard; _anchor = anchor;
        _subscription = source.Subscribe(this);
        Completion = CompleteAsync();
    }

    public bool TryObserve(ConversationAudioSide side, int rate, int channels, ReadOnlySpan<short> samples) =>
        side == ConversationAudioSide.Received || Fail(SentPcmRecordingFailure.MetadataUnavailable);

    public bool TryObserveSent(SentRtpPcmPosition position, int sampleRate, int channels, ReadOnlySpan<short> samples)
    {
        if (Failure is not null) return false;
        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0) return Fail(SentPcmRecordingFailure.ConcurrentInput);
        try
        {
            if (Failure is not null) return false;
            if (_session.State != PcmRecordingState.Capturing) return Fail(SentPcmRecordingFailure.RecordingRejected);
            if (sampleRate is < 8000 or > 192000 || channels != 1 || samples.IsEmpty || samples.Length > sampleRate * 2
                || position.RtpClockRate is < 1 or > 192000 || position.DurationRtpUnits == 0
                || (long)samples.Length * position.RtpClockRate / sampleRate != position.DurationRtpUnits)
                return Fail(SentPcmRecordingFailure.InvalidPcm);
            if (position.SynchronizationSource != _anchor.SynchronizationSource) return Fail(SentPcmRecordingFailure.SourceChanged);
            if (position.RtpClockRate != _anchor.RtpClockRate) return Fail(SentPcmRecordingFailure.ClockChanged);
            var delta = unchecked((int)(position.Timestamp - (_started ? _lastTimestamp : _anchor.Timestamp)));
            if (delta < 0 || delta > _anchor.RtpClockRate * 300L) return Fail(SentPcmRecordingFailure.TimestampDiscontinuity);
            if (_started && ((uint)delta < _previousDuration || (long)delta * _previousRate < (long)_previousSamples * _anchor.RtpClockRate))
                return Fail(SentPcmRecordingFailure.Overlap);
            var units = _units + delta;
            var start = _anchor.RecordingOffset.Ticks + RoundedTicks(units, _anchor.RtpClockRate);
            var end = start + RoundedTicks(samples.Length, sampleRate);
            if (end > TimeSpan.FromHours(8).Ticks) return Fail(SentPcmRecordingFailure.InvalidPcm);
            var trim = start >= 0 ? 0 : (int)Math.Min(samples.Length, (-start * sampleRate + TimeSpan.TicksPerSecond - 1) / TimeSpan.TicksPerSecond);
            var offset = trim;
            while (offset < samples.Length)
            {
                if (Failure is not null) return false;
                var count = Math.Min(sampleRate / 10, samples.Length - offset);
                if (!_session.TryAppend(ConversationAudioSide.Sent, TimeSpan.FromTicks(start + RoundedTicks(offset, sampleRate)), sampleRate, samples.Slice(offset, count)))
                    return Fail(SentPcmRecordingFailure.RecordingRejected);
                offset += count;
            }
            if (Failure is not null) return false;
            _started = true; _lastTimestamp = position.Timestamp; _previousDuration = position.DurationRtpUnits;
            _units = units; _previousSamples = samples.Length; _previousRate = sampleRate;
            Interlocked.Add(ref _trimmed, trim); Interlocked.Exchange(ref _lastEnd, Math.Max(0, end));
            return true;
        }
        catch (Exception) { return Fail(SentPcmRecordingFailure.RecordingRejected); }
        finally { Volatile.Write(ref _busy, 0); }
    }

    private static long RoundedTicks(long units, int rate) => (units * TimeSpan.TicksPerSecond + rate / 2) / rate;
    public void OnPcmObservationFailed(PcmObservationFailure failure) => Fail(failure switch
    {
        PcmObservationFailure.SentMetadataUnavailable => SentPcmRecordingFailure.MetadataUnavailable,
        PcmObservationFailure.InvalidFormat => SentPcmRecordingFailure.InvalidPcm,
        _ => SentPcmRecordingFailure.ObservationFailed
    });
    private bool Fail(SentPcmRecordingFailure failure)
    {
        Interlocked.CompareExchange(ref _failure, (int)failure, 0);
        try { _guard.RejectInput(failure == SentPcmRecordingFailure.InvalidPcm ? PcmRecordingFailure.InvalidBlock : PcmRecordingFailure.TimelineConflict); }
        catch (Exception) { /* A faulty foreign guard must not throw into the source callback. */ }
        return false;
    }
    private async Task<SentPcmRecordingFailure?> CompleteAsync()
    {
        try { if (await _subscription.Completion.ConfigureAwait(false) is { } failure) OnPcmObservationFailed(failure); }
        catch (Exception) { Fail(SentPcmRecordingFailure.ObservationFailed); }
        return Failure;
    }
    public ValueTask DisposeAsync() { lock (_gate) return new(_dispose ??= DisposeCoreAsync()); }
    private async Task DisposeCoreAsync()
    {
        try { await _subscription.DisposeAsync().ConfigureAwait(false); }
        finally { await Completion.ConfigureAwait(false); }
    }
}

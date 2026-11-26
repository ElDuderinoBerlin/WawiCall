using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Core.Audio;

internal sealed class RtpPcmRecordingFeed(IPcmRecordingSession session, IPcmRecordingInputGuard guard,
    ConversationAudioSide side, RtpPcmTimelineAnchor anchor) : IRtpPcmRecordingFeed
{
    private const int MaxG722OverlapSamples = 320; // 20 ms at the G.722 PCM rate.
    private readonly TaskCompletionSource _drained = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _closed, _busy, _failure;
    private bool _started;
    private ushort _lastSequence;
    private uint _lastTimestamp;
    private long _rtpUnits, _lastEnd, _duplicates, _late, _trimmed;
    private int _previousSamples, _previousRate;
    public RtpPcmFeedFailure? Failure
    { get { var value = Volatile.Read(ref _failure); return value == 0 ? null : (RtpPcmFeedFailure)value; } }
    public long DuplicatePackets => Interlocked.Read(ref _duplicates);
    public long LatePackets => Interlocked.Read(ref _late);
    public long TrimmedSamples => Interlocked.Read(ref _trimmed);
    public TimeSpan LastPacketEnd => TimeSpan.FromTicks(Interlocked.Read(ref _lastEnd));

    public RtpPcmAppendResult TryAppend(RtpPcmPacketPosition packet, int pcmSampleRate, ReadOnlySpan<short> samples)
    {
        if (Volatile.Read(ref _closed) != 0) return Failure is null ? RtpPcmAppendResult.Closed : RtpPcmAppendResult.Failed;
        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0) return Fail(RtpPcmFeedFailure.ConcurrentInput);
        try
        {
            if (Volatile.Read(ref _closed) != 0) return Failure is null ? RtpPcmAppendResult.Closed : RtpPcmAppendResult.Failed;
            if (session.State is PcmRecordingState.Stopping or PcmRecordingState.Completed or PcmRecordingState.Aborted)
            { Interlocked.Exchange(ref _closed, 1); return RtpPcmAppendResult.Closed; }
            if (session.State == PcmRecordingState.Failed) return Fail(RtpPcmFeedFailure.RecordingRejected);
            if (pcmSampleRate is < 8000 or > 192000 || samples.IsEmpty || samples.Length > pcmSampleRate * 2 || packet.RtpClockRate is < 1 or > 192000)
                return Fail(RtpPcmFeedFailure.InvalidPacket);
            if (packet.SynchronizationSource != anchor.SynchronizationSource) return Fail(RtpPcmFeedFailure.SourceChanged);
            if (packet.RtpClockRate != anchor.RtpClockRate) return Fail(RtpPcmFeedFailure.ClockChanged);

            var sequenceDelta = unchecked((short)(packet.SequenceNumber - (_started ? _lastSequence : anchor.SequenceNumber)));
            var timestampDelta = unchecked((int)(packet.Timestamp - (_started ? _lastTimestamp : anchor.Timestamp)));
            if (sequenceDelta == short.MinValue) return Fail(RtpPcmFeedFailure.SequenceConflict);
            if (!_started && sequenceDelta == 0 && timestampDelta != 0) return Fail(RtpPcmFeedFailure.SequenceConflict);
            if (!_started && sequenceDelta > 0 && timestampDelta == 0) return Fail(RtpPcmFeedFailure.TimestampDiscontinuity);
            if (_started && sequenceDelta == 0)
            {
                if (timestampDelta != 0) return Fail(RtpPcmFeedFailure.SequenceConflict);
                Interlocked.Increment(ref _duplicates); return RtpPcmAppendResult.Duplicate;
            }
            if (sequenceDelta < 0)
            {
                if (timestampDelta > 0) return Fail(RtpPcmFeedFailure.SequenceConflict);
                Interlocked.Increment(ref _late); return RtpPcmAppendResult.Late;
            }
            if (timestampDelta < 0 || timestampDelta > anchor.RtpClockRate * 300L)
                return Fail(RtpPcmFeedFailure.TimestampDiscontinuity);
            var overlap = 0;
            if (_started && (long)timestampDelta * _previousRate < (long)_previousSamples * anchor.RtpClockRate)
            {
                if (!anchor.TrimShortG722Overlap || side != ConversationAudioSide.Received || sequenceDelta != 1
                    || timestampDelta == 0 || anchor.RtpClockRate != 8000 || _previousRate != 16000 || pcmSampleRate != 16000)
                    return Fail(RtpPcmFeedFailure.Overlap);
                overlap = _previousSamples - 2 * timestampDelta;
                if (overlap > MaxG722OverlapSamples || overlap >= samples.Length)
                    return Fail(RtpPcmFeedFailure.Overlap);
            }

            var units = _rtpUnits + timestampDelta;
            var start = anchor.RecordingOffset.Ticks + RoundedTicks(units, anchor.RtpClockRate);
            var end = start + RoundedTicks(samples.Length, pcmSampleRate);
            if (end > TimeSpan.FromHours(8).Ticks) return Fail(RtpPcmFeedFailure.InvalidPacket);
            var beforeStart = start >= 0 ? 0L : Math.Min(samples.Length, (-start * pcmSampleRate + TimeSpan.TicksPerSecond - 1) / TimeSpan.TicksPerSecond);
            // The earlier packet has already supplied the common interval. Keep the original clock for all remaining samples.
            var trim = Math.Max(beforeStart, overlap);
            var position = (int)trim;
            if (position < samples.Length)
            {
                var chunkSize = pcmSampleRate / 10;
                while (position < samples.Length)
                {
                    if (Failure is not null) return RtpPcmAppendResult.Failed;
                    var count = Math.Min(chunkSize, samples.Length - position);
                    var chunkStart = start + RoundedTicks(position, pcmSampleRate);
                    if (!session.TryAppend(side, TimeSpan.FromTicks(chunkStart), pcmSampleRate, samples.Slice(position, count)))
                        return Fail(RtpPcmFeedFailure.RecordingRejected);
                    position += count;
                }
            }
            if (Failure is not null) return RtpPcmAppendResult.Failed;
            _started = true; _lastSequence = packet.SequenceNumber; _lastTimestamp = packet.Timestamp;
            _rtpUnits = units; _previousSamples = samples.Length; _previousRate = pcmSampleRate;
            Interlocked.Add(ref _trimmed, trim); Interlocked.Exchange(ref _lastEnd, Math.Max(0, end));
            return trim == samples.Length ? RtpPcmAppendResult.BeforeRecording : RtpPcmAppendResult.Accepted;
        }
        catch (Exception) { return Fail(RtpPcmFeedFailure.RecordingRejected); }
        finally
        {
            Interlocked.Exchange(ref _busy, 0);
            if (Volatile.Read(ref _closed) != 0) _drained.TrySetResult();
        }
    }

    private static long RoundedTicks(long units, int rate) => (units * TimeSpan.TicksPerSecond + rate / 2) / rate;
    private RtpPcmAppendResult Fail(RtpPcmFeedFailure failure)
    {
        Interlocked.CompareExchange(ref _failure, (int)failure, 0);
        Interlocked.Exchange(ref _closed, 1);
        try { guard.RejectInput(failure == RtpPcmFeedFailure.InvalidPacket ? PcmRecordingFailure.InvalidBlock : PcmRecordingFailure.TimelineConflict); }
        catch (Exception) { /* A faulty foreign guard must not throw into the media callback. */ }
        if (Volatile.Read(ref _busy) == 0) _drained.TrySetResult();
        return RtpPcmAppendResult.Failed;
    }

    public ValueTask DisposeAsync()
    {
        Interlocked.Exchange(ref _closed, 1);
        if (Volatile.Read(ref _busy) == 0) _drained.TrySetResult();
        return new(_drained.Task);
    }
}

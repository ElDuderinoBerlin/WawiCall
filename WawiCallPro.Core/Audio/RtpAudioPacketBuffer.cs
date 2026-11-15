using System.Diagnostics.CodeAnalysis;
using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Core.Audio;

internal sealed class RtpAudioPacketBuffer(TimeProvider clock, RtpAudioPacketBufferOptions options, TimeSpan delay) : IRtpAudioPacketBuffer
{
    private readonly object _gate = new();
    private readonly Dictionary<ushort, Entry> _packets = new();
    private ushort _nextSequence = options.FirstSequenceNumber;
    private bool _inputComplete, _disposed;
    private RtpAudioBufferFailure? _failure;
    private long _duplicates, _late, _missing;
    public RtpAudioBufferFailure? Failure { get { lock (_gate) return _failure; } }
    public int Count { get { lock (_gate) return _packets.Count; } }
    public long DuplicatePackets { get { lock (_gate) return _duplicates; } }
    public long LatePackets { get { lock (_gate) return _late; } }
    public long MissingPackets { get { lock (_gate) return _missing; } }
    public TimeSpan? NextPacketDelay
    {
        get
        {
            lock (_gate)
            {
                if (_packets.Count == 0) return null;
                return _inputComplete || _packets.ContainsKey(_nextSequence) ? TimeSpan.Zero : RemainingDelay();
            }
        }
    }

    public RtpAudioEnqueueResult TryEnqueue(RtpPcmPacketPosition position, int payloadType, ReadOnlySpan<byte> payload)
    {
        lock (_gate)
        {
            if (_failure is not null) return RtpAudioEnqueueResult.Failed;
            if (_inputComplete || _disposed) return RtpAudioEnqueueResult.Closed;
            if (payload.IsEmpty || payload.Length > 4096 || payloadType is < 0 or > 127 || position.RtpClockRate is < 1 or > 192000)
                return Fail(RtpAudioBufferFailure.InvalidPacket);
            if (position.SynchronizationSource != options.SynchronizationSource) return Fail(RtpAudioBufferFailure.SourceChanged);
            if (position.RtpClockRate != options.RtpClockRate) return Fail(RtpAudioBufferFailure.ClockChanged);
            var distance = unchecked((short)(position.SequenceNumber - _nextSequence));
            // A half sequence space is ambiguous; large forward jumps cannot fit this bounded reorder window.
            if (distance == short.MinValue || distance > 1024) return Fail(RtpAudioBufferFailure.SequenceConflict);
            if (distance < 0) { _late++; return RtpAudioEnqueueResult.Late; }
            if (_packets.TryGetValue(position.SequenceNumber, out var existing))
            {
                if (existing.Packet.Position != position || existing.Packet.PayloadType != payloadType || !payload.SequenceEqual(existing.Packet.Payload.Span))
                    return Fail(RtpAudioBufferFailure.SequenceConflict);
                _duplicates++; return RtpAudioEnqueueResult.Duplicate;
            }
            if (_packets.Count >= options.Capacity) return Fail(RtpAudioBufferFailure.Overflow);
            _packets.Add(position.SequenceNumber, new(new(position, payloadType, payload.ToArray()), clock.GetTimestamp()));
            return RtpAudioEnqueueResult.Accepted;
        }
    }

    public bool TryDequeue([NotNullWhen(true)] out BufferedRtpAudioPacket? packet)
    {
        lock (_gate)
        {
            packet = null;
            if (_packets.Count == 0) return false;
            var sequence = _nextSequence;
            if (!_packets.ContainsKey(sequence))
            {
                if (!_inputComplete && RemainingDelay() > TimeSpan.Zero) return false;
                var nearestDistance = int.MaxValue;
                foreach (var candidate in _packets.Keys)
                {
                    var distance = unchecked((ushort)(candidate - _nextSequence));
                    if (distance < nearestDistance) { nearestDistance = distance; sequence = candidate; }
                }
                _missing += nearestDistance;
            }
            packet = _packets[sequence].Packet;
            _packets.Remove(sequence);
            _nextSequence = unchecked((ushort)(sequence + 1));
            return true;
        }
    }

    private TimeSpan RemainingDelay()
    {
        var now = clock.GetTimestamp();
        var longestWait = TimeSpan.Zero;
        foreach (var entry in _packets.Values)
        {
            var elapsed = clock.GetElapsedTime(entry.ArrivalTimestamp, now);
            if (elapsed > longestWait) longestWait = elapsed;
        }
        return longestWait >= delay ? TimeSpan.Zero : delay - longestWait;
    }

    private RtpAudioEnqueueResult Fail(RtpAudioBufferFailure failure)
    {
        _failure ??= failure;
        _packets.Clear();
        return RtpAudioEnqueueResult.Failed;
    }

    public void CompleteInput() { lock (_gate) _inputComplete = true; }
    public void Dispose() { lock (_gate) { _disposed = true; _packets.Clear(); } }
    private sealed record Entry(BufferedRtpAudioPacket Packet, long ArrivalTimestamp);
}

using System.Diagnostics.CodeAnalysis;

namespace WawiCallPro.Interfaces.Audio;

/// <summary>One explicitly identified audio stream. The delay bounds missing-packet waiting, not media time.</summary>
public sealed record RtpAudioPacketBufferOptions(uint SynchronizationSource, ushort FirstSequenceNumber,
    int RtpClockRate, int Capacity = 64, TimeSpan? ReorderDelay = null);

/// <summary>An owned copy of one encoded payload and its original RTP metadata. No decoded PCM is buffered.</summary>
public sealed class BufferedRtpAudioPacket(RtpPcmPacketPosition position, int payloadType, ReadOnlyMemory<byte> payload)
{
    public RtpPcmPacketPosition Position { get; } = position;
    public int PayloadType { get; } = payloadType;
    public ReadOnlyMemory<byte> Payload { get; } = payload;
}

public enum RtpAudioEnqueueResult { Accepted, Duplicate, Late, Closed, Failed }
public enum RtpAudioBufferFailure { InvalidPacket = 1, SourceChanged, ClockChanged, SequenceConflict, Overflow }

public interface IRtpAudioPacketBufferFactory
{
    /// <summary>Creates an owned per-stream buffer. No timers, transports, devices or decoder are started.</summary>
    IRtpAudioPacketBuffer Create(RtpAudioPacketBufferOptions options);
}

/// <summary>Orders encoded audio before a single stateful decoder. Caller filters non-audio RTP and negotiates formats.
/// Calls are synchronized, but the caller must serialize dequeue, decode and PCM delivery as one pipeline.
/// Poll after NextPacketDelay even when no more packets arrive; arrival time is used only for this deadline.</summary>
public interface IRtpAudioPacketBuffer : IDisposable
{
    RtpAudioBufferFailure? Failure { get; }
    int Count { get; }
    long DuplicatePackets { get; }
    long LatePackets { get; }
    long MissingPackets { get; }
    /// <summary>Null when empty/failed/disposed, zero when ready, otherwise remaining missing-packet wait.</summary>
    TimeSpan? NextPacketDelay { get; }
    /// <summary>Copies at most 4096 payload bytes. Capacity never waits; overflow fails and clears queued payloads.</summary>
    RtpAudioEnqueueResult TryEnqueue(RtpPcmPacketPosition position, int payloadType, ReadOnlySpan<byte> payload);
    /// <summary>Transfers ownership of the next ordered packet; false also means a gap is still within its deadline.</summary>
    bool TryDequeue([NotNullWhen(true)] out BufferedRtpAudioPacket? packet);
    /// <summary>Stops input and makes remaining packets drainable in sequence order without a further gap wait.</summary>
    void CompleteInput();
}

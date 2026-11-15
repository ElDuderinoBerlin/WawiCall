using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Core.Audio;

/// <summary>Shares only a monotonic deadline clock; each stream owns its packet storage.</summary>
public sealed class RtpAudioPacketBufferFactory(TimeProvider clock) : IRtpAudioPacketBufferFactory
{
    public IRtpAudioPacketBuffer Create(RtpAudioPacketBufferOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var delay = options.ReorderDelay ?? TimeSpan.FromMilliseconds(60);
        if (options.RtpClockRate is < 1 or > 192000 || options.Capacity is < 1 or > 256 ||
            delay < TimeSpan.Zero || delay > TimeSpan.FromSeconds(1))
            throw new ArgumentException("Ungültige Grenzen für den RTP-Audiopuffer.", nameof(options));
        return new RtpAudioPacketBuffer(clock, options, delay);
    }
}

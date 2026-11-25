using System.Collections.Immutable;

namespace WawiCallPro.Interfaces.Audio;

public enum AudioEndpointDirection { Capture, Playback }

/// <summary>Signal peak and Windows controls of one endpoint, expressed independently.</summary>
public sealed record AudioEndpointReading(int PeakPercent, int WindowsVolumePercent, bool Muted)
{
    public ImmutableArray<int> Spectrum { get; init; } = AudioSpectrum.Silence;
}

/// <summary>Reads the endpoint controls and its live level and frequency bands.</summary>
public interface IAudioEndpointMeter : IDisposable
{
    AudioEndpointReading? Read();
}

public interface IAudioEndpointMeterFactory
{
    IAudioEndpointMeter? Open(AudioEndpointDirection direction, string? deviceId);
}

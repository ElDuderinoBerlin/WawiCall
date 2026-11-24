namespace WawiCallPro.Interfaces.Audio;

public enum AudioEndpointDirection { Capture, Playback }

/// <summary>Signal peak and Windows controls of one endpoint, expressed independently.</summary>
public sealed record AudioEndpointReading(int PeakPercent, int WindowsVolumePercent, bool Muted);

/// <summary>Reads an endpoint without opening a capture or playback stream.</summary>
public interface IAudioEndpointMeter : IDisposable
{
    AudioEndpointReading? Read();
}

public interface IAudioEndpointMeterFactory
{
    IAudioEndpointMeter? Open(AudioEndpointDirection direction, string? deviceId);
}

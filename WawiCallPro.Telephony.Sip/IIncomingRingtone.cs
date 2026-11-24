using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.Telephony.Sip;

/// <summary>Plays a local incoming-call signal until cancelled. Completion includes output disposal.</summary>
public interface IIncomingRingtone
{
    Task PlayAsync(string? deviceId, int volumePercent, CancellationToken cancellationToken);
    Task PlayAsync(AudioDeviceSettings settings, CancellationToken cancellationToken) =>
        PlayAsync(settings.RingDeviceId, settings.RingVolumePercent, cancellationToken);
}

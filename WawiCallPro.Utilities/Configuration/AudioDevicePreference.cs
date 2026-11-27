using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Utilities.Configuration;

/// <summary>Availability-independent preference matching shared by the settings and native audio paths.</summary>
public static class AudioDevicePreference
{
    public static AudioDeviceInfo? Find(IEnumerable<AudioDeviceInfo> devices, string? preferredId, string? rememberedName)
    {
        if (preferredId is null) return null;
        AudioDeviceInfo? named = null;
        var ambiguous = false;
        foreach (var device in devices)
        {
            if (string.Equals(device.Id, preferredId, StringComparison.OrdinalIgnoreCase)) return device;
            if (rememberedName is null || !string.Equals(device.DisplayName, rememberedName, StringComparison.OrdinalIgnoreCase)) continue;
            ambiguous |= named is not null;
            named = device;
        }
        return ambiguous ? null : named;
    }
}

/// <summary>Enriches explicitly selected endpoint IDs with their last known Windows display names.</summary>
public interface IAudioDeviceNames
{
    AudioDeviceSettings Remember(AudioDeviceSettings devices);
}

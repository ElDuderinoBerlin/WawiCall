using CSCore.CoreAudioAPI;
using System.Runtime.InteropServices;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Diagnostics;

namespace WawiCallPro.Audio;

/// <summary>Reads endpoint names from Windows, including retained records for temporarily disconnected devices.</summary>
public sealed class WindowsAudioDeviceNames(StartupExecutionMode? mode = null) : IAudioDeviceNames
{
    public AudioDeviceSettings Remember(AudioDeviceSettings devices)
    {
        if (mode?.SuppressNetwork == true || devices is { CaptureDeviceId: null, PlaybackDeviceId: null, RingDeviceId: null }) return devices;
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            return devices with
            {
                CaptureDeviceName = RememberName(enumerator, devices.CaptureDeviceId, devices.CaptureDeviceName),
                PlaybackDeviceName = RememberName(enumerator, devices.PlaybackDeviceId, devices.PlaybackDeviceName),
                RingDeviceName = RememberName(enumerator, devices.RingDeviceId, devices.RingDeviceName)
            };
        }
        catch (COMException) { return devices; }
    }

    private static string? RememberName(MMDeviceEnumerator enumerator, string? id, string? remembered)
    {
        if (id is null) return null;
        try
        {
            using var device = enumerator.GetDevice(id);
            var name = device.FriendlyName;
            return !string.IsNullOrWhiteSpace(name) && name.Length <= 1024 && !name.Any(char.IsControl) ? name : remembered;
        }
        catch (COMException) { return remembered; }
    }

    internal static string? ResolveId(string? id, string? name, DataFlow flow)
    {
        if (id is null || name is null) return id;
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            try
            {
                using var selected = enumerator.GetDevice(id);
                if (selected.DeviceState == DeviceState.Active) return selected.DeviceID;
            }
            catch (COMException) { }

            using var endpoints = enumerator.EnumAudioEndpoints(flow, DeviceState.Active);
            var devices = new List<AudioDeviceInfo>();
            foreach (var endpoint in endpoints)
            {
                using (endpoint) devices.Add(new(endpoint.DeviceID, endpoint.FriendlyName));
            }
            return AudioDevicePreference.Find(devices, id, name)?.Id ?? id;
        }
        catch (COMException) { return id; }
    }
}

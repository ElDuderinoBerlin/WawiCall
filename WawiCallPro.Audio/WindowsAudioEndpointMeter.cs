using CSCore.CoreAudioAPI;
using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Audio;

internal sealed class WindowsAudioEndpointMeter(AudioMeterInformation meter, AudioEndpointVolume volume) : IAudioEndpointMeter
{
    internal static WindowsAudioEndpointMeter? Open(AudioEndpointDirection direction, string? id)
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var flow = direction == AudioEndpointDirection.Capture ? DataFlow.Capture : DataFlow.Render;
            using var device = id is null ? MMDeviceEnumerator.DefaultAudioEndpoint(flow, Role.Multimedia) : enumerator.GetDevice(id);
            if (device.DeviceState != DeviceState.Active || device.DataFlow != flow) return null;
            var meter = AudioMeterInformation.FromDevice(device);
            try { return new WindowsAudioEndpointMeter(meter, AudioEndpointVolume.FromDevice(device)); }
            catch { meter.Dispose(); throw; }
        }
        catch (CoreAudioAPIException) { return null; }
        catch (System.Runtime.InteropServices.COMException) { return null; }
    }

    public AudioEndpointReading? Read()
    {
        try { return new(ToPercent(meter.PeakValue), ToPercent(volume.MasterVolumeLevelScalar), volume.IsMuted); }
        catch (CoreAudioAPIException) { return null; }
        catch (System.Runtime.InteropServices.COMException) { return null; }
    }

    public void Dispose()
    {
        try { volume.Dispose(); }
        finally { meter.Dispose(); }
    }

    private static int ToPercent(float level) => (int)MathF.Round(Math.Clamp(level, 0F, 1F) * 100);
}

using CSCore.CoreAudioAPI;
using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Audio;

internal sealed class WindowsAudioEndpointMeter(AudioMeterInformation meter, AudioEndpointVolume volume,
    WindowsEndpointSpectrum spectrum, DataFlow flow, string deviceId, bool followsDefault) : IAudioEndpointMeter
{
    private long _nextDefaultCheck = Environment.TickCount64 + 1500;

    internal static WindowsAudioEndpointMeter? Open(AudioEndpointDirection direction, string? id)
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var flow = direction == AudioEndpointDirection.Capture ? DataFlow.Capture : DataFlow.Render;
            using var device = id is null ? MMDeviceEnumerator.DefaultAudioEndpoint(flow, Role.Multimedia) : enumerator.GetDevice(id);
            if (device.DeviceState != DeviceState.Active || device.DataFlow != flow) return null;
            var meter = AudioMeterInformation.FromDevice(device);
            try
            {
                var deviceId = device.DeviceID;
                var volume = AudioEndpointVolume.FromDevice(device);
                try { return new WindowsAudioEndpointMeter(meter, volume, new WindowsEndpointSpectrum(direction, deviceId), flow, deviceId, id is null); }
                catch { volume.Dispose(); throw; }
            }
            catch { meter.Dispose(); throw; }
        }
        catch (CoreAudioAPIException) { return null; }
        catch (System.Runtime.InteropServices.COMException) { return null; }
    }

    public AudioEndpointReading? Read()
    {
        try
        {
            var now = Environment.TickCount64;
            if (followsDefault && now >= _nextDefaultCheck)
            {
                _nextDefaultCheck = now + 1500;
                using var current = MMDeviceEnumerator.DefaultAudioEndpoint(flow, Role.Multimedia);
                if (!string.Equals(deviceId, current.DeviceID, StringComparison.OrdinalIgnoreCase)) return null;
            }
            var muted = volume.IsMuted;
            return new(ToPercent(meter.PeakValue), ToPercent(volume.MasterVolumeLevelScalar), muted)
            { Spectrum = muted ? AudioSpectrum.Silence : spectrum.Read() };
        }
        catch (CoreAudioAPIException) { return null; }
        catch (System.Runtime.InteropServices.COMException) { return null; }
    }

    public void Dispose()
    {
        try { spectrum.Dispose(); }
        finally
        {
            try { volume.Dispose(); }
            finally { meter.Dispose(); }
        }
    }

    private static int ToPercent(float level) => (int)MathF.Round(Math.Clamp(level, 0F, 1F) * 100);
}

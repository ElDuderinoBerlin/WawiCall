using CSCore.CoreAudioAPI;
using WawiCallPro.Audio;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Utilities.Diagnostics;

namespace WawiCallPro.App.Tests;

[TestClass]
public sealed class EndpointMeterTests
{
    [TestMethod]
    public async Task NativeMeterReadsSelectedCaptureAndPlaybackWithoutChangingWindowsControls()
    {
        var deviceName = Environment.GetEnvironmentVariable("WCP_TEST_ENDPOINT_METER_DEVICE");
        if (string.IsNullOrWhiteSpace(deviceName)) Assert.Inconclusive("Set WCP_TEST_ENDPOINT_METER_DEVICE to opt in to endpoint reads.");

        var catalog = new WindowsAudioDeviceCatalog(new StartupExecutionMode(false));
        var devices = await catalog.ReadAsync();
        using var enumerator = new MMDeviceEnumerator();
        foreach (var (direction, entries) in new[] { (AudioEndpointDirection.Capture, devices.Capture), (AudioEndpointDirection.Playback, devices.Playback) })
        {
            var device = entries.Single(item => item.DisplayName.Contains(deviceName, StringComparison.OrdinalIgnoreCase));
            using var endpoint = enumerator.GetDevice(device.Id);
            using var controls = AudioEndpointVolume.FromDevice(endpoint);
            using var meter = catalog.Open(direction, device.Id);
            Assert.IsNotNull(meter);
            var reading = meter.Read();
            Assert.IsNotNull(reading);
            Assert.IsTrue(reading.PeakPercent is >= 0 and <= 100);
            Assert.AreEqual((int)MathF.Round(controls.MasterVolumeLevelScalar * 100), reading.WindowsVolumePercent);
            Assert.AreEqual(controls.IsMuted, reading.Muted);
            Console.WriteLine($"{device.DisplayName}: {reading.PeakPercent} % peak, Windows {reading.WindowsVolumePercent} %, muted={reading.Muted}.");
        }
    }
}

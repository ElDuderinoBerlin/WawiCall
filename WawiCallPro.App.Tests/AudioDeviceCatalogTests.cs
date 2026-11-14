using WawiCallPro.Audio;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Utilities.Diagnostics;

namespace WawiCallPro.App.Tests;

[TestClass]
public sealed class AudioDeviceCatalogTests
{
    [TestMethod]
    public async Task AudioDeviceCatalogSuppressesHardwareInAuditsAndHonorsCancellation()
    {
        var catalog = new WindowsAudioDeviceCatalog(new StartupExecutionMode(true));
        var result = await catalog.ReadAsync();
        Assert.IsTrue(result.Suppressed); Assert.IsTrue(result.Capture.IsEmpty); Assert.IsTrue(result.Playback.IsEmpty);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => catalog.ReadAsync(cancellation.Token));
    }

    [TestMethod]
    public async Task AudioDeviceCatalogReadsOnlyNativeEndpointMetadataWithoutOpeningStreams()
    {
        var catalog = new WindowsAudioDeviceCatalog(new StartupExecutionMode(false));
        var result = await catalog.ReadAsync(); Assert.IsFalse(result.Suppressed);
        foreach (var devices in new[] { result.Capture, result.Playback })
        {
            Assert.IsFalse(devices.IsDefault);
            Assert.AreEqual(devices.Length, devices.Select(device => device.Id).Distinct(StringComparer.Ordinal).Count());
            Assert.IsTrue(devices.All(device => !string.IsNullOrWhiteSpace(device.Id) && !string.IsNullOrWhiteSpace(device.DisplayName)));
        }
    }
}

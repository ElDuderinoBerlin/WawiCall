using CSCore.CoreAudioAPI;
using WawiCallPro.Audio;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Diagnostics;
using WawiCallPro.Utilities.Persistence;
using WawiCallPro.Utilities.Security;

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
    [TestMethod]
    public async Task WindowsAudioDeviceNamesResolveAReappearingEndpointWithAChangedId()
    {
        var catalog = await new WindowsAudioDeviceCatalog(new StartupExecutionMode(false)).ReadAsync();
        var output = catalog.Playback.FirstOrDefault(entry => catalog.Playback.Count(other =>
            string.Equals(entry.DisplayName, other.DisplayName, StringComparison.OrdinalIgnoreCase)) == 1);
        if (output is null) Assert.Inconclusive("Requires a uniquely named Windows playback endpoint.");

        var saved = new WindowsAudioDeviceNames().Remember(new() { PlaybackDeviceId = output.Id, RingDeviceId = output.Id });
        Assert.AreEqual(output.DisplayName, saved.PlaybackDeviceName);
        Assert.AreEqual(output.DisplayName, saved.RingDeviceName);
        Assert.AreEqual(output.Id, WindowsAudioDeviceNames.ResolveId("previous-endpoint", output.DisplayName, DataFlow.Render));
        Assert.IsNull(WindowsAudioDeviceNames.ResolveId(null, output.DisplayName, DataFlow.Render));
        var profile = Directory.CreateTempSubdirectory("Wcp-AudioDeviceNames-");
        try
        {
            var location = new ConfigurationLocation(profile.FullName);
            var validator = new ApplicationOptionsValidator(new WindowsUserSecretProtector());
            var legacy = new ConfigurationStore(location, validator);
            await legacy.SaveAsync(new ApplicationOptions
            {
                User = new UserSettings { StartMinimized = true },
                Devices = new AudioDeviceSettings { PlaybackDeviceId = output.Id }
            });
            var store = new ConfigurationStore(location, validator, new PhoneSearchChanges(), new WindowsAudioDeviceNames());
            var runtime = new ApplicationConfigurationService(store);
            await runtime.StartAsync(default);
            Assert.AreEqual(output.DisplayName, runtime.ConversationDevices.PlaybackName);
            var loaded = await legacy.LoadAsync();
            Assert.AreEqual(output.DisplayName, loaded.Options.Devices.PlaybackDeviceName);
            Assert.AreEqual(output.Id, loaded.Options.Devices.PlaybackDeviceId);
            Assert.IsTrue(loaded.Options.User.StartMinimized);
            await runtime.StopAsync(default);
        }
        finally { profile.Delete(true); }
    }

    [TestMethod]
    public void RememberedNameDoesNotOverrideAnExistingEndpointId()
    {
        var existing = new AudioDeviceInfo("old", "Umbenannter Kopfhörer");
        var sameName = new AudioDeviceInfo("new", "Alter Name");
        Assert.AreEqual(existing, AudioDevicePreference.Find([sameName, existing], "OLD", "Alter Name"));
    }
}

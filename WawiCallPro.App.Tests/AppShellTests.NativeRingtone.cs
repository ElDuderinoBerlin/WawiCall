using System.Buffers.Binary;
using CSCore;
using CSCore.Codecs.WAV;
using DevExpress.XtraEditors;
using Microsoft.Extensions.DependencyInjection;
using WawiCallPro.App.Views;
using WawiCallPro.Audio;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Utilities.Diagnostics;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    public Task AudioDeviceSettingsRestoreSelectionsWithDifferentIdCasing() => WithWorkspace(async (workspace, _, _) =>
    {
        var snapshot = await workspace.Settings.LoadAsync();
        await workspace.Settings.SaveAsync(snapshot.Options with { Devices = new()
        {
            CaptureDeviceId = "CAPTURE-ONE", PlaybackDeviceId = "RENDER-ONE", RingDeviceId = "RENDER-ONE"
        } }, snapshot);
        var catalog = new AudioCatalogDouble();
        var available = catalog.Result;
        catalog.Result = new([], []);
        using var dialog = new FrmSettings(workspace);
        dialog.PrepareForBackgroundRun();
        dialog.ConfigureAudioDevices(catalog);
        Assert.IsTrue(await dialog.PrepareAsync());
        var page = Field<SettingsPage>(dialog, "settingsPage");
        var capture = Field<LookUpEdit>(page, "captureDeviceEdit");
        var playback = Field<LookUpEdit>(page, "playbackDeviceEdit");
        var ring = Field<LookUpEdit>(page, "ringDeviceEdit");
        Assert.AreEqual("CAPTURE-ONE", capture.EditValue);
        Assert.AreEqual("RENDER-ONE", playback.EditValue);
        Assert.AreEqual("RENDER-ONE", ring.EditValue);

        catalog.Result = available;
        await page.ReloadAudioDevicesAsync();
        Assert.AreEqual("capture-one", capture.EditValue);
        Assert.AreEqual("render-one", playback.EditValue);
        Assert.AreEqual("render-one", ring.EditValue);
        await dialog.StopAsync();
    });

    [TestMethod]
    public Task NativeRingtoneSliderChangesTheRunningJabraOutput() => WithWorkspace(async (workspace, services, _) =>
    {
        var name = Environment.GetEnvironmentVariable("WCP_NATIVE_AUDIO_DEVICE");
        if (string.IsNullOrWhiteSpace(name)) Assert.Inconclusive("Opt in to the physical output test.");
        var catalog = new WindowsAudioDeviceCatalog(new StartupExecutionMode(false), services.GetRequiredService<WindowsIncomingRingtone>());
        var endpoint = (await catalog.ReadAsync()).Playback.Single(device => device.DisplayName.Contains(name, StringComparison.OrdinalIgnoreCase));
        using var dialog = new FrmSettings(workspace);
        dialog.PrepareForBackgroundRun();
        dialog.ConfigureAudioDevices(catalog);
        Assert.IsTrue(await dialog.PrepareAsync());
        dialog.SelectAutomationTab("settings-audio");
        dialog.Show();
        var page = Field<SettingsPage>(dialog, "settingsPage");
        Field<LookUpEdit>(page, "ringDeviceEdit").EditValue = endpoint.Id;
        var file = Path.Combine(workspace.Paths.RootDirectory, "volume-test.wav");
        var data = new byte[48000 * sizeof(short)];
        for (var sample = 0; sample < 48000; sample++)
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(sample * sizeof(short)), (short)(5000 * Math.Sin(2 * Math.PI * 440 * sample / 48000)));
        using (var wave = new WaveWriter(file, new WaveFormat(48000, 16, 1))) wave.Write(data, 0, data.Length);
        await page.SelectCustomRingtoneAsync(file);
        var slider = Field<TrackBarControl>(page, "ringVolumeSlider");
        var button = Field<SimpleButton>(page, "previewRingtoneButton");
        var peak = Field<ProgressBarControl>(page, "ringPeak");
        slider.Value = 15;
        var playback = page.PreviewRingtoneAsync();
        try
        {
            await Task.Delay(500);
            Assert.AreEqual("Stopp", button.Text);
            Assert.IsFalse(dialog.UseWaitCursor);
            var low = await ObservePeakUntilAsync(peak, Task.Delay(600));
            slider.Value = 60;
            await Task.Delay(400);
            var high = await ObservePeakUntilAsync(peak, Task.Delay(600));
            Assert.AreEqual("Stopp", button.Text);
            Console.WriteLine($"{workspace.Application.ProductLine}: {endpoint.DisplayName}, fortlaufender Testton; Regler 15 → 60 %, Gerätepegel {low} → {high} %.");
            Assert.IsGreaterThan(0, low);
            Assert.IsGreaterThanOrEqualTo(low * 2, high);
        }
        finally
        {
            if (button.Text == "Stopp") button.PerformClick();
            await playback.WaitAsync(TimeSpan.FromSeconds(5));
            await dialog.StopAsync();
        }
    }, configure: services => services.AddAudioConversion());

    [TestMethod]
    public Task NativeDeviceListTracksPhysicalJabraRemovalAndReturn() => WithWorkspace(async (workspace, _, _) =>
    {
        var name = Environment.GetEnvironmentVariable("WCP_NATIVE_AUDIO_DEVICE");
        var coordination = Environment.GetEnvironmentVariable("WCP_HOTPLUG_TEST_DIR");
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(coordination)) Assert.Inconclusive("Opt in to the physical unplug test.");
        var catalog = new WindowsAudioDeviceCatalog(new StartupExecutionMode(false));
        var devices = await catalog.ReadAsync();
        var microphone = devices.Capture.Single(device => device.DisplayName.Contains(name, StringComparison.OrdinalIgnoreCase));
        var headphones = devices.Playback.Single(device => device.DisplayName.Contains(name, StringComparison.OrdinalIgnoreCase));
        using var dialog = new FrmSettings(workspace);
        dialog.PrepareForBackgroundRun();
        dialog.ConfigureAudioDevices(catalog);
        Assert.IsTrue(await dialog.PrepareAsync());
        dialog.SelectAutomationTab("settings-audio");
        dialog.Show();
        var page = Field<SettingsPage>(dialog, "settingsPage");
        var capture = Field<LookUpEdit>(page, "captureDeviceEdit");
        var playback = Field<LookUpEdit>(page, "playbackDeviceEdit");
        var ring = Field<LookUpEdit>(page, "ringDeviceEdit");
        capture.EditValue = microphone.Id;
        playback.EditValue = ring.EditValue = headphones.Id;
        var edition = workspace.Application.ProductLine;
        await File.WriteAllTextAsync(Path.Combine(coordination, edition + ".ready"), "ready");
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        try
        {
            while (capture.Text != "Gerät nicht verfügbar" || playback.Text != "Gerät nicht verfügbar" || ring.Text != "Gerät nicht verfügbar")
                await Task.Delay(100, timeout.Token);
            Assert.AreEqual(microphone.Id, capture.EditValue);
            Assert.AreEqual(headphones.Id, playback.EditValue);
            Assert.AreEqual(headphones.Id, ring.EditValue);
            Console.WriteLine($"{edition}: Windows entfernt, alle drei Zuordnungen als nicht verfügbar angezeigt und erhalten.");
            await File.WriteAllTextAsync(Path.Combine(coordination, edition + ".removed"), "removed", timeout.Token);
            while (capture.Text != microphone.DisplayName || playback.Text != headphones.DisplayName || ring.Text != headphones.DisplayName)
                await Task.Delay(100, timeout.Token);
            Console.WriteLine($"{edition}: Mikrofon, Gesprächs- und Klingelausgabe nach Rückkehr automatisch angezeigt.");
        }
        finally { await dialog.StopAsync(); }
    }, timeout: TimeSpan.FromMinutes(3));
}

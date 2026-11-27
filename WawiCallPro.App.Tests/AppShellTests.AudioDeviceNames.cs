using DevExpress.XtraEditors;
using WawiCallPro.App.Views;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    public Task AudioDeviceSettingsRestoreTheRememberedNameOnlyWhenUnique() => WithWorkspace(async (workspace, _, _) =>
    {
        var snapshot = await workspace.Settings.LoadAsync();
        await workspace.Settings.SaveAsync(snapshot.Options with
        {
            Devices = snapshot.Options.Devices with
            {
                CaptureDeviceId = "old-in", CaptureDeviceName = "Mikrofon eins",
                PlaybackDeviceId = "old-out", PlaybackDeviceName = "Kopfhörer eins",
                RingDeviceId = "old-ring", RingDeviceName = "Klingelausgabe eins"
            }
        }, snapshot);
        var catalog = new AudioCatalogDouble { Result = new([], []) };
        using var settings = new FrmSettings(workspace);
        settings.PrepareForBackgroundRun();
        settings.ConfigureAudioDevices(catalog);
        settings.SelectAutomationTab("settings-audio");
        await settings.PrepareAsync();
        settings.Show();
        var page = Field<SettingsPage>(settings, "settingsPage");
        var capture = Field<LookUpEdit>(page, "captureDeviceEdit");
        var output = Field<LookUpEdit>(page, "playbackDeviceEdit");
        var ring = Field<LookUpEdit>(page, "ringDeviceEdit");
        Assert.Contains("Mikrofon eins", capture.Text);
        Assert.Contains("nicht verfügbar", capture.Text);

        catalog.Result = new([new("input-a", "Mikrofon eins"), new("input-b", "Mikrofon eins")],
            [new("new-output", "Kopfhörer eins"), new("new-ring", "Klingelausgabe eins")]);
        await page.ReloadAudioDevicesAsync();
        Assert.AreEqual("old-in", capture.EditValue);
        Assert.AreEqual("new-output", output.EditValue);
        Assert.AreEqual("new-ring", ring.EditValue);

        catalog.Result = catalog.Result with { Capture = [new("input-b", "Mikrofon eins")] };
        await page.ReloadAudioDevicesAsync();
        Assert.AreEqual("input-b", capture.EditValue);
        await page.SaveAsync(default);

        var saved = (await workspace.Settings.LoadAsync()).Options.Devices;
        Assert.AreEqual("input-b", saved.CaptureDeviceId);
        Assert.AreEqual("Mikrofon eins", saved.CaptureDeviceName);
        Assert.AreEqual("Kopfhörer eins", saved.PlaybackDeviceName);
        Assert.AreEqual("Klingelausgabe eins", saved.RingDeviceName);
        var json = await File.ReadAllTextAsync(Path.Combine(workspace.Paths.RootDirectory, "appsettings.json"));
        Assert.Contains("\"captureDeviceName\"", json);

        capture.EditValue = "";
        await page.SaveAsync(default);
        var defaultInput = (await workspace.Settings.LoadAsync()).Options.Devices;
        Assert.IsNull(defaultInput.CaptureDeviceName);
        Assert.IsNull(defaultInput.CaptureDeviceId);
        await settings.StopAsync();
    });
}

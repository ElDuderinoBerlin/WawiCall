using CSCore;
using CSCore.Codecs.WAV;
using DevExpress.LookAndFeel;
using DevExpress.Utils;
using DevExpress.XtraEditors;
using WawiCallPro.App.Diagnostics;
using WawiCallPro.App.Hosting;
using WawiCallPro.App.Views;
using WawiCallPro.Audio;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Diagnostics;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    [DataRow("Office 2019 Black", 16F, 1F)]
    [DataRow("Basic", 10F, 1.5F)]
    public Task WindowsRingtoneSettingsPreviewDraftAndPreserveMissingSelections(string skin, float points, float scale) => WithWorkspace(async (workspace, _, _) =>
    {
        var snapshot = await workspace.Settings.LoadAsync();
        await workspace.Settings.SaveAsync(snapshot.Options with
        {
            Devices = snapshot.Options.Devices with { WindowsRingtoneFile = "RingPrevious.wav" }
        }, snapshot);
        var media = Path.Combine(workspace.Paths.RootDirectory, "fixture-media");
        Directory.CreateDirectory(media);
        using (var wave = new WaveWriter(Path.Combine(media, "RingNew.wav"), new WaveFormat(22050, 16, 1)))
            wave.Write(new byte[256], 0, 256);
        var output = new RingtoneSettingsOutput();
        var playback = new WindowsIncomingRingtone(output, media);
        var catalog = new WindowsAudioDeviceCatalog(new StartupExecutionMode(false), playback);
        var previousSkin = UserLookAndFeel.Default.ActiveSkinName;
        var previousPalette = UserLookAndFeel.Default.ActiveSvgPaletteName;
        var previousFont = WindowsFormsSettings.DefaultFont;
        var previousAppearance = AppearanceObject.DefaultFont;
        try
        {
            AppUiStartup.Apply();
            UserLookAndFeel.Default.SetSkinStyle(skin);
            using var font = new Font("Segoe UI", points);
            WindowsFormsSettings.DefaultFont = AppearanceObject.DefaultFont = font;
            using var dialog = new FrmSettings(workspace);
            dialog.PrepareForBackgroundRun();
            dialog.Font = font;
            dialog.ConfigureAudioDevices(catalog);
            await dialog.PrepareAsync();
            dialog.SelectAutomationTab("settings-audio");
            dialog.Show();
            var page = Field<SettingsPage>(dialog, "settingsPage");
            var ringtone = Field<LookUpEdit>(page, "windowsRingtoneEdit");
            var button = Field<SimpleButton>(page, "previewRingtoneButton");
            Assert.AreEqual("RingPrevious.wav", ringtone.EditValue);
            Assert.Contains("nicht verfügbar", ringtone.Text);
            Assert.IsTrue(button.Enabled);
            ringtone.EditValue = "RingNew.wav";
            Field<SpinEdit>(page, "ringVolumeEdit").Value = 35;
            await page.PreviewRingtoneAsync();
            Assert.AreEqual(22050, output.SampleRate);
            Assert.AreEqual(35, output.Volume);
            Assert.AreEqual("RingPrevious.wav", (await workspace.Settings.LoadAsync()).Options.Devices.WindowsRingtoneFile);
            await page.SaveAsync(CancellationToken.None);
            await page.ReloadAsync();
            Assert.AreEqual("RingNew.wav", ringtone.EditValue);
            Assert.AreEqual("RingNew.wav", (await workspace.Settings.LoadAsync()).Options.Devices.WindowsRingtoneFile);
            dialog.MinimumSize = Size.Empty;
            dialog.ClientSize = new Size(680, 760);
            if (scale != 1) dialog.Scale(new SizeF(scale, scale));
            dialog.PerformLayout();
            Application.DoEvents();
            CheckTableBounds(dialog);
            Assert.AreEqual(Field<LookUpEdit>(page, "ringDeviceEdit").Left, ringtone.Left);
            Assert.IsLessThanOrEqualTo(button.Left, ringtone.Right);
            Assert.IsLessThan(Field<SimpleButton>(page, "refreshAudioButton").Width, button.Width);
            var folder = Path.Combine(RenderOutput(), "windows-ringtones", workspace.Application.ProductLine);
            Directory.CreateDirectory(folder);
            using var image = UiAudit.Capture(dialog);
            image.Save(Path.Combine(folder, $"ringtone-{skin}-{points}.png"));
            await dialog.StopAsync();
        }
        finally
        {
            UserLookAndFeel.Default.SetSkinStyle(previousSkin, previousPalette);
            WindowsFormsSettings.DefaultFont = previousFont;
            AppearanceObject.DefaultFont = previousAppearance;
        }
    });

    private sealed class RingtoneSettingsOutput : IHistoryRecordingOutput
    {
        internal int SampleRate { get; private set; }
        internal int Volume { get; private set; }

        public void Play(IWaveSource source, AudioDeviceSettings settings, CancellationToken token)
        {
            SampleRate = source.WaveFormat.SampleRate;
            Volume = settings.PlaybackVolumePercent;
        }
    }
}

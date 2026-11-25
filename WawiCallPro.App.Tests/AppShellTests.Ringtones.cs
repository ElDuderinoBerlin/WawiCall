using CSCore;
using CSCore.Codecs.WAV;
using DevExpress.LookAndFeel;
using DevExpress.Utils;
using DevExpress.XtraEditors;
using WawiCallPro.App.Diagnostics;
using WawiCallPro.App.Hosting;
using WawiCallPro.App.Views;
using WawiCallPro.Audio;
using WawiCallPro.Interfaces.Audio;
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
            var slider = Field<TrackBarControl>(page, "ringVolumeSlider");
            var microphone = Field<TrackBarControl>(page, "micVolumeSlider");
            var outputVolume = Field<TrackBarControl>(page, "playVolumeSlider");
            Assert.IsTrue(microphone.Properties.ShowValueToolTip);
            Assert.IsTrue(outputVolume.Properties.ShowValueToolTip);
            Assert.IsTrue(slider.Properties.ShowValueToolTip);
            microphone.Value = 64;
            outputVolume.Value = 78;
            slider.Value = 35;
            var notice = page.LastSettingsNotice;
            await page.PreviewRingtoneAsync();
            Assert.AreEqual(notice, page.LastSettingsNotice);
            var status = Field<LabelControl>(page, "audioStatus");
            Assert.AreEqual("", status.Text);
            Assert.AreEqual(22050, output.SampleRate);
            Assert.AreEqual(35, output.Volume);
            output.Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
            var playing = page.PreviewRingtoneAsync();
            var liveVolume = await output.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsFalse(dialog.UseWaitCursor);
            Assert.IsTrue(slider.Enabled);
            Assert.AreEqual("Stopp", button.Text);
            Assert.AreEqual("", status.Text);
            slider.Value = 57;
            Assert.AreEqual(57, liveVolume.Percent);
            button.PerformClick();
            await playing.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.AreEqual("Abspielen", button.Text);
            Assert.AreEqual("", status.Text);
            output.Started = null;
            slider.Value = 35;
            Assert.AreEqual("RingPrevious.wav", (await workspace.Settings.LoadAsync()).Options.Devices.WindowsRingtoneFile);
            await page.SaveAsync(CancellationToken.None);
            await page.ReloadAsync();
            Assert.AreEqual("RingNew.wav", ringtone.EditValue);
            Assert.AreEqual(35, slider.Value);
            Assert.AreEqual(64, microphone.Value);
            Assert.AreEqual(78, outputVolume.Value);
            Assert.AreEqual("RingNew.wav", (await workspace.Settings.LoadAsync()).Options.Devices.WindowsRingtoneFile);
            var customFile = Path.Combine(media, "Eigene Ansage.wav");
            var channels = scale == 1F ? 2 : 1;
            using (var wave = new WaveWriter(customFile, new WaveFormat(44100, 16, channels)))
                wave.Write(new byte[256], 0, 256);
            await page.SelectCustomRingtoneAsync(customFile);
            var info = Field<LabelControl>(page, "customRingtoneInfo");
            Assert.Contains("Hz", info.Text);
            Assert.Contains(channels == 1 ? "Mono" : "Stereo", info.Text);
            await page.PreviewRingtoneAsync();
            Assert.AreEqual(44100, output.SampleRate);
            Assert.AreEqual(channels, output.Channels);
            await page.SaveAsync(CancellationToken.None);
            await page.ReloadAsync();
            var saved = (await workspace.Settings.LoadAsync()).Options.Devices;
            Assert.AreEqual(customFile, saved.CustomRingtonePath);
            Assert.IsNull(saved.WindowsRingtoneFile);
            Assert.Contains(channels == 1 ? "Mono" : "Stereo", info.Text);
            await page.PreviewRingtoneAsync();
            dialog.MinimumSize = Size.Empty;
            dialog.ClientSize = new Size(680, 870);
            if (scale != 1) dialog.Scale(new SizeF(scale, scale));
            dialog.PerformLayout();
            Application.DoEvents();
            CheckTableBounds(dialog);
            Assert.AreEqual(Field<LookUpEdit>(page, "ringDeviceEdit").Left, ringtone.Left);
            Assert.IsGreaterThan(Field<ProgressBarControl>(page, "ringPeak").Width, ringtone.Width);
            Assert.IsGreaterThan(0, Field<ProgressBarControl>(page, "ringPeak").Height);
            Assert.IsLessThanOrEqualTo(button.Left, ringtone.Right);
            Assert.AreEqual(Field<SimpleButton>(page, "browseRingtoneButton").Width, button.Width);
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

    [TestMethod]
    public Task SettingsDeviceMetersReleaseWhenLeavingTheAudioTab() => WithWorkspace(async (workspace, _, _) =>
    {
        var catalog = new AnnouncementCatalogDouble();
        using var dialog = new FrmSettings(workspace);
        dialog.PrepareForBackgroundRun();
        dialog.ConfigureAudioDevices(catalog);
        await dialog.PrepareAsync();
        dialog.Show();
        var page = Field<SettingsPage>(dialog, "settingsPage");
        Assert.AreEqual(0, catalog.OpenMeters);
        dialog.SelectAutomationTab("settings-audio");
        await AppComTelephonyTests.Until(() => Field<ProgressBarControl>(page, "capturePeak").Position == 34);
        Assert.AreEqual(67, Field<ProgressBarControl>(page, "playbackPeak").Position);
        Assert.AreEqual(67, Field<ProgressBarControl>(page, "ringPeak").Position);
        Assert.AreEqual(3, catalog.OpenMeters);
        dialog.SelectAutomationTab("settings-audio-levels");
        Assert.AreEqual(0, catalog.OpenMeters);
        Assert.AreEqual(0, Field<ProgressBarControl>(page, "ringPeak").Position);
        await dialog.StopAsync();
    });

    [TestMethod]
    public Task SettingsDetectRemovedAndReturnedDevicesWithoutReplacingTheDraft() => WithWorkspace(async (workspace, _, _) =>
    {
        var catalog = new ChangingAudioCatalog();
        using var dialog = new FrmSettings(workspace);
        dialog.PrepareForBackgroundRun();
        dialog.ConfigureAudioDevices(catalog);
        await dialog.PrepareAsync();
        dialog.SelectAutomationTab("settings-audio");
        dialog.Show();
        var page = Field<SettingsPage>(dialog, "settingsPage");
        var capture = Field<LookUpEdit>(page, "captureDeviceEdit");
        capture.EditValue = "test-mic";
        Assert.AreEqual("USB-Mikrofon", capture.Text);
        var notice = page.LastSettingsNotice;
        catalog.Devices = new([], []);
        await Task.Run(catalog.Notify);
        await AppComTelephonyTests.Until(() => capture.Text == "Gerät nicht verfügbar");
        Assert.AreEqual("test-mic", capture.EditValue);
        catalog.Devices = new([new("test-mic", "USB-Mikrofon wieder da")], []);
        await Task.Run(catalog.Notify);
        await AppComTelephonyTests.Until(() => capture.Text == "USB-Mikrofon wieder da");
        Assert.AreEqual(notice, page.LastSettingsNotice);
        Assert.IsNull((await workspace.Settings.LoadAsync()).Options.Devices.CaptureDeviceId);
        await dialog.StopAsync();
        Assert.IsFalse(catalog.IsSubscribed);
    });

    private sealed class ChangingAudioCatalog : IAudioDeviceCatalog, IAudioDeviceChanges
    {
        private Action? _changed;
        internal AudioDeviceCatalog Devices { get; set; } = new([new("test-mic", "USB-Mikrofon")], []);
        internal bool IsSubscribed => _changed is not null;
        internal void Notify() => _changed?.Invoke();

        public Task<AudioDeviceCatalog> ReadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Devices);

        public IDisposable Subscribe(Action onChanged)
        {
            _changed = onChanged;
            return new Subscription(this);
        }

        private sealed class Subscription(ChangingAudioCatalog owner) : IDisposable
        {
            public void Dispose() => owner._changed = null;
        }
    }

    private sealed class RingtoneSettingsOutput : IHistoryRecordingOutput
    {
        internal int SampleRate { get; private set; }
        internal int Volume { get; private set; }
        internal int Channels { get; private set; }
        internal TaskCompletionSource<AudioPlaybackVolume>? Started { get; set; }

        public void Play(IWaveSource source, AudioDeviceSettings settings, CancellationToken token)
        {
            SampleRate = source.WaveFormat.SampleRate;
            Channels = source.WaveFormat.Channels;
            Volume = settings.PlaybackVolumePercent;
        }

        public void Play(IWaveSource source, AudioDeviceSettings settings, AudioPlaybackVolume volume, CancellationToken token)
        {
            Play(source, settings with { PlaybackVolumePercent = volume.Percent }, token);
            if (Started is not { } started) return;
            started.TrySetResult(volume);
            token.WaitHandle.WaitOne();
            token.ThrowIfCancellationRequested();
        }
    }
}

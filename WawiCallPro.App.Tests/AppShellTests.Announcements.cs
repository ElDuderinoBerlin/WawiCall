using DevExpress.LookAndFeel;
using DevExpress.Utils;
using DevExpress.XtraEditors;
using WawiCallPro.App.Diagnostics;
using WawiCallPro.App.Hosting;
using WawiCallPro.App.Views;
using WawiCallPro.Audio;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    [DataRow("Basic", 10F)]
    [DataRow("Office 2019 Black", 16F)]
    public Task AnnouncementsRecordPreviewExportAndRestorePreferences(string skin, float points) => WithWorkspace(async (workspace, _, _) =>
    {
        var oldSkin = UserLookAndFeel.Default.ActiveSkinName;
        var oldPalette = UserLookAndFeel.Default.ActiveSvgPaletteName;
        var oldFont = WindowsFormsSettings.DefaultFont;
        var oldAppearance = AppearanceObject.DefaultFont;
        var snapshot = await workspace.Settings.LoadAsync();
        await workspace.Settings.SaveAsync(snapshot.Options with
        {
            Devices = snapshot.Options.Devices with
            {
                CaptureDeviceId = "call-capture",
                PlaybackDeviceId = "call-playback",
                Announcements = new() { VoiceId = "missing-voice", CaptureDeviceId = "missing-microphone" }
            }
        }, snapshot);
        var audio = new AnnouncementAudioDouble();
        var speech = new AnnouncementSpeechDouble();
        var conversion = new AnnouncementConversionDouble();
        var catalog = new AnnouncementCatalogDouble();
        var output = Path.Combine(RenderOutput(), "announcements", workspace.Application.ProductLine);
        Directory.CreateDirectory(output);
        string? draft = null;
        try
        {
            AppUiStartup.Apply();
            UserLookAndFeel.Default.SetSkinStyle(skin);
            using var font = new Font("Segoe UI", points);
            WindowsFormsSettings.DefaultFont = AppearanceObject.DefaultFont = font;
            using (var dialog = new FrmAnnouncements(audio, speech, conversion, catalog, workspace.Settings, null))
            {
                dialog.PrepareForBackgroundRun();
                dialog.Font = font;
                await dialog.LoadAsync();
                dialog.Show();
                Application.DoEvents();
                var capture = Field<LookUpEdit>(dialog, "captureEdit");
                var playback = Field<LookUpEdit>(dialog, "playbackEdit");
                var voice = Field<LookUpEdit>(dialog, "voiceEdit");
                Assert.AreEqual("missing-microphone", capture.EditValue);
                Assert.AreEqual("missing-voice", voice.EditValue);
                Assert.Contains("Nicht verfügbar", capture.Text);
                capture.EditValue = "mic";
                playback.EditValue = "speaker";
                voice.EditValue = "voice";
                await dialog.SavePreferencesAsync();
                var saved = (await workspace.Settings.LoadAsync()).Options.Devices;
                Assert.AreEqual(new AnnouncementAudioSettings { CaptureDeviceId = "mic", PlaybackDeviceId = "speaker", VoiceId = "voice" }, saved.Announcements);
                Assert.AreEqual("call-capture", saved.CaptureDeviceId);
                Assert.AreEqual("call-playback", saved.PlaybackDeviceId);
                var recording = dialog.RecordAsync();
                Assert.AreEqual("mic", audio.RecordedDevice);
                Assert.IsFalse(recording.IsCompleted);
                dialog.StopAction();
                await recording;
                await dialog.PreviewAsync();
                Assert.AreEqual("speaker", audio.PreviewDevice);
                draft = audio.Draft;
                Assert.IsTrue(File.Exists(draft));
                await dialog.ExportAsync(Path.Combine(output, "fixture-export.wav"));
                Assert.AreEqual(draft, conversion.ExportedSource);
                Field<MemoEdit>(dialog, "textEdit").Text = "Probeansage";
                await dialog.SynthesizeAsync();
                Assert.IsFalse(File.Exists(draft));
                Assert.AreEqual("voice", speech.SelectedVoice);
                await dialog.PreviewAsync();
                draft = audio.Draft;
                dialog.ClientSize = new Size(665, 440);
                if (points == 10F) dialog.Scale(new SizeF(1.5F, 1.5F));
                dialog.PerformLayout();
                CheckTableBounds(dialog);
                using var bitmap = UiAudit.Capture(dialog);
                bitmap.Save(Path.Combine(output, $"announcements-{skin}-{points:0}.png"));
            }
            Assert.IsFalse(File.Exists(draft));
            using var reopened = new FrmAnnouncements(audio, speech, conversion, catalog, workspace.Settings, null);
            await reopened.LoadAsync();
            Assert.AreEqual("mic", Field<LookUpEdit>(reopened, "captureEdit").EditValue);
            Assert.AreEqual("speaker", Field<LookUpEdit>(reopened, "playbackEdit").EditValue);
            Assert.AreEqual("voice", Field<LookUpEdit>(reopened, "voiceEdit").EditValue);
            var active = reopened.RecordAsync();
            await reopened.StopAsync();
            Assert.IsTrue(active.IsCompletedSuccessfully);
            Assert.IsTrue(audio.Aborted);
        }
        finally
        {
            UserLookAndFeel.Default.SetSkinStyle(oldSkin, oldPalette);
            WindowsFormsSettings.DefaultFont = oldFont;
            AppearanceObject.DefaultFont = oldAppearance;
        }
    });

    private sealed class AnnouncementCatalogDouble : IAudioDeviceCatalog
    {
        public Task<AudioDeviceCatalog> ReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new AudioDeviceCatalog([new("mic", "USB-Mikrofon")], [new("speaker", "Headset")]));
    }

    private sealed class AnnouncementAudioDouble : IAnnouncementAudio
    {
        internal string? RecordedDevice;
        internal string? PreviewDevice;
        internal string? Draft;
        internal bool Aborted;

        public async Task RecordAsync(string? deviceId, string destination, CancellationToken finish, CancellationToken abort)
        {
            RecordedDevice = deviceId;
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(finish, abort);
            try { await Task.Delay(Timeout.InfiniteTimeSpan, linked.Token); }
            catch (OperationCanceledException) when (finish.IsCancellationRequested && !abort.IsCancellationRequested) { }
            catch (OperationCanceledException) when (abort.IsCancellationRequested) { Aborted = true; throw; }
            await File.WriteAllBytesAsync(destination, [1, 2, 3], abort);
        }

        public Task PreviewAsync(string path, AudioDeviceSettings settings, CancellationToken token)
        {
            PreviewDevice = settings.PlaybackDeviceId;
            Draft = path;
            Assert.IsTrue(File.Exists(path));
            return Task.CompletedTask;
        }
    }

    private sealed class AnnouncementSpeechDouble : IAnnouncementSpeech
    {
        private static readonly AnnouncementVoice Voice = new("voice", "Teststimme", "de-DE");
        internal string? SelectedVoice;
        public IReadOnlyList<AnnouncementVoice> GetVoices() => [Voice];
        public async Task<AnnouncementVoice> WriteWaveAsync(string text, string? voiceId, string destination, CancellationToken token)
        {
            SelectedVoice = voiceId;
            await File.WriteAllBytesAsync(destination, [4, 5, 6], token);
            return Voice;
        }
    }

    private sealed class AnnouncementConversionDouble : IAudioFileConversion
    {
        internal string? ExportedSource;
        public Task ConvertAsync(string source, string destination, AudioWaveOptions options, IProgress<int>? progress, CancellationToken token)
        {
            Assert.IsTrue(File.Exists(source));
            ExportedSource = source;
            return Task.CompletedTask;
        }
        public Task PreviewAsync(string path, CancellationToken token) => throw new AssertFailedException();
    }
}

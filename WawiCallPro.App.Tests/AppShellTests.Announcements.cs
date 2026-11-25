using CSCore.CoreAudioAPI;
using DevExpress.LookAndFeel;
using DevExpress.Utils;
using DevExpress.XtraEditors;
using Microsoft.Extensions.DependencyInjection;
using Quantarix.DevExpressExtensions.Controls;
using WawiCallPro.App.Diagnostics;
using WawiCallPro.App.Hosting;
using WawiCallPro.App.Views;
using WawiCallPro.Audio;
using WawiCallPro.Core.Audio;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Diagnostics;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    public Task AnnouncementsCaptureAndPreviewOnSelectedWasapiEndpoints() => WithWorkspace(async (workspace, services, _) =>
    {
        var captureName = Environment.GetEnvironmentVariable("WCP_ANNOUNCEMENT_CAPTURE_DEVICE");
        var playbackName = Environment.GetEnvironmentVariable("WCP_ANNOUNCEMENT_PLAYBACK_DEVICE");
        if (string.IsNullOrWhiteSpace(captureName) || string.IsNullOrWhiteSpace(playbackName))
            Assert.Inconclusive("Set both WCP_ANNOUNCEMENT device variables to opt in to microphone capture and audible playback.");

        var catalog = new WindowsAudioDeviceCatalog(new StartupExecutionMode(false));
        var devices = await catalog.ReadAsync();
        var microphone = FindEndpoint(devices.Capture, captureName);
        var headphones = FindEndpoint(devices.Playback, playbackName);
        Console.WriteLine($"{workspace.Application.ProductLine}: {microphone.DisplayName} → {headphones.DisplayName}");
        using var endpoints = new MMDeviceEnumerator();
        using var captureEndpoint = endpoints.GetDevice(microphone.Id);
        using var captureVolume = AudioEndpointVolume.FromDevice(captureEndpoint);
        using var renderEndpoint = endpoints.GetDevice(headphones.Id);
        using var renderVolume = AudioEndpointVolume.FromDevice(renderEndpoint);
        Console.WriteLine($"Windows-Endpunkt stumm: Mikrofon={captureVolume.IsMuted}, Kopfhörer={renderVolume.IsMuted}.");

        var original = await workspace.Settings.LoadAsync();
        await workspace.Settings.SaveAsync(original.Options with
        {
            Devices = original.Options.Devices with { PlaybackVolumePercent = 35 }
        }, original);
        var conversion = services.GetRequiredService<IAudioFileConversion>();
        var audio = services.GetRequiredService<IAnnouncementAudio>();
        var speech = services.GetRequiredService<IAnnouncementSpeech>();
        var silentCaptures = 0;
        var maximumCapturePeak = 0;
        var maximumPlaybackPeak = 0;
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            string? draft;
            using (var dialog = new FrmAnnouncements(audio, speech, conversion, catalog, workspace.Settings, null))
            {
                dialog.PrepareForBackgroundRun();
                await dialog.LoadAsync();
                dialog.Show();
                var capture = Field<LookUpEdit>(dialog, "captureEdit");
                var playback = Field<LookUpEdit>(dialog, "playbackEdit");
                if (attempt == 1)
                {
                    capture.EditValue = microphone.Id;
                    playback.EditValue = headphones.Id;
                    await dialog.SavePreferencesAsync();
                }
                Assert.AreEqual(microphone.Id, capture.EditValue);
                Assert.AreEqual(headphones.Id, playback.EditValue);

                var recording = dialog.RecordAsync();
                var capturePeak = 0;
                try { capturePeak = await ObservePeakUntilAsync(Field<AudioLevelMeter>(dialog, "capturePeak"), Task.Delay(TimeSpan.FromSeconds(6))); }
                finally { dialog.StopAction(); }
                await recording.WaitAsync(TimeSpan.FromSeconds(12));
                var message = Field<LabelControl>(dialog, "statusLabel");
                draft = Field<string?>(dialog, "_draft");
                Assert.IsNotNull(draft, message.Text);
                using (var wave = File.OpenRead(draft))
                {
                    var header = PcmWaveHeader.Read(wave, default);
                    Assert.AreEqual(1, header.Channels);
                    var samples = header.DataLength / sizeof(short);
                    Assert.IsTrue((double)samples / header.SampleRate is > 4 and < 8);
                    wave.Position = header.DataOffset;
                    using var reader = new BinaryReader(wave);
                    var min = 0;
                    var max = 0;
                    for (long sample = 0; sample < samples; sample++)
                    {
                        int value = reader.ReadInt16();
                        min = Math.Min(min, value);
                        max = Math.Max(max, value);
                    }
                    Console.WriteLine($"Aufnahme {attempt}: {samples / (double)header.SampleRate:F2} s, {header.SampleRate} Hz, PCM-Spanne {min} bis {max}.");
                    if (min == max) silentCaptures++;
                }

                var elapsed = System.Diagnostics.Stopwatch.StartNew();
                var playbackPeak = await ObservePeakUntilAsync(Field<AudioLevelMeter>(dialog, "playbackPeak"),
                    dialog.PreviewAsync().WaitAsync(TimeSpan.FromSeconds(12)));
                Assert.StartsWith("Wiedergabe beendet. Windows:", message.Text);
                Assert.IsGreaterThan(TimeSpan.FromSeconds(4), elapsed.Elapsed);
                maximumCapturePeak = Math.Max(maximumCapturePeak, capturePeak);
                maximumPlaybackPeak = Math.Max(maximumPlaybackPeak, playbackPeak);
                Console.WriteLine($"Pegel {attempt}: Mikrofon {capturePeak} %, Ausgabe {playbackPeak} %. "
                    + $"Vorhören nach {elapsed.Elapsed.TotalSeconds:F2} s: {message.Text}");
            }
            Assert.IsFalse(File.Exists(draft), "Closing the dialog must remove the recorded draft.");
        }
        Assert.IsLessThan(2, silentCaptures, "The microphone delivered no changing signal in either take.");
        Assert.IsGreaterThan(0, maximumCapturePeak, "The dialog did not register a live microphone peak.");
        Assert.IsGreaterThan(0, maximumPlaybackPeak, "The dialog did not register a live playback peak.");
    }, configure: services => services.AddAudioConversion());

    private static async Task<int> ObservePeakUntilAsync(AudioLevelMeter meter, Task operation)
    {
        var peak = meter.Position;
        while (!operation.IsCompleted)
        {
            await Task.WhenAny(operation, Task.Delay(40));
            peak = Math.Max(peak, meter.Position);
        }
        await operation;
        return peak;
    }

    private static AudioDeviceInfo FindEndpoint(IEnumerable<AudioDeviceInfo> devices, string name) =>
        devices.Single(device => string.Equals(device.DisplayName, name, StringComparison.OrdinalIgnoreCase));

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
                Assert.Contains("Windows: 0 %, stumm.", Field<LabelControl>(dialog, "statusLabel").Text);
                draft = audio.Draft;
                Assert.IsTrue(File.Exists(draft));
                await dialog.ExportAsync(Path.Combine(output, "fixture-export.wav"));
                Assert.AreEqual(draft, conversion.ExportedSource);
                Field<MemoEdit>(dialog, "textEdit").Text = "Probeansage";
                await dialog.SynthesizeAsync();
                Assert.IsFalse(File.Exists(draft));
                Assert.AreEqual("voice", speech.SelectedVoice);
                await dialog.PreviewAsync();
                await AppComTelephonyTests.Until(() => Field<AudioLevelMeter>(dialog, "capturePeak").Position == 34);
                Assert.AreEqual(67, Field<AudioLevelMeter>(dialog, "playbackPeak").Position);
                draft = audio.Draft;
                dialog.ClientSize = new Size(665, 440);
                if (points == 10F) dialog.Scale(new SizeF(1.5F, 1.5F));
                dialog.PerformLayout();
                CheckTableBounds(dialog);
                var editor = Field<MemoEdit>(dialog, "textEdit");
                Assert.IsGreaterThanOrEqualTo(editor.Font.Height * 2, editor.Height);
                using var bitmap = UiAudit.Capture(dialog);
                bitmap.Save(Path.Combine(output, $"announcements-{skin}-{points:0}.png"));
            }
            Assert.IsFalse(File.Exists(draft));
            Assert.AreEqual(0, catalog.OpenMeters);
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

    private sealed class AnnouncementCatalogDouble : IAudioDeviceCatalog, IAudioEndpointMeterFactory
    {
        internal int OpenMeters { get; private set; }

        public Task<AudioDeviceCatalog> ReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new AudioDeviceCatalog([new("mic", "USB-Mikrofon")], [new("speaker", "Headset")]));

        public IAudioEndpointMeter? Open(AudioEndpointDirection direction, string? deviceId)
        {
            if (deviceId == "missing-microphone") return null;
            OpenMeters++;
            var reading = direction == AudioEndpointDirection.Capture ? new AudioEndpointReading(34, 70, false) : new(67, 0, true);
            return new Meter(reading, () => OpenMeters--);
        }

        private sealed class Meter(AudioEndpointReading reading, Action close) : IAudioEndpointMeter
        {
            public AudioEndpointReading Read() => reading;
            public void Dispose() => close();
        }
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

    [TestMethod]
    public Task AnnouncementSpeechProviderSwitchesVoicesAndRestoresSavedSelection() => WithWorkspace(async (workspace, _, _) =>
    {
        var windows = new AnnouncementSpeechDouble();
        var azure = new AnnouncementSpeechDouble { Voice = new("azure-voice", "Azure-Teststimme", "de-DE") };
        var google = new AnnouncementSpeechDouble { Voice = new("google-voice", "Google-Teststimme", "de-DE") };
        var audio = new AnnouncementAudioDouble();
        var conversion = new AnnouncementConversionDouble();
        var catalog = new AnnouncementCatalogDouble();
        IAnnouncementSpeech? Resolve(AnnouncementSpeechProvider provider) => provider switch
        {
            AnnouncementSpeechProvider.AzureSpeech => azure,
            AnnouncementSpeechProvider.GoogleCloud => google,
            _ => null
        };

        using (var dialog = new FrmAnnouncements(audio, windows, conversion, catalog, workspace.Settings, null)
               { ResolveCloudSpeech = Resolve })
        {
            dialog.PrepareForBackgroundRun();
            await dialog.LoadAsync();
            dialog.Show();
            var provider = Field<LookUpEdit>(dialog, "providerEdit");
            var voice = Field<LookUpEdit>(dialog, "voiceEdit");
            var text = Field<MemoEdit>(dialog, "textEdit");
            var speech = Field<SimpleButton>(dialog, "speechButton");
            text.Text = "Deutsche Ansage für den Hörvergleich.";
            Assert.AreEqual(AnnouncementSpeechProvider.Windows, provider.EditValue);
            voice.EditValue = "voice";

            provider.EditValue = AnnouncementSpeechProvider.AzureSpeech;
            await AppComTelephonyTests.Until(() => speech.Enabled);
            Assert.AreEqual("", voice.EditValue);
            Assert.IsNull(voice.Properties.GetDataSourceRowByKeyValue("voice"));
            voice.EditValue = "azure-voice";
            await dialog.SynthesizeAsync();
            Assert.AreEqual("azure-voice", azure.SelectedVoice);

            provider.EditValue = AnnouncementSpeechProvider.GoogleCloud;
            await AppComTelephonyTests.Until(() => speech.Enabled);
            Assert.IsNull(voice.Properties.GetDataSourceRowByKeyValue("azure-voice"));
            voice.EditValue = "google-voice";
            await dialog.SynthesizeAsync();
            Assert.AreEqual("google-voice", google.SelectedVoice);
            Assert.Contains("Ansagetext", Field<LabelControl>(dialog, "providerHint").Text);

            provider.EditValue = AnnouncementSpeechProvider.Windows;
            Assert.AreEqual("voice", voice.EditValue);
            provider.EditValue = AnnouncementSpeechProvider.GoogleCloud;
            await AppComTelephonyTests.Until(() => speech.Enabled);
            Assert.AreEqual("google-voice", voice.EditValue);
            await dialog.SavePreferencesAsync();
            Assert.AreEqual("Deutsche Ansage für den Hörvergleich.", text.Text);
            Assert.IsNull(windows.SelectedVoice);
            dialog.ClientSize = new Size(665, 440);
            dialog.PerformLayout();
            CheckTableBounds(dialog);
            var output = Path.Combine(RenderOutput(), "announcements", workspace.Application.ProductLine);
            Directory.CreateDirectory(output);
            using var bitmap = UiAudit.Capture(dialog);
            bitmap.Save(Path.Combine(output, "announcements-google.png"));
        }

        using var reopened = new FrmAnnouncements(audio, windows, conversion, catalog, workspace.Settings, null);
        await reopened.LoadAsync();
        Assert.AreEqual(AnnouncementSpeechProvider.GoogleCloud, Field<LookUpEdit>(reopened, "providerEdit").EditValue);
        Assert.AreEqual("google-voice", Field<LookUpEdit>(reopened, "voiceEdit").EditValue);
        Field<MemoEdit>(reopened, "textEdit").Text = "Ansage";
        Assert.IsFalse(Field<SimpleButton>(reopened, "speechButton").Enabled);
        Assert.Contains("WAWICALLPRO_GOOGLE_TTS_PROJECT", Field<LabelControl>(reopened, "providerHint").Text);
    });

    private sealed class AnnouncementSpeechDouble : IAnnouncementSpeech
    {
        internal AnnouncementVoice Voice { get; init; } = new("voice", "Teststimme", "de-DE");
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

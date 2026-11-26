using DevExpress.XtraEditors;
using Microsoft.Extensions.DependencyInjection;
using Quantarix.DevExpressExtensions.Controls;
using WawiCallPro.App.Hosting;
using WawiCallPro.App.Views;
using WawiCallPro.Audio;
using WawiCallPro.Core.Audio;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Diagnostics;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    public Task ExistingCloudAnnouncementPlaysAndExportsWithoutCloudServices() => RunStaAsync(async () =>
    {
        var source = Environment.GetEnvironmentVariable("WCP_OFFLINE_ANNOUNCEMENT_SOURCE");
        var output = Environment.GetEnvironmentVariable("WCP_OFFLINE_ANNOUNCEMENT_EXPORT");
        var playbackName = Environment.GetEnvironmentVariable("WCP_ANNOUNCEMENT_PLAYBACK_DEVICE");
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(output) || string.IsNullOrWhiteSpace(playbackName))
            Assert.Inconclusive("Supply the existing Azure WAV, a new export path and a WASAPI playback device to opt in.");

        Assert.IsTrue(File.Exists(source));
        Assert.IsFalse(File.Exists(output), "Choose a fresh path for the test export.");
        var profile = Directory.CreateTempSubdirectory();
        try
        {
            using var context = new WindowsFormsSynchronizationContext();
            using var exceptions = new AppExceptionBoundary(new MaintenanceCrashReporter());
            var mode = new StartupExecutionMode(SuppressNetwork: true)
                { IsolatedProfile = true, DocumentationProfileDirectory = profile.FullName };
            var host = AppCompositionRoot.Build(context, exceptions, mode, new MaintenanceInstanceGuard());
            await using var lifetime = (IAsyncDisposable)host;
            Assert.IsNull(host.Services.GetService<Func<AnnouncementSpeechProvider, IAnnouncementSpeech?>>());

            // The startup mode also suppresses hardware enumeration. Allow only the local audio catalog for this test.
            var catalog = new WindowsAudioDeviceCatalog(new StartupExecutionMode(SuppressNetwork: false));
            var playback = FindEndpoint((await catalog.ReadAsync()).Playback, playbackName);
            var store = host.Services.GetRequiredService<ConfigurationStore>();
            var fixture = new ExistingAzureWave(source);
            var voice = fixture.GetVoices().Single();
            await store.SaveAsync(new ApplicationOptions
            {
                Devices = new AudioDeviceSettings
                {
                    PlaybackVolumePercent = 25,
                    Announcements = new AnnouncementAudioSettings
                    {
                        SpeechProvider = AnnouncementSpeechProvider.AzureSpeech,
                        VoiceId = voice.Id,
                        PlaybackDeviceId = playback.Id
                    }
                }
            });

            using var dialog = new FrmAnnouncements(host.Services.GetRequiredService<IAnnouncementAudio>(),
                host.Services.GetRequiredService<IAnnouncementSpeech>(), host.Services.GetRequiredService<IAudioFileConversion>(),
                catalog, store, null)
            {
                ResolveCloudSpeech = provider => provider == AnnouncementSpeechProvider.AzureSpeech ? fixture : null
            };
            dialog.PrepareForBackgroundRun();
            await dialog.LoadAsync();
            dialog.Show();
            Assert.AreEqual(playback.Id, Field<LookUpEdit>(dialog, "playbackEdit").EditValue);
            Field<MemoEdit>(dialog, "textEdit").Text = "Vorhandenes Azure-Testaudio";
            await dialog.SynthesizeAsync();
            var status = Field<LabelControl>(dialog, "statusLabel");
            Assert.AreEqual("Ansage ist zum Abspielen und Speichern bereit.", status.Text);

            var timer = System.Diagnostics.Stopwatch.StartNew();
            var peak = await ObservePeakUntilAsync(Field<AudioLevelMeter>(dialog, "playbackPeak"),
                dialog.PreviewAsync().WaitAsync(TimeSpan.FromSeconds(20)));
            Assert.StartsWith("Wiedergabe beendet.", status.Text);
            Assert.IsGreaterThan(0, peak, "The WASAPI playback endpoint did not register a signal.");
            Console.WriteLine($"{playback.DisplayName}: Wiedergabe in {timer.Elapsed.TotalSeconds:F2} s, Spitzenpegel {peak} %. {status.Text}");

            await dialog.ExportAsync(output);
            Assert.AreEqual("WAV-Datei gespeichert.", status.Text);
            using var input = File.OpenRead(source);
            using var exported = File.OpenRead(output);
            var originalWave = PcmWaveHeader.Read(input, default);
            var exportedWave = PcmWaveHeader.Read(exported, default);
            Assert.AreEqual(1, exportedWave.Channels);
            Assert.AreEqual(originalWave.SampleRate, exportedWave.SampleRate);
            Assert.AreEqual(originalWave.DataLength, exportedWave.DataLength);
            input.Position = originalWave.DataOffset;
            exported.Position = exportedWave.DataOffset;
            using var originalSamples = new BinaryReader(input);
            using var exportedSamples = new BinaryReader(exported);
            var sampleCount = exportedWave.DataLength / sizeof(short);
            var maximumDifference = 0;
            for (long sample = 0; sample < sampleCount; sample++)
                maximumDifference = Math.Max(maximumDifference, Math.Abs(originalSamples.ReadInt16() - exportedSamples.ReadInt16()));
            // The converter uses a floating-point pipeline before encoding PCM16 again.
            Assert.IsLessThanOrEqualTo(1, maximumDifference);
            Console.WriteLine($"Export: {exportedWave.SampleRate} Hz, Mono, {sampleCount} Samples, max. Abweichung {maximumDifference} PCM-Stufe.");
        }
        finally { profile.Delete(recursive: true); }
    });

    private sealed class ExistingAzureWave(string source) : IAnnouncementSpeech
    {
        private static readonly AnnouncementVoice Voice = new("de-DE-ConradNeural", "Conrad", "de-DE");

        public IReadOnlyList<AnnouncementVoice> GetVoices() => [Voice];

        public async Task<AnnouncementVoice> WriteWaveAsync(string text, string? voiceId, string destination, CancellationToken token)
        {
            await using var input = File.OpenRead(source);
            await using var output = File.Create(destination);
            await input.CopyToAsync(output, token);
            return Voice;
        }
    }
}

using Microsoft.Extensions.DependencyInjection;
using WawiCallPro.App.Hosting;
using WawiCallPro.Audio;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Diagnostics;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    public Task DesktopCloudSpeechResolverEnforcesSavedMonthlyBudgets() => RunStaAsync(async () =>
    {
        var profile = Directory.CreateTempSubdirectory();
        try
        {
            using var context = new WindowsFormsSynchronizationContext();
            using var exceptions = new AppExceptionBoundary(new MaintenanceCrashReporter());
            var mode = new StartupExecutionMode(SuppressNetwork: false)
                { IsolatedProfile = true, DocumentationProfileDirectory = profile.FullName };
            var azure = new QuotaTestSpeech("de-DE-KatjaNeural");
            var google = new QuotaTestSpeech("de-DE-Neural2-G");
            var host = AppCompositionRoot.Build(context, exceptions, mode, new MaintenanceInstanceGuard(),
                null, null, services =>
                {
                    services.AddKeyedSingleton<IAnnouncementSpeech>(CloudAnnouncementSpeechServices.AzureSpeech, azure);
                    services.AddKeyedSingleton<IAnnouncementSpeech>(CloudAnnouncementSpeechServices.GoogleCloud, google);
                });
            await using var hostLifetime = (IAsyncDisposable)host;

            var quota = host.Services.GetRequiredService<AnnouncementCloudQuota>();
            var settings = host.Services.GetRequiredService<ConfigurationStore>();
            var resolve = host.Services.GetRequiredService<Func<AnnouncementSpeechProvider, IAnnouncementSpeech?>>();
            var destination = Path.Combine(profile.FullName, "announcement.wav");
            Assert.IsNull(resolve(AnnouncementSpeechProvider.Windows));
            foreach (var (provider, adapter) in new[]
                     {
                         (AnnouncementSpeechProvider.AzureSpeech, azure),
                         (AnnouncementSpeechProvider.GoogleCloud, google)
                     })
            {
                var speech = resolve(provider)!;
                await Assert.ThrowsAsync<AnnouncementSpeechException>(() => speech.WriteWaveAsync("Hallo", null, destination, default));
                Assert.AreEqual(0, adapter.Calls);
            }
            Assert.AreEqual((0, 0, 0), quota.Read());

            await settings.SaveAsync(new ApplicationOptions
            {
                Devices = new AudioDeviceSettings
                {
                    Announcements = new AnnouncementAudioSettings
                    {
                        Cloud = new AnnouncementCloudSettings { AzureMonthlyAllowance = 5, GoogleNeural2MonthlyAllowance = 6 }
                    }
                }
            });

            var azureSpeech = resolve(AnnouncementSpeechProvider.AzureSpeech)!;
            var googleSpeech = resolve(AnnouncementSpeechProvider.GoogleCloud)!;
            Assert.AreEqual("de-DE-KatjaNeural", (await azureSpeech.WriteWaveAsync("Hallo", null, destination, default)).Id);
            Assert.AreEqual("de-DE-Neural2-G", (await googleSpeech.WriteWaveAsync("Grüß", null, destination, default)).Id);
            Assert.AreEqual((5, 6, 0), quota.Read());

            await Assert.ThrowsAsync<AnnouncementSpeechException>(() => azureSpeech.WriteWaveAsync("a", null, destination, default));
            await Assert.ThrowsAsync<AnnouncementSpeechException>(() => googleSpeech.WriteWaveAsync("a", null, destination, default));
            Assert.AreEqual(1, azure.Calls);
            Assert.AreEqual(1, google.Calls);
            Assert.AreEqual((5, 6, 0), quota.Read());
        }
        finally { profile.Delete(recursive: true); }
    });

    private sealed class QuotaTestSpeech(string voiceId) : IAnnouncementSpeech
    {
        private readonly AnnouncementVoice _voice = new(voiceId, voiceId, "de-DE");

        public int Calls { get; private set; }

        public IReadOnlyList<AnnouncementVoice> GetVoices() => [_voice];

        public Task<AnnouncementVoice> WriteWaveAsync(string text, string? voiceId, string destination, CancellationToken token)
        {
            Calls++;
            return Task.FromResult(_voice);
        }
    }
}

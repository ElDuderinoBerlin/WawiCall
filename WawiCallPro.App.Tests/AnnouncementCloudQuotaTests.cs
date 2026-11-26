using System.IO;
using WawiCallPro.App.Hosting;
using WawiCallPro.Audio;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.App.Tests;

[TestClass]
public sealed class AnnouncementCloudQuotaTests
{
    [TestMethod]
    public async Task BlockedRequestsDoNotCallProviderAndFailedRequestsRetainTheirReservation()
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            var file = Path.Combine(directory.FullName, "quota.json");
            var clock = new TestClock(new DateTimeOffset(2026, 11, 26, 12, 0, 0, TimeSpan.Zero));
            var quota = new AnnouncementCloudQuota(file, clock);
            var adapter = new CloudDouble();
            var allowance = new AnnouncementCloudSettings { GoogleNeural2MonthlyAllowance = 5, GoogleChirpMonthlyAllowance = 5 };
            var speech = new QuotaProtectedAnnouncementSpeech(adapter, quota, AnnouncementSpeechProvider.GoogleCloud, allowance);
            await Assert.ThrowsAsync<AnnouncementSpeechException>(() => speech.WriteWaveAsync("abcdef", null, "unwritten", default));
            Assert.AreEqual(0, adapter.Calls);
            await Assert.ThrowsAsync<InvalidOperationException>(() => speech.WriteWaveAsync("ä-b", null, "unwritten", default));
            Assert.AreEqual(1, adapter.Calls);

            var reopened = new AnnouncementCloudQuota(file, clock);
            var afterFailure = new QuotaProtectedAnnouncementSpeech(adapter, reopened, AnnouncementSpeechProvider.GoogleCloud, allowance);
            Assert.AreEqual((0, 4, 0), reopened.Read());
            await Assert.ThrowsAsync<AnnouncementSpeechException>(() => afterFailure.WriteWaveAsync("ab", null, "unwritten", default));
            Assert.AreEqual(1, adapter.Calls);
            await Assert.ThrowsAsync<InvalidOperationException>(() => afterFailure.WriteWaveAsync("abc", "de-DE-Chirp3-HD-Example", "unwritten", default));
            Assert.AreEqual((0, 4, 3), reopened.Read());
        }
        finally { directory.Delete(recursive: true); }
    }

    [TestMethod]
    public async Task UnreleasedCorruptAndConcurrentBudgetsFailClosed()
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            var file = Path.Combine(directory.FullName, "quota.json");
            var clock = new TestClock(new DateTimeOffset(2026, 11, 26, 12, 0, 0, TimeSpan.Zero));
            var quota = new AnnouncementCloudQuota(file, clock);
            Assert.Throws<AnnouncementSpeechException>(() =>
                quota.Reserve(AnnouncementSpeechProvider.AzureSpeech, new(), "azure", "a", default));
            var allowance = new AnnouncementCloudSettings { AzureMonthlyAllowance = 4 };
            async Task<bool> TryReserveAsync()
            {
                try
                {
                    await Task.Run(() => new AnnouncementCloudQuota(file, clock)
                        .Reserve(AnnouncementSpeechProvider.AzureSpeech, allowance, "azure", "abc", default));
                    return true;
                }
                catch (AnnouncementSpeechException) { return false; }
            }
            var attempts = await Task.WhenAll(TryReserveAsync(), TryReserveAsync());
            Assert.HasCount(1, attempts.Where(accepted => accepted).ToArray());
            Assert.AreEqual((3, 0, 0), quota.Read());
            await File.WriteAllTextAsync(file, "{ broken");
            Assert.Throws<AnnouncementSpeechException>(() =>
                quota.Reserve(AnnouncementSpeechProvider.AzureSpeech, allowance, "azure", "a", default));
        }
        finally { directory.Delete(recursive: true); }
    }

    [TestMethod]
    public void GoogleResetsAtPacificMonthBoundaryAndReservesRequestsAcrossIt()
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            var clock = new TestClock(new DateTimeOffset(2026, 12, 1, 7, 59, 45, TimeSpan.Zero));
            var quota = new AnnouncementCloudQuota(Path.Combine(directory.FullName, "quota.json"), clock);
            var allowance = new AnnouncementCloudSettings { GoogleNeural2MonthlyAllowance = 5 };
            quota.Reserve(AnnouncementSpeechProvider.GoogleCloud, allowance, "de-DE-Neural2-G", "ab", default);
            Assert.AreEqual((0, 2, 0), quota.Read());
            clock.Now = clock.Now.AddMinutes(1);
            Assert.AreEqual((0, 2, 0), quota.Read());
            clock.Now = clock.Now.AddMonths(1);
            Assert.AreEqual((0, 0, 0), quota.Read());
        }
        finally { directory.Delete(recursive: true); }
    }

    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class CloudDouble : IAnnouncementSpeech
    {
        public int Calls { get; private set; }
        public IReadOnlyList<AnnouncementVoice> GetVoices() =>
            [new("de-DE-Neural2-G", "Neural2", "de-DE"), new("de-DE-Chirp3-HD-Example", "Chirp", "de-DE")];
        public Task<AnnouncementVoice> WriteWaveAsync(string text, string? voiceId, string destination, CancellationToken token)
        {
            Calls++;
            throw new InvalidOperationException("Provider timed out.");
        }
    }
}

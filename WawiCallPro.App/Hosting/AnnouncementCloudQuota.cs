using System.Globalization;
using System.IO;
using System.Security;
using System.Text;
using System.Text.Json;
using WawiCallPro.Audio;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.App.Hosting;

internal sealed class AnnouncementCloudQuota(string file, TimeProvider clock)
{
    private static readonly TimeZoneInfo GoogleTime = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
    private static readonly string[] Categories = ["azure", "neural2", "chirp3"];

    internal (int Azure, int Neural2, int Chirp) Read()
    {
        var entries = ReadEntries();
        var now = clock.GetUtcNow();
        return (Used(entries, "azure", now), Used(entries, "neural2", now), Used(entries, "chirp3", now));
    }

    internal void Reserve(AnnouncementSpeechProvider provider, AnnouncementCloudSettings settings, string voiceId, string text,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var (category, limit) = (provider, voiceId) switch
        {
            (AnnouncementSpeechProvider.AzureSpeech, _) => ("azure", settings.AzureMonthlyAllowance),
            (AnnouncementSpeechProvider.GoogleCloud, _) when voiceId.Contains("-Neural2-", StringComparison.Ordinal) =>
                ("neural2", settings.GoogleNeural2MonthlyAllowance),
            (AnnouncementSpeechProvider.GoogleCloud, _) when voiceId.Contains("-Chirp3-HD-", StringComparison.Ordinal) =>
                ("chirp3", settings.GoogleChirpMonthlyAllowance),
            _ => throw new AnnouncementSpeechException("Für diese Cloud-Stimme ist kein Freikontingent hinterlegt.")
        };
        var chargedText = provider == AnnouncementSpeechProvider.AzureSpeech ? SecurityElement.Escape(text) ?? text : text;
        var units = Encoding.UTF8.GetByteCount(chargedText);
        if (limit <= 0) throw new AnnouncementSpeechException("Unter Einstellungen → Audio → Cloud-Ansagen zuerst ein Monatsbudget freigeben.");
        var maximum = provider == AnnouncementSpeechProvider.AzureSpeech ? AnnouncementCloudSettings.AzureFreeCharacters
            : AnnouncementCloudSettings.GoogleFreeCharactersPerVoiceFamily;
        if (limit > maximum) throw new AnnouncementSpeechException("Das lokale Monatsbudget liegt oberhalb des Freikontingents.");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            using var gate = new FileStream(file + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            var entries = ReadEntries();
            var now = clock.GetUtcNow();
            var currentMonth = Month(category, now);
            var possibleMonth = Month(category, now.AddMinutes(1));
            var months = currentMonth == possibleMonth ? new[] { currentMonth } : new[] { currentMonth, possibleMonth };
            foreach (var month in months)
            {
                var key = category + ":" + month;
                var used = entries.GetValueOrDefault(key);
                if (units > limit - used)
                    throw new AnnouncementSpeechException($"Cloud-Vertonung gesperrt. Für diese Stimme sind noch {Math.Max(0, limit - used):N0} Zeichen im lokalen Monatsbudget verfügbar.");
                entries[key] = used + units;
            }
            // Keep the adjacent months for provider boundaries and clock adjustments.
            var oldest = Month("azure", now.AddMonths(-2));
            foreach (var old in entries.Keys.Where(entry => string.CompareOrdinal(entry[(entry.IndexOf(':') + 1)..], oldest) < 0).ToArray())
                entries.Remove(old);
            token.ThrowIfCancellationRequested();
            WriteEntries(entries);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new AnnouncementSpeechException("Cloud-Vertonung gesperrt. Der lokale Monatsverbrauch konnte nicht gesichert werden.");
        }
    }

    private Dictionary<string, int> ReadEntries()
    {
        try
        {
            using var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            if (input.Length > 8192) throw new JsonException();
            var entries = JsonSerializer.Deserialize<Dictionary<string, int>>(input) ?? throw new JsonException();
            foreach (var (key, amount) in entries)
            {
                var separator = key.IndexOf(':');
                if (separator < 0 || !Categories.Contains(key[..separator])
                    || !DateOnly.TryParseExact(key[(separator + 1)..] + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
                    || amount is < 0 or > AnnouncementCloudSettings.GoogleFreeCharactersPerVoiceFamily)
                    throw new JsonException();
            }
            return entries;
        }
        catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException) { return []; }
    }

    private void WriteEntries(Dictionary<string, int> entries)
    {
        var temporary = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(output, entries);
                output.Flush(flushToDisk: true);
            }
            File.Move(temporary, file, overwrite: true);
        }
        finally { File.Delete(temporary); }
    }

    private static int Used(Dictionary<string, int> entries, string category, DateTimeOffset now) =>
        entries.GetValueOrDefault(category + ":" + Month(category, now));

    private static string Month(string category, DateTimeOffset now) =>
        (category == "azure" ? now : TimeZoneInfo.ConvertTime(now, GoogleTime)).ToString("yyyy-MM", CultureInfo.InvariantCulture);
}

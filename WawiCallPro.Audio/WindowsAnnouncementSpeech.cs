using Windows.Media.SpeechSynthesis;

namespace WawiCallPro.Audio;

public sealed record AnnouncementVoice(string Id, string DisplayName, string Language);

/// <summary>Generates a local PCM WAV from text using a voice offered by the configured provider.</summary>
public interface IAnnouncementSpeech
{
    IReadOnlyList<AnnouncementVoice> GetVoices();
    Task<AnnouncementVoice> WriteWaveAsync(string text, string? voiceId, string destination, CancellationToken token);
}

public sealed class AnnouncementSpeechException(string message) : Exception(message);

internal sealed class WindowsAnnouncementSpeech(IAudioFileConversion conversion) : IAnnouncementSpeech
{
    public IReadOnlyList<AnnouncementVoice> GetVoices() => SpeechSynthesizer.AllVoices
        .OrderBy(voice => voice.Language, StringComparer.OrdinalIgnoreCase)
        .ThenBy(voice => voice.DisplayName, StringComparer.CurrentCultureIgnoreCase)
        .Select(Describe).ToArray();

    public async Task<AnnouncementVoice> WriteWaveAsync(string text, string? voiceId, string destination, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (string.IsNullOrWhiteSpace(text))
            throw new AnnouncementSpeechException("Der Ansagetext ist leer.");
        if (!Path.IsPathFullyQualified(destination) || !Path.GetExtension(destination).Equals(".wav", StringComparison.OrdinalIgnoreCase))
            throw new AnnouncementSpeechException("Die Ausgabe benötigt einen absoluten Pfad zu einer WAV-Datei.");
        if (File.Exists(destination))
            throw new AnnouncementSpeechException("Die WAV-Datei existiert bereits.");

        token.ThrowIfCancellationRequested();
        using var synthesizer = new SpeechSynthesizer();
        if (voiceId is not null)
        {
            synthesizer.Voice = SpeechSynthesizer.AllVoices.FirstOrDefault(voice => voice.Id == voiceId)
                ?? throw new AnnouncementSpeechException("Die ausgewählte Stimme ist nicht installiert.");
        }
        var voice = Describe(synthesizer.Voice);
        using var speech = await synthesizer.SynthesizeTextToStreamAsync(text).AsTask(token).ConfigureAwait(false);
        using var source = speech.AsStreamForRead();
        var temporary = Path.Combine(Path.GetTempPath(), "WawiCallPro-speech-" + Guid.NewGuid().ToString("N") + ".wav");
        var created = false;
        try
        {
            await using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                created = true;
                await source.CopyToAsync(file, token).ConfigureAwait(false);
            }

            await conversion.ConvertAsync(temporary, destination, new AudioWaveOptions(null, 1, 100), null, token)
                .ConfigureAwait(false);
            return voice;
        }
        finally
        {
            if (created) File.Delete(temporary);
        }
    }

    private static AnnouncementVoice Describe(VoiceInformation voice) => new(voice.Id, voice.DisplayName, voice.Language);
}

using CSCore;
using CSCore.Codecs.WAV;

namespace WawiCallPro.Audio;

internal abstract class CloudAnnouncementSpeech(IReadOnlyList<AnnouncementVoice> voices) : IAnnouncementSpeech
{
    protected const int SampleRate = 16000;
    protected const string GermanLocale = "de-DE";
    private IReadOnlyList<AnnouncementVoice>? _loadedVoices;

    public IReadOnlyList<AnnouncementVoice> GetVoices() => _loadedVoices ?? voices;

    public async Task<IReadOnlyList<AnnouncementVoice>> LoadVoicesAsync(CancellationToken token)
    {
        if (_loadedVoices is { } cached) return cached;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(8));
        try
        {
            var catalog = await QueryVoicesAsync(deadline.Token).ConfigureAwait(false);
            _loadedVoices = Array.AsReadOnly(voices.Concat(catalog.OrderBy(voice => voice.DisplayName, StringComparer.OrdinalIgnoreCase))
                .DistinctBy(voice => voice.Id).ToArray());
            return _loadedVoices;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            token.ThrowIfCancellationRequested();
            throw new AnnouncementSpeechException("Stimmen konnten beim Anbieter nicht geladen werden. Zugang und Verbindung prüfen.");
        }
    }

    public async Task<AnnouncementVoice> WriteWaveAsync(string text, string? voiceId, string destination, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(text) || text.Length > 4000)
            throw new AnnouncementSpeechException("Der Ansagetext muss 1 bis 4000 Zeichen enthalten.");
        if (!Path.IsPathFullyQualified(destination) || !Path.GetExtension(destination).Equals(".wav", StringComparison.OrdinalIgnoreCase))
            throw new AnnouncementSpeechException("Die Ausgabe benötigt einen absoluten Pfad zu einer WAV-Datei.");
        if (File.Exists(destination))
            throw new AnnouncementSpeechException("Die WAV-Datei existiert bereits.");
        var selected = GetVoices();
        var voice = voiceId is null ? selected[0] : selected.FirstOrDefault(voice => voice.Id == voiceId);
        if (voice is null)
            voice = (await LoadVoicesAsync(token).ConfigureAwait(false)).FirstOrDefault(voice => voice.Id == voiceId)
                ?? throw new AnnouncementSpeechException("Die ausgewählte Cloud-Stimme ist nicht verfügbar.");

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        byte[] audio;
        try
        {
            audio = await SynthesizeAsync(text, voice, deadline.Token).ConfigureAwait(false);
            deadline.Token.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested && deadline.IsCancellationRequested)
        {
            throw new AnnouncementSpeechException("Die Cloud-Sprachsynthese hat das Zeitlimit überschritten.");
        }

        ValidateWave(audio);
        var temporary = Path.Combine(Path.GetDirectoryName(destination)!, $".announcement-{Guid.NewGuid():N}.tmp");
        var temporaryCreated = false;
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                temporaryCreated = true;
                await stream.WriteAsync(audio, token).ConfigureAwait(false);
            }
            token.ThrowIfCancellationRequested();
            File.Move(temporary, destination, overwrite: false);
            temporaryCreated = false;
        }
        finally
        {
            if (temporaryCreated) File.Delete(temporary);
        }
        return voice;
    }

    protected abstract Task<byte[]> SynthesizeAsync(string text, AnnouncementVoice voice, CancellationToken token);
    protected abstract Task<IEnumerable<AnnouncementVoice>> QueryVoicesAsync(CancellationToken token);

    private static void ValidateWave(byte[] audio)
    {
        try
        {
            using var stream = new MemoryStream(audio, writable: false);
            using var reader = new WaveFileReader(stream);
            if (reader.Length > 0 && reader.WaveFormat is
                { WaveFormatTag: AudioEncoding.Pcm, BitsPerSample: 16, Channels: 1, SampleRate: SampleRate })
                return;
        }
        catch (Exception exception) when (exception is IOException or ArgumentException or NotSupportedException) { }
        throw new AnnouncementSpeechException("Der Anbieter hat keine gültige Mono-PCM-WAV-Datei geliefert.");
    }
}

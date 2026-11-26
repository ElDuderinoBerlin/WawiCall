using System.Text;
using Google.Cloud.TextToSpeech.V1;
using Grpc.Core;

namespace WawiCallPro.Audio;

internal sealed class GoogleAnnouncementSpeech(TextToSpeechClient client) : CloudAnnouncementSpeech(Voices)
{
    internal static readonly IReadOnlyList<AnnouncementVoice> Voices = Array.AsReadOnly<AnnouncementVoice>(
        [new("de-DE-Neural2-G", "G (Neural2)", "de-DE"), new("de-DE-Chirp3-HD-Achernar", "Achernar (Chirp 3 HD)", "de-DE")]);

    protected override async Task<IEnumerable<AnnouncementVoice>> QueryVoicesAsync(CancellationToken token)
    {
        var response = await client.ListVoicesAsync(new ListVoicesRequest { LanguageCode = GermanLocale }, token).ConfigureAwait(false);
        return response.Voices.Where(voice => voice.LanguageCodes.Contains(GermanLocale)
            && (voice.Name.StartsWith("de-DE-Neural2-", StringComparison.Ordinal)
                || voice.Name.StartsWith("de-DE-Chirp3-HD-", StringComparison.Ordinal)))
            .Select(voice => new AnnouncementVoice(voice.Name, voice.Name[(GermanLocale.Length + 1)..], GermanLocale)).ToArray();
    }

    protected override async Task<byte[]> SynthesizeAsync(string text, AnnouncementVoice voice, CancellationToken token)
    {
        if (Encoding.UTF8.GetByteCount(text) > 5000)
            throw new AnnouncementSpeechException("Der Ansagetext ist für Google Cloud Text-to-Speech zu lang.");

        var request = new SynthesizeSpeechRequest
        {
            Input = new SynthesisInput { Text = text },
            Voice = new VoiceSelectionParams { LanguageCode = voice.Language, Name = voice.Id },
            AudioConfig = new AudioConfig { AudioEncoding = AudioEncoding.Linear16, SampleRateHertz = SampleRate }
        };
        try
        {
            var response = await client.SynthesizeSpeechAsync(request, token).ConfigureAwait(false);
            return response.AudioContent.ToByteArray();
        }
        catch (RpcException error)
        {
            token.ThrowIfCancellationRequested();
            throw new AnnouncementSpeechException(error.StatusCode switch
            {
                StatusCode.Unauthenticated or StatusCode.PermissionDenied =>
                    "Google Cloud Text-to-Speech hat den Zugang abgelehnt. Anmeldung, Projekt und Berechtigung prüfen.",
                StatusCode.ResourceExhausted => "Google Cloud Text-to-Speech meldet ein Kontingent- oder Nutzungslimit.",
                StatusCode.DeadlineExceeded => "Google Cloud Text-to-Speech hat das Zeitlimit überschritten.",
                _ => "Google Cloud Text-to-Speech konnte die Ansage nicht erzeugen."
            });
        }
    }
}

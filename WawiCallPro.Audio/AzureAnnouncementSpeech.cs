using Azure.Core;
using Microsoft.CognitiveServices.Speech;
using System.Runtime.InteropServices;

namespace WawiCallPro.Audio;

internal sealed class AzureAnnouncementSpeech(Func<SpeechConfig> createConfig) : CloudAnnouncementSpeech(Voices)
{
    internal AzureAnnouncementSpeech(Uri endpoint, TokenCredential credential)
        : this(() => SpeechConfig.FromEndpoint(endpoint, credential)) { }

    private static readonly IReadOnlyList<AnnouncementVoice> Voices = Array.AsReadOnly<AnnouncementVoice>(
        [new("de-DE-KatjaNeural", "Katja (Neural)", "de-DE"), new("de-DE-ConradNeural", "Conrad (Neural)", "de-DE")]);

    protected override async Task<byte[]> SynthesizeAsync(string text, AnnouncementVoice voice, CancellationToken token)
    {
        if (RuntimeInformation.ProcessArchitecture is not (Architecture.X64 or Architecture.Arm64))
            throw new AnnouncementSpeechException("Azure Speech benötigt unter Windows einen x64- oder ARM64-Prozess.");

        try
        {
            var config = createConfig();
            config.SpeechSynthesisVoiceName = voice.Id;
            config.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Riff16Khz16BitMonoPcm);
            using var synthesizer = new SpeechSynthesizer(config, audioConfig: null);
            token.ThrowIfCancellationRequested();
            var pending = synthesizer.SpeakTextAsync(text);
            try
            {
                using var result = await pending.WaitAsync(token).ConfigureAwait(false);
                if (result.Reason == ResultReason.SynthesizingAudioCompleted) return result.AudioData;
                var error = SpeechSynthesisCancellationDetails.FromResult(result).ErrorCode;
                throw new AnnouncementSpeechException(error switch
                {
                    CancellationErrorCode.AuthenticationFailure or CancellationErrorCode.Forbidden =>
                        "Azure Speech hat den Zugang abgelehnt. Anmeldung und Berechtigung prüfen.",
                    CancellationErrorCode.TooManyRequests => "Azure Speech meldet ein Kontingent- oder Nutzungslimit.",
                    _ => "Azure Speech konnte die Ansage nicht erzeugen."
                });
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                try { await synthesizer.StopSpeakingAsync().ConfigureAwait(false); }
                finally
                {
                    try { using var abandoned = await pending.ConfigureAwait(false); }
                    catch (Exception) { }
                }
                throw;
            }
        }
        catch (Exception exception) when (exception is not (OperationCanceledException or AnnouncementSpeechException))
        {
            token.ThrowIfCancellationRequested();
            throw new AnnouncementSpeechException("Azure Speech konnte die Ansage nicht erzeugen. Anmeldung und Verbindung prüfen.");
        }
    }
}

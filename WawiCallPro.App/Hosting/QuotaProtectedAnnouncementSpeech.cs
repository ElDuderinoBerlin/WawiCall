using WawiCallPro.Audio;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.App.Hosting;

internal sealed class QuotaProtectedAnnouncementSpeech(IAnnouncementSpeech speech, AnnouncementCloudQuota quota,
    AnnouncementSpeechProvider provider, AnnouncementCloudSettings settings) : IAnnouncementSpeech
{
    public IReadOnlyList<AnnouncementVoice> GetVoices() => speech.GetVoices();
    public Task<IReadOnlyList<AnnouncementVoice>> LoadVoicesAsync(CancellationToken token) => speech.LoadVoicesAsync(token);

    public async Task<AnnouncementVoice> WriteWaveAsync(string text, string? voiceId, string destination, CancellationToken token)
    {
        var voices = speech.GetVoices();
        var voice = voiceId is null ? voices.FirstOrDefault() : voices.FirstOrDefault(voice => voice.Id == voiceId);
        if (voice is null && voiceId is not null)
            voice = (await speech.LoadVoicesAsync(token).ConfigureAwait(false)).FirstOrDefault(voice => voice.Id == voiceId);
        if (voice is null) throw new AnnouncementSpeechException("Die gewählte Cloud-Stimme ist nicht verfügbar.");
        await Task.Run(() => quota.Reserve(provider, settings, voice.Id, text, token), token).ConfigureAwait(false);
        return await speech.WriteWaveAsync(text, voice.Id, destination, token).ConfigureAwait(false);
    }
}

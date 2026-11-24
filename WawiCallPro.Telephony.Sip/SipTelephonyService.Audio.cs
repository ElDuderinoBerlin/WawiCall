using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Interfaces.Telephony;

namespace WawiCallPro.Telephony.Sip;

public sealed partial class SipTelephonyService : IConversationAudioService
{
    public ConversationAudioSnapshot? GetAudio(Guid callId)
    {
        ISipConversationAudio? audio = null;
        lock (_gate)
        {
            if (_agent is { } agent && agent.Id == callId && HasLiveAudio(agent))
                audio = agent.Media?.ConversationAudio;
        }
        return audio?.GetAudio();
    }

    public Task SetPlaybackVolumeAsync(Guid callId, int percent, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(percent, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(percent, 100);
        return ChangeAudioAsync(callId, audio => audio.SetPlaybackVolume(percent), cancellationToken);
    }

    public Task SetMicrophoneMutedAsync(Guid callId, bool muted, CancellationToken cancellationToken = default) =>
        ChangeAudioAsync(callId, audio => audio.SetMicrophoneMuted(muted), cancellationToken);

    private Task ChangeAudioAsync(Guid callId, Action<ISipConversationAudio> change, CancellationToken token) =>
        CommandAsync(callId, agent => Task.Run(() =>
        {
            if (!HasLiveAudio(agent) || agent.Media?.ConversationAudio is not { } audio)
                throw new TelephonyException("AudioUnavailable");
            change(audio);
        }, token), token);

    private static bool HasLiveAudio(Agent agent) => agent.State?.Current.State is CallState.Connected or CallState.Held;
}

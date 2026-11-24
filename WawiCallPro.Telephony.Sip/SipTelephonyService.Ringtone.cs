using Microsoft.Extensions.Logging;

namespace WawiCallPro.Telephony.Sip;

public sealed partial class SipTelephonyService
{
    // The call event loop owns both start and stop. The sound device runs independently of SIP.
    private void StartRingtone(Agent agent)
    {
        if (_ringtone is not { } player || _devices.RingVolumePercent == 0 || agent.ShutdownRequested) return;

        var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        var settings = _devices;
        var playback = Task.Run(async () =>
        {
            try { await player.PlayAsync(settings, token).ConfigureAwait(false); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch { _log.LogWarning("Der Klingelton konnte nicht ausgegeben werden. Bitte prüfen Sie die Klingelausgabe."); }
        }, token);
        agent.Ringtone = (cancellation, playback);
    }

    private static async Task StopRingtoneAsync(Agent agent)
    {
        if (agent.Ringtone is not { } ringtone) return;
        agent.Ringtone = null;

        using var cancellation = ringtone.Cancellation;
        await cancellation.CancelAsync().ConfigureAwait(false);
        try { await ringtone.Playback.ConfigureAwait(false); }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
    }
}

using Microsoft.Extensions.Logging;
using SIPSorcery.SIP;
using WawiCallPro.Interfaces.Telephony;

namespace WawiCallPro.Telephony.Sip;

public sealed partial class SipTelephonyService
{
    private void ObserveByeResponse(SIPTransport transport, SIPResponse response)
    {
        if (response.Status != SIPResponseStatusCodesEnum.Ok || response.Header.CSeqMethod != SIPMethodsEnum.BYE)
            return;

        Agent? agent;
        lock (_gate) agent = ReferenceEquals(transport, _transport) ? _agent : null;
        if (agent?.UserAgent.Dialogue is not { DialogueState: SIPDialogueStateEnum.Terminated } dialog
            || response.Header.CallId != dialog.CallId || response.Header.To?.ToTag != dialog.LocalTag
            || response.Header.From?.FromTag != dialog.RemoteTag)
            return;

        // The stack has accepted the remote BYE. Its hangup event normally follows, but a media
        // cleanup failure can prevent that callback. Both observations converge on one terminal state.
        _log.LogDebug("SIP-BYE der Gegenstelle bestätigt.");
        agent.Stopping.Cancel();
        (agent.Media as ISipLiveRecordingMediaSession)?.CancelPendingStart();
        Queue(() => ChangeAsync(agent, agent.ShutdownRequested ? CallState.Failed : CallState.Ended,
            agent.ShutdownRequested ? CallEndReason.Interrupted : agent.TransferAccepted ? CallEndReason.Transferred : CallEndReason.RemoteHangup));
    }
}

using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using SIPSorcery.SIP;
using WawiCallPro.Interfaces.Telephony;

namespace WawiCallPro.Telephony.Sip;

public sealed partial class SipTelephonyService
{
    private async Task SendKeepAlivesAsync(SIPTransport transport, SIPURI registrarAddress, PeriodicTimer timer)
    {
        while (await timer.WaitForNextTickAsync().ConfigureAwait(false))
        {
            var peer = Volatile.Read(ref _registeredPeer);
            if (!ReferenceEquals(transport, Volatile.Read(ref _transport))) return;
            if (peer is null || Registration.State != RegistrationState.Registered) continue;

            try
            {
                var request = SIPRequest.GetRequest(SIPMethodsEnum.OPTIONS, registrarAddress.CopyOf());
                var result = await transport.SendRequestAsync(peer, request).ConfigureAwait(false);
                if (result != SocketError.Success)
                    _log.LogDebug("SIP-Keep-Alive konnte nicht gesendet werden ({SocketError}).", result);
            }
            catch (Exception)
            {
                // Keep-alive is best effort and must not interrupt registration or calls.
                _log.LogDebug("SIP-Keep-Alive konnte nicht gesendet werden.");
            }
        }
    }
}

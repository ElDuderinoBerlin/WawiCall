using System.Net;
using SIPSorcery.Media;
using SIPSorceryMedia.Abstractions;

namespace WawiCallPro.Audio;

internal sealed class ConversationVoipMediaSession(MediaEndPoints endpoints, IPAddress? bindAddress)
    : VoIPMediaSession(endpoints, bindAddress, 0)
{
    internal SipRtpPcmSender? PcmSender { private get; set; }

    public override Task SendDtmf(byte key, CancellationToken token) => PcmSender is { } sender
        ? sender.SendDuringDtmfAsync(() => base.SendDtmf(key, token), token)
        : base.SendDtmf(key, token);
}

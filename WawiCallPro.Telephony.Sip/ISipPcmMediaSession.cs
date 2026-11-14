using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Telephony.Sip;

/// <summary>Optional capability of the CSCore media session; existing media factories need not implement it.</summary>
public interface ISipPcmMediaSession : ISipMediaSession
{
    IConversationPcmSource PcmSource { get; }
}

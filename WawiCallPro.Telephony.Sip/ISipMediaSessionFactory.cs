using SIPSorcery.SIP.App;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.Telephony.Sip;

/// <summary>Creates one owned media endpoint per explicitly answered or dialled call.</summary>
public interface ISipMediaSessionFactory
{
    ISipMediaSession Create(SipAccountOptions account, AudioDeviceSettings devices);
}

public interface ISipMediaSession : IAsyncDisposable
{
    IMediaSession Session { get; }
    ISipConversationAudio? ConversationAudio => null;
    void ApplyPlaybackSettings();
}

/// <summary>Audio controls owned by the SIP media session.</summary>
public interface ISipConversationAudio
{
    ConversationAudioSnapshot GetAudio();
    void SetPlaybackVolume(int percent);
    void SetMicrophoneMuted(bool muted);
}

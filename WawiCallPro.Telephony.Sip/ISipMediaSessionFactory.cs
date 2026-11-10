using SIPSorcery.SIP.App;
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
    void ApplyPlaybackSettings();
}

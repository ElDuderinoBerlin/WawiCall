using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Telephony.Sip;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.Audio;

/// <summary>Both hosts attach recording ownership to their CSCore media without initializing audio during DI setup.</summary>
public sealed class RecordingSipMediaSessionFactory(WindowsSipMediaSessionFactory media, IDuplexPcmRecordingFactory recordings) : ISipMediaSessionFactory
{
    public ISipMediaSession Create(SipAccountOptions account, AudioDeviceSettings devices) =>
        new RecordingSipMediaSession((ISipPcmMediaSession)media.Create(account, devices), recordings);
}

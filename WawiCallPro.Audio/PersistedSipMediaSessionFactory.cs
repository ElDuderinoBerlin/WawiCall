using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Telephony.Sip;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.Audio;

/// <summary>The call owns persistence cleanup as part of its existing recording/media lifetime.</summary>
public sealed class PersistedSipMediaSessionFactory(WindowsSipMediaSessionFactory media, IPersistedDuplexPcmRecordingFactory recordings) : ISipMediaSessionFactory
{
    public ISipMediaSession Create(SipAccountOptions account, AudioDeviceSettings devices) =>
        new RecordingSipMediaSession((ISipPcmMediaSession)media.Create(account, devices), recordings);
}

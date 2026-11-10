using CSCore.CoreAudioAPI;
using SIPSorcery.Media;
using SIPSorcery.SIP.App;
using SIPSorceryMedia.Abstractions;
using SIPSorceryMedia.Windows.CSCore;
using WawiCallPro.Telephony.Sip;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.Audio;

/// <summary>Uses the user's CSCore endpoint; construction occurs only when media is requested.</summary>
public sealed class WindowsSipMediaSessionFactory : ISipMediaSessionFactory
{
    public ISipMediaSession Create(SipAccountOptions account, AudioDeviceSettings devices)
    {
        using var enumerator = new MMDeviceEnumerator();
        MMDevice? playback = null;
        MMDevice? capture = null;
        WindowsAudioEndPoint? endpoint = null;
        try
        {
            if (devices.PlaybackDeviceId is not null) playback = enumerator.GetDevice(devices.PlaybackDeviceId);
            if (devices.CaptureDeviceId is not null) capture = enumerator.GetDevice(devices.CaptureDeviceId);
            endpoint = new WindowsAudioEndPoint(new AudioEncoder(), playback, capture, disableSource: devices.MicrophoneMuted);
            if (!endpoint.IsAudioSinkInitialized || !devices.MicrophoneMuted && !endpoint.IsAudioSourceInitialized)
                throw new WawiCallPro.Interfaces.Telephony.TelephonyException("AudioDeviceUnavailable");
            endpoint.OutputVolume = devices.PlaybackVolumePercent / 100F;
            endpoint.RestrictFormats(format => format.Codec is AudioCodecsEnum.PCMA or AudioCodecsEnum.PCMU
                || account.Connection.EnableG722 && format.Codec == AudioCodecsEnum.G722);
            var media = new VoIPMediaSession(endpoint.ToMediaEndPoints(), System.Net.IPAddress.Parse(account.Connection.BindAddress), 0)
                { AcceptRtpFromAny = account.Connection.AcceptRtpFromAny };
            return new AudioSession(endpoint, media, devices.PlaybackVolumePercent, playback, capture);
        }
        catch
        {
            try { endpoint?.Close().GetAwaiter().GetResult(); }
            finally { playback?.Dispose(); capture?.Dispose(); }
            throw;
        }
    }

    private sealed class AudioSession(WindowsAudioEndPoint endpoint, VoIPMediaSession media, int volume,
        MMDevice? playback, MMDevice? capture) : ISipMediaSession
    {
        private int _closed;
        public IMediaSession Session => media;
        public void ApplyPlaybackSettings()
        {
            if (endpoint.IsAudioSinkInitialized) endpoint.OutputVolume = volume / 100F;
        }
        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _closed, 1) != 0) return;
            try { media.Close("Call ended"); }
            finally
            {
                try { await endpoint.Close(); }
                finally { playback?.Dispose(); capture?.Dispose(); }
            }
        }
    }
}

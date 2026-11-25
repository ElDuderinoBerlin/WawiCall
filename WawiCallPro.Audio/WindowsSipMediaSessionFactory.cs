using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SIPSorcery.Media;
using SIPSorcery.SIP.App;
using SIPSorceryMedia.Abstractions;
using WawiCallPro.Telephony.Sip;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.Audio;

/// <summary>Creates one SIP media session with independently recoverable CSCore capture and playback.</summary>
public sealed class WindowsSipMediaSessionFactory(ISipRtpPcmBridgeFactory bridges) : ISipMediaSessionFactory
{
    private readonly IConversationAudioHardware _hardware = new ConversationAudioHardware();
    private readonly ApplicationConfigurationService? _configuration;
    private readonly TimeProvider _clock = TimeProvider.System;
    private readonly ILogger _log = NullLogger.Instance;

    public WindowsSipMediaSessionFactory(ISipRtpPcmBridgeFactory bridges, ApplicationConfigurationService configuration,
        TimeProvider clock, ILogger<WindowsSipMediaSessionFactory> log) : this(bridges)
    {
        _configuration = configuration;
        _clock = clock;
        _log = log;
    }

    internal WindowsSipMediaSessionFactory(ISipRtpPcmBridgeFactory bridges, IConversationAudioHardware hardware,
        ApplicationConfigurationService configuration, TimeProvider clock, ILogger log) : this(bridges)
    {
        _hardware = hardware;
        _configuration = configuration;
        _clock = clock;
        _log = log;
    }

    public ISipMediaSession Create(SipAccountOptions account, AudioDeviceSettings devices)
    {
        var start = new ConversationDeviceSelection(devices.CaptureDeviceId, devices.PlaybackDeviceId);
        var pcm = new PcmObservingAudioEncoder(new AudioEncoder());
        var connection = account.Connection;
        var audio = new RecoveringConversationAudio(_hardware, () => _configuration?.ConversationDevices ?? start, pcm,
            devices, connection.EnableG722 || connection.CodecOrder is not null, _clock, _log);
        VoIPMediaSession? media = null;
        IAsyncDisposable? receive = null;
        try
        {
            if (connection.CodecOrder is { } order) audio.SetCodecOrder(order);
            media = new VoIPMediaSession(new MediaEndPoints { AudioSource = audio, AudioSink = audio },
                System.Net.IPAddress.Parse(connection.BindAddress), 0)
                { AcceptRtpFromAny = connection.AcceptRtpFromAny };
            receive = bridges.Create(media, audio, pcm);
            return new RecordingInputMediaSession(new PcmSipMediaSession(new AudioSession(audio, media), pcm, receive),
                devices, audio.ReadPlaybackBinding);
        }
        catch
        {
            try { if (receive is not null) receive.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
            finally
            {
                try { pcm.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
                finally
                {
                    try { media?.Close("Media setup failed"); }
                    finally { audio.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
                }
            }
            throw;
        }
    }

    private sealed class AudioSession(RecoveringConversationAudio audio, VoIPMediaSession media) : ISipMediaSession
    {
        public IMediaSession Session => media;
        public ISipConversationAudio ConversationAudio => audio;
        public void ApplyPlaybackSettings()
        {
            if (media.AudioStream is { } stream) audio.EnableNegotiatedAudio(stream.NegotiatedFormat.ToAudioFormat());
        }
        public async ValueTask DisposeAsync()
        {
            try { media.Close("Call ended"); }
            finally { await audio.DisposeAsync().ConfigureAwait(false); }
        }
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Audio;

public static class AudioConversionServices
{
    /// <summary>Both hosts share stateless conversion/mixing services. Native resources and files belong to each request.</summary>
    public static IServiceCollection AddAudioConversion(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ISipRtpPcmBridgeFactory, SipRtpPcmBridgeFactory>();
        services.TryAddSingleton<IMp3EncodingBackend, MediaFoundationMp3Backend>();
        services.TryAddSingleton<IMp3Converter, WavToMp3Converter>();
        services.TryAddSingleton<WawiCallPro.Utilities.Persistence.IRecordingFormatConverters, RecordingFormatConverters>();
        services.TryAddSingleton<IHistoryRecordingOutput, WasapiHistoryRecordingOutput>();
        services.TryAddSingleton<WindowsIncomingRingtone>();
        services.TryAddSingleton<IAudioFileConversion, AudioFileConversion>();
        services.TryAddSingleton<IAnnouncementSpeech, WindowsAnnouncementSpeech>();
        services.TryAddSingleton<IAnnouncementAudio, WindowsAnnouncementAudio>();
        services.TryAddSingleton<IAudioSelfTest, WindowsAudioSelfTest>();
        services.TryAddSingleton<IConversationWaveRenderer, ConversationWaveRenderer>();
        services.TryAddSingleton<IConversationWaveMixer, ConversationWaveMixer>();
        services.TryAddSingleton<IPcmSegmentSinkFactory, CscorePcmSegmentSinkFactory>();
        services.TryAddSingleton<IPcmRecordingSessionFactory, PcmRecordingSessionFactory>();
        services.TryAddSingleton<IRtpPcmRecordingFeedFactory, WawiCallPro.Core.Audio.RtpPcmRecordingFeedFactory>();
        services.TryAddSingleton<IReceivedPcmRecordingBindingFactory, WawiCallPro.Core.Audio.ReceivedPcmRecordingBindingFactory>();
        services.TryAddSingleton<ISentPcmRecordingBindingFactory, WawiCallPro.Core.Audio.SentPcmRecordingBindingFactory>();
        services.TryAddSingleton<WawiCallPro.Core.Audio.DuplexPcmRecordingFactory>();
        services.TryAddSingleton<IPlaybackPcmCaptureFactory, CscorePlaybackCaptureFactory>();
        services.TryAddSingleton<PlaybackDuplexRecordingFactory>();
        services.TryAddSingleton<IDuplexPcmRecordingFactory, RecordingSourceFactory>();
        services.TryAddSingleton<IRtpRecordingAnchorResolver, WawiCallPro.Core.Audio.RtpRecordingAnchorResolver>();
        services.TryAddSingleton<IRtpAudioPacketBufferFactory, WawiCallPro.Core.Audio.RtpAudioPacketBufferFactory>();
        return services;
    }

    /// <summary>Hosts opt into call persistence after registering the shared database journal and application paths.</summary>
    public static IServiceCollection AddPersistedRecordings(this IServiceCollection services, bool startRecovery = true)
    {
        services.TryAddSingleton<WawiCallPro.Utilities.Persistence.IPersistedMp3ConversionFactory, WawiCallPro.Utilities.Persistence.PersistedMp3ConversionFactory>();
        if (startRecovery) services.AddHostedService<WawiCallPro.Utilities.Persistence.RecordingConversionRecoveryStartup>();
        services.TryAddSingleton<WawiCallPro.Utilities.Persistence.IRecordingConversionBatch, WawiCallPro.Utilities.Persistence.RecordingConversionBatch>();
        services.TryAddSingleton<WawiCallPro.Utilities.Persistence.IRecordingConversionRequests, WawiCallPro.Utilities.Persistence.RecordingConversionRequests>();
        services.TryAddSingleton<WawiCallPro.Utilities.Persistence.IAutomaticRecordingConversions, WawiCallPro.Utilities.Persistence.AutomaticRecordingConversions>();
        services.TryAddSingleton<WawiCallPro.Utilities.Persistence.RecordingConversionSignals>();
        services.TryAddSingleton<WawiCallPro.Utilities.Persistence.RecordingConversionQueue>();
        services.TryAddSingleton<WawiCallPro.Utilities.Persistence.IRecordingConversionQueue>(provider => provider.GetRequiredService<WawiCallPro.Utilities.Persistence.RecordingConversionSignals>());
        services.AddHostedService(provider => provider.GetRequiredService<WawiCallPro.Utilities.Persistence.RecordingConversionQueue>());
        services.TryAddSingleton<IPersistedDuplexPcmRecordingFactory, PersistedDuplexPcmRecordingFactory>();
        services.TryAddSingleton<LivePcmRecordingFactory>();
        services.TryAddSingleton<IRecordingJournalRecovery, RecordingJournalRecovery>();
        services.TryAddSingleton<IInterruptedRecordingRecovery, InterruptedRecordingRecovery>();
        services.TryAddSingleton<RecordingRecoverySipPersistence>();
        services.TryAddSingleton<InterruptedRecordingSipPersistence>();
        services.TryAddSingleton<WawiCallPro.Utilities.Persistence.RecordingArtifactAuditScheduler>();
        services.TryAddSingleton<WawiCallPro.Utilities.Persistence.IRecordingArtifactAuditScheduler>(provider => provider.GetRequiredService<WawiCallPro.Utilities.Persistence.RecordingArtifactAuditScheduler>());
        services.AddHostedService(provider => provider.GetRequiredService<WawiCallPro.Utilities.Persistence.RecordingArtifactAuditScheduler>());
        services.TryAddSingleton<WawiCallPro.Telephony.Sip.SipCallPersistence>();
        services.TryAddSingleton<WawiCallPro.Telephony.Sip.ISipCallPersistence, ReconciledRecordingSipPersistence>();
        return services;
    }
}

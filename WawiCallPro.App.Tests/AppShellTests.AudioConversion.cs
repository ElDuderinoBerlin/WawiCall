using Microsoft.Extensions.DependencyInjection;
using WawiCallPro.App.Hosting;
using WawiCallPro.Audio;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Utilities.Diagnostics;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    public Task ApplicationHostResolvesConverterWithoutStartingNativeWork() => RunStaAsync(async () =>
    {
        using var context = new WindowsFormsSynchronizationContext();
        using var exceptions = new AppExceptionBoundary(new MaintenanceCrashReporter());
        var host = AppCompositionRoot.Build(context, exceptions, new StartupExecutionMode(true), new MaintenanceInstanceGuard());
        try
        {
            var converter = host.Services.GetRequiredService<IMp3Converter>();
            Assert.IsInstanceOfType<WavToMp3Converter>(converter);
            Assert.AreSame(converter, host.Services.GetRequiredService<IMp3Converter>());
            Assert.IsInstanceOfType<MediaFoundationMp3Backend>(host.Services.GetRequiredService<IMp3EncodingBackend>());
            var mixer = host.Services.GetRequiredService<IConversationWaveMixer>();
            Assert.IsInstanceOfType<ConversationWaveMixer>(mixer);
            Assert.AreSame(mixer, host.Services.GetRequiredService<IConversationWaveMixer>());
            Assert.IsInstanceOfType<ConversationWaveRenderer>(host.Services.GetRequiredService<IConversationWaveRenderer>());
            var recordingFactory = host.Services.GetRequiredService<IPcmRecordingSessionFactory>();
            Assert.IsInstanceOfType<PcmRecordingSessionFactory>(recordingFactory);
            Assert.AreSame(recordingFactory, host.Services.GetRequiredService<IPcmRecordingSessionFactory>());
            Assert.IsInstanceOfType<CscorePcmSegmentSinkFactory>(host.Services.GetRequiredService<IPcmSegmentSinkFactory>());
            Assert.IsInstanceOfType<PersistedSipMediaSessionFactory>(host.Services.GetRequiredService<WawiCallPro.Telephony.Sip.ISipMediaSessionFactory>());
            Assert.AreSame<object>(host.Services.GetRequiredService<WawiCallPro.Interfaces.Telephony.ITelephonyService>(), host.Services.GetRequiredService<ICallRecordingService>());
            var feeds = host.Services.GetRequiredService<IRtpPcmRecordingFeedFactory>();
            Assert.IsInstanceOfType<WawiCallPro.Core.Audio.RtpPcmRecordingFeedFactory>(feeds);
            Assert.AreSame(feeds, host.Services.GetRequiredService<IRtpPcmRecordingFeedFactory>());
            var bindings = host.Services.GetRequiredService<IReceivedPcmRecordingBindingFactory>();
            Assert.IsInstanceOfType<WawiCallPro.Core.Audio.ReceivedPcmRecordingBindingFactory>(bindings);
            Assert.AreSame(bindings, host.Services.GetRequiredService<IReceivedPcmRecordingBindingFactory>());
            var sentBindings = host.Services.GetRequiredService<ISentPcmRecordingBindingFactory>();
            Assert.IsInstanceOfType<WawiCallPro.Core.Audio.SentPcmRecordingBindingFactory>(sentBindings);
            Assert.AreSame(sentBindings, host.Services.GetRequiredService<ISentPcmRecordingBindingFactory>());
            var duplex = host.Services.GetRequiredService<IDuplexPcmRecordingFactory>();
            Assert.IsInstanceOfType<WawiCallPro.Core.Audio.DuplexPcmRecordingFactory>(duplex);
            Assert.AreSame(duplex, host.Services.GetRequiredService<IDuplexPcmRecordingFactory>());
            var anchors = host.Services.GetRequiredService<IRtpRecordingAnchorResolver>();
            Assert.IsInstanceOfType<WawiCallPro.Core.Audio.RtpRecordingAnchorResolver>(anchors);
            Assert.AreSame(anchors, host.Services.GetRequiredService<IRtpRecordingAnchorResolver>());
            var captures = host.Services.GetRequiredService<WawiCallPro.Database.Contracts.IRecordingCaptureRepository>();
            Assert.IsInstanceOfType<WawiCallPro.Database.Repositories.DapperRecordingCaptureRepository>(captures);
            Assert.AreSame(captures, host.Services.GetRequiredService<WawiCallPro.Database.Contracts.IRecordingCaptureRepository>());
            Assert.IsInstanceOfType<WawiCallPro.Database.Repositories.DapperRecordingArtifactRepository>(host.Services.GetRequiredService<WawiCallPro.Database.Contracts.IRecordingArtifactRepository>());
            Assert.IsInstanceOfType<WawiCallPro.Database.Repositories.DapperRecordingConversionRepository>(host.Services.GetRequiredService<WawiCallPro.Database.Contracts.IRecordingConversionRepository>());
            Assert.IsInstanceOfType<WawiCallPro.Utilities.Persistence.DurableRecordingConversionJournal>(host.Services.GetRequiredService<WawiCallPro.Database.Contracts.IRecordingConversionJournal>());
            Assert.IsInstanceOfType<WawiCallPro.Utilities.Persistence.ProtectedRecordingConversionOutbox>(host.Services.GetRequiredService<WawiCallPro.Utilities.Persistence.IRecordingConversionOutbox>());
            Assert.IsInstanceOfType<WawiCallPro.Utilities.Persistence.RecordingConversionRecovery>(host.Services.GetRequiredService<WawiCallPro.Utilities.Persistence.IRecordingConversionRecovery>());
            Assert.IsInstanceOfType<WawiCallPro.Utilities.Persistence.PersistedMp3ConversionFactory>(host.Services.GetRequiredService<WawiCallPro.Utilities.Persistence.IPersistedMp3ConversionFactory>());
            Assert.IsInstanceOfType<WawiCallPro.Utilities.Persistence.RecordingConversionBatch>(host.Services.GetRequiredService<WawiCallPro.Utilities.Persistence.IRecordingConversionBatch>());
            Assert.IsInstanceOfType<WawiCallPro.Utilities.Persistence.RecordingDirectoryInventory>(host.Services.GetRequiredService<WawiCallPro.Utilities.Persistence.IRecordingDirectoryInventory>());
            Assert.IsInstanceOfType<WawiCallPro.Utilities.Persistence.RecordingConversionRequests>(host.Services.GetRequiredService<WawiCallPro.Utilities.Persistence.IRecordingConversionRequests>());
            var conversionQueue = host.Services.GetRequiredService<WawiCallPro.Utilities.Persistence.IRecordingConversionQueue>();
            Assert.AreSame(conversionQueue, host.Services.GetRequiredService<WawiCallPro.Utilities.Persistence.RecordingConversionSignals>());
            var worker = host.Services.GetRequiredService<WawiCallPro.Utilities.Persistence.RecordingConversionQueue>();
            Assert.IsTrue(host.Services.GetServices<Microsoft.Extensions.Hosting.IHostedService>().Any(service => ReferenceEquals(service, worker)));
            Assert.IsInstanceOfType<WawiCallPro.Utilities.Persistence.RecordingWaveArtifactPublisher>(host.Services.GetRequiredService<WawiCallPro.Utilities.Persistence.IRecordingCompletionPublisher>());
            var persisted = host.Services.GetRequiredService<IPersistedDuplexPcmRecordingFactory>();
            Assert.IsInstanceOfType<PersistedDuplexPcmRecordingFactory>(persisted);
            Assert.AreSame(persisted, host.Services.GetRequiredService<IPersistedDuplexPcmRecordingFactory>());
            Assert.IsInstanceOfType<RecordingJournalRecovery>(host.Services.GetRequiredService<IRecordingJournalRecovery>());
            Assert.IsInstanceOfType<InterruptedRecordingRecovery>(host.Services.GetRequiredService<IInterruptedRecordingRecovery>());
            Assert.IsInstanceOfType<ReconciledRecordingSipPersistence>(host.Services.GetRequiredService<WawiCallPro.Telephony.Sip.ISipCallPersistence>());
            Assert.IsInstanceOfType<WawiCallPro.Utilities.Persistence.RecordingArtifactReconciliation>(host.Services.GetRequiredService<WawiCallPro.Utilities.Persistence.IRecordingArtifactReconciliation>());
            var packets = host.Services.GetRequiredService<IRtpAudioPacketBufferFactory>();
            Assert.IsInstanceOfType<WawiCallPro.Core.Audio.RtpAudioPacketBufferFactory>(packets);
            Assert.AreSame(packets, host.Services.GetRequiredService<IRtpAudioPacketBufferFactory>());
            var bridge = host.Services.GetRequiredService<ISipRtpPcmBridgeFactory>();
            Assert.IsInstanceOfType<SipRtpPcmBridgeFactory>(bridge);
            Assert.AreSame(bridge, host.Services.GetRequiredService<ISipRtpPcmBridgeFactory>());
        }
        finally { await ((IAsyncDisposable)host).DisposeAsync(); }
    });
}

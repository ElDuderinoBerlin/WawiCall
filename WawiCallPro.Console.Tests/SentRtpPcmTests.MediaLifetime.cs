using Microsoft.Extensions.DependencyInjection;
using SIPSorcery.Media;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using WawiCallPro.Audio;
using WawiCallPro.ConsoleApp;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Telephony.Sip;

namespace WawiCallPro.Console.Tests;

public sealed partial class SentRtpPcmTests
{
    [TestMethod]
    [DataRow(SDPWellKnownMediaFormatsEnum.PCMA)]
    [DataRow(SDPWellKnownMediaFormatsEnum.PCMU)]
    [DataRow(SDPWellKnownMediaFormatsEnum.G722)]
    public async Task ExplicitlyStoppedWaveSurvivesItsOwningMediaSessionClosing(SDPWellKnownMediaFormatsEnum codec)
    {
        await using var fixture = new Fixture(codec); fixture.EnableReceiveBuffer(); await fixture.Connect(); using var files = new RecordingFiles();
        using var host = ConsoleCompositionRoot.Build(loadPlugins: false);
        Assert.IsInstanceOfType<RecordingSipMediaSessionFactory>(host.Services.GetRequiredService<ISipMediaSessionFactory>());
        Assert.AreSame<object>(host.Services.GetRequiredService<WawiCallPro.Interfaces.Telephony.ITelephonyService>(), host.Services.GetRequiredService<ICallRecordingService>());
        await using var media = new RecordingSipMediaSession(new PcmSipMediaSession(new RecordingNativeMedia(fixture.Media), fixture.Tap, fixture.Bridge), DuplexFactory());
        var recording = await media.StartRecordingAsync(DuplexRequest(fixture, files)); var rate = fixture.Format.ClockRate; var block = rate / 50; var input = Samples(block, 0);
        fixture.Capture(input); await fixture.Packets(1);
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Media.OnRtpPacketReceived += (_, type, _) => { if (type == SDPMediaTypesEnum.audio) received.TrySetResult(); };
        var payload = new AudioEncoder().EncodeAudio(Samples(block, 1), fixture.Format); var expected = new AudioEncoder().DecodeAudio(payload, fixture.Format);
        fixture.SendRemote(100, 5000, payload); await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await recording.StopAsync(TimeSpan.FromMilliseconds(40)); var beforeClose = File.ReadAllBytes(files.Output);
        await media.DisposeAsync(); Assert.AreEqual(PcmRecordingState.Completed, recording.State); Assert.IsTrue(recording.InputCompletion.IsCompleted);
        CollectionAssert.AreEqual(beforeClose, File.ReadAllBytes(files.Output)); var pcm = files.Read(rate, block * 2);
        for (var frame = 0; frame < block * 2; frame++)
        {
            Assert.AreEqual(frame < block ? input[frame] : (short)0, pcm[frame * 2]);
            var offset = frame - rate / 100; Assert.AreEqual(offset >= 0 && offset < expected.Length ? expected[offset] : (short)0, pcm[frame * 2 + 1]);
        }
        Assert.IsTrue(fixture.Media.IsClosed);
    }

    [TestMethod]
    public async Task OwningMediaSessionAbortsAnUnstoppedLiveRecordingWithoutPublishingAWave()
    {
        await using var fixture = new Fixture(); await fixture.Connect(); using var files = new RecordingFiles();
        var native = new RecordingNativeMedia(fixture.Media);
        var media = new RecordingSipMediaSession(new PcmSipMediaSession(native, fixture.Tap, fixture.Bridge), DuplexFactory());
        var recording = await media.StartRecordingAsync(DuplexRequest(fixture, files)); fixture.Capture(Samples(160, 0)); await fixture.Packets(1);
        var closing = media.DisposeAsync().AsTask(); Assert.AreSame(closing, media.DisposeAsync().AsTask()); await closing;
        Assert.AreEqual(PcmRecordingState.Aborted, recording.State); Assert.IsTrue(recording.InputCompletion.IsCompleted);
        Assert.IsFalse(File.Exists(files.Output)); Assert.AreEqual(1, native.Closed); Assert.IsTrue(fixture.Media.IsClosed);
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => recording.StopAsync(TimeSpan.FromMilliseconds(40)));
    }

    private sealed class RecordingNativeMedia(VoIPMediaSession media) : ISipMediaSession
    {
        internal int Closed;
        public SIPSorcery.SIP.App.IMediaSession Session => media;
        public void ApplyPlaybackSettings() { }
        public ValueTask DisposeAsync() { Closed++; media.Close("Synthetic recording owner"); return ValueTask.CompletedTask; }
    }
}

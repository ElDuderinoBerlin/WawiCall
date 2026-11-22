using SIPSorcery.Net;
using SIPSorcery.Media;
using SIPSorceryMedia.Abstractions;
using WawiCallPro.Audio;
using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Console.Tests;

public sealed partial class SentRtpPcmTests
{
    [TestMethod]
    [DataRow(SDPWellKnownMediaFormatsEnum.PCMA)]
    [DataRow(SDPWellKnownMediaFormatsEnum.PCMU)]
    [DataRow(SDPWellKnownMediaFormatsEnum.G722)]
    public async Task LiveReferencesRecordActualUdpAndCodecPcmWithoutCallerSuppliedAnchors(SDPWellKnownMediaFormatsEnum codec)
    {
        await using var fixture = new Fixture(codec); fixture.EnableReceiveBuffer(); await fixture.Connect();
        using var files = new RecordingFiles();
        var live = new LivePcmRecordingFactory(new LiveForwardFactory(DuplexFactory()), TimeProvider.System);
        var start = live.StartAsync(fixture.Tap, new(Guid.NewGuid(), Guid.NewGuid(), files.Output, fixture.Format.ClockRate));
        var block = fixture.Format.ClockRate / 50;
        fixture.Capture(Samples(block, 0)); await fixture.Packets(1);
        fixture.SendRemote(100, 5000, new AudioEncoder().EncodeAudio(Samples(block, 1), fixture.Format));
        await using var recording = await start.WaitAsync(TimeSpan.FromSeconds(10));
        await Task.Delay(60);
        var result = await recording.StopAsync();
        Assert.AreEqual(PcmRecordingState.Completed, recording.State);
        Assert.AreEqual(2, result.Wave.Channels);
        var pcm = files.Read(fixture.Format.ClockRate, checked((int)result.Wave.FrameCount));
        Assert.IsTrue(pcm.Where((_, index) => index % 2 == 0).Any(value => value != 0));
        Assert.IsTrue(pcm.Where((_, index) => index % 2 == 1).Any(value => value != 0));
    }
    private sealed class LiveForwardFactory(IDuplexPcmRecordingFactory inner) : IPersistedDuplexPcmRecordingFactory
    {
        public Task<IDuplexPcmRecording> StartAsync(IConversationPcmFailureSource source, DuplexPcmRecordingRequest request,
            CancellationToken token = default) => inner.StartAsync(source, request, token);
    }
}

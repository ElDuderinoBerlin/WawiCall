using Microsoft.Extensions.DependencyInjection;
using SIPSorcery.Media;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using WawiCallPro.Audio;
using WawiCallPro.ConsoleApp;
using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Console.Tests;

public sealed partial class SentRtpPcmTests
{
    [TestMethod]
    [DataRow(SDPWellKnownMediaFormatsEnum.PCMA)]
    [DataRow(SDPWellKnownMediaFormatsEnum.PCMU)]
    [DataRow(SDPWellKnownMediaFormatsEnum.G722)]
    public async Task VerifiedSamplingReferencesCalculateBothOffsetsForASampleExactLiveWave(SDPWellKnownMediaFormatsEnum codec)
    {
        await using var fixture = new Fixture(codec); fixture.EnableReceiveBuffer(); await fixture.Connect(); using var files = new RecordingFiles();
        using var host = ConsoleCompositionRoot.Build(loadPlugins: false);
        var resolver = host.Services.GetRequiredService<IRtpRecordingAnchorResolver>();
        Assert.IsInstanceOfType<WawiCallPro.Core.Audio.RtpRecordingAnchorResolver>(resolver);
        Assert.AreSame(resolver, host.Services.GetRequiredService<IRtpRecordingAnchorResolver>());
        var epoch = 0xE000000080000000UL; var clock = Guid.NewGuid(); var sent = Anchor(fixture);
        // These are synthetic, known sampling instants, not the SDK's unverified RTCP report/arrival times.
        RtpClockCalibration References(uint ssrc, uint timestamp) => new(
            new(clock, ssrc, 8000, unchecked(timestamp - 8000), epoch - (1UL << 32)), new(clock, ssrc, 8000, timestamp, epoch));
        var calibration = new RtpDuplexCalibration(clock, epoch,
            References(sent.SynchronizationSource, unchecked(sent.Timestamp + 40)), References(fixture.RemoteSsrc, 4920),
            new(sent.SynchronizationSource, sent.Timestamp, 8000, 160), new(fixture.RemoteSsrc, 100, 5000, 8000));
        var rate = fixture.Format.ClockRate; var block = rate / 50;
        var request = resolver.Resolve(files.Request(rate), calibration);
        Assert.AreEqual(TimeSpan.FromMilliseconds(-5), request.Sent.RecordingOffset);
        Assert.AreEqual(TimeSpan.FromMilliseconds(10), request.Received.RecordingOffset);
        await using var media = new RecordingSipMediaSession(new PcmSipMediaSession(new RecordingNativeMedia(fixture.Media), fixture.Tap, fixture.Bridge),
            host.Services.GetRequiredService<IDuplexPcmRecordingFactory>());
        var recording = await media.StartRecordingAsync(request);
        var input = Samples(block, 0); fixture.Capture(input); await fixture.Packets(1);
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Media.OnRtpPacketReceived += (_, type, _) => { if (type == SDPMediaTypesEnum.audio) received.TrySetResult(); };
        var payload = new AudioEncoder().EncodeAudio(Samples(block, 1), fixture.Format);
        var expected = new AudioEncoder().DecodeAudio(payload, fixture.Format);
        fixture.SendRemote(100, 5000, payload); await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await recording.StopAsync(TimeSpan.FromMilliseconds(40)); var pcm = files.Read(rate, block * 2);
        for (var frame = 0; frame < block * 2; frame++)
        {
            var left = frame + rate / 200; Assert.AreEqual(left < input.Length ? input[left] : (short)0, pcm[frame * 2]);
            var right = frame - rate / 100; Assert.AreEqual(right >= 0 && right < expected.Length ? expected[right] : (short)0, pcm[frame * 2 + 1]);
        }
        Assert.AreEqual(PcmRecordingState.Completed, recording.State);
    }
}

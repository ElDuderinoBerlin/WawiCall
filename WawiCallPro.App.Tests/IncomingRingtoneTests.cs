using CSCore;
using Microsoft.Extensions.DependencyInjection;
using WawiCallPro.Audio;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.App.Tests;

[TestClass]
public sealed class IncomingRingtoneTests
{
    [TestMethod]
    public async Task RingtoneUsesSelectedOutputAndRepeatsItsPcmCadence()
    {
        var output = new CadenceProbe();
        await new WindowsIncomingRingtone(output).PlayAsync("ring-output", 35, CancellationToken.None);
        Assert.IsTrue(output.Played);
    }

    [TestMethod]
    public async Task MutedNativeRingtoneStopsAndCanReopenOutput()
    {
        if (Environment.GetEnvironmentVariable("WCP_VERIFY_RECORDING_OUTPUT_DEVICE") != "1")
            Assert.Inconclusive("Native WASAPI output is opt-in.");

        using var services = new ServiceCollection().AddAudioConversion().BuildServiceProvider();
        var output = services.GetRequiredService<IHistoryRecordingOutput>();
        var ringtone = new WindowsIncomingRingtone(output);
        for (var run = 0; run < 2; run++)
        {
            using var stop = new CancellationTokenSource();
            var playback = Task.Run(() => ringtone.PlayAsync(null, 0, stop.Token));
            var elapsed = Task.Delay(TimeSpan.FromMilliseconds(600));
            try
            {
                if (await Task.WhenAny(playback, elapsed) == playback)
                {
                    await playback;
                    Assert.Fail("WASAPI stopped before cancellation.");
                }
            }
            finally { await stop.CancelAsync(); }
            await Assert.ThrowsAsync<OperationCanceledException>(() => playback.WaitAsync(TimeSpan.FromSeconds(5)));
        }
    }

    private sealed class CadenceProbe : IHistoryRecordingOutput
    {
        internal bool Played { get; private set; }

        public void Play(IWaveSource source, AudioDeviceSettings settings, CancellationToken token)
        {
            Assert.AreEqual("ring-output", settings.PlaybackDeviceId);
            Assert.AreEqual(35, settings.PlaybackVolumePercent);
            Assert.AreEqual(1, source.WaveFormat.Channels);
            Assert.AreEqual(16, source.WaveFormat.BitsPerSample);
            var second = source.WaveFormat.SampleRate * source.WaveFormat.BlockAlign;
            var samples = new byte[second * 4];
            Assert.AreEqual(samples.Length, source.Read(samples, 0, samples.Length));
            Assert.IsTrue(samples[..second].Any(value => value != 0));
            Assert.IsTrue(samples[second..(second * 3)].All(value => value == 0));
            CollectionAssert.AreEqual(samples[..second], samples[(second * 3)..]);
            Played = true;
        }
    }
}

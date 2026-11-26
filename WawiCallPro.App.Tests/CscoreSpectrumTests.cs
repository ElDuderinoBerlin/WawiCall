using WawiCallPro.Audio;
using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.App.Tests;

[TestClass]
public sealed class CscoreSpectrumTests
{
    [TestMethod]
    [DataRow(8000)]
    [DataRow(48000)]
    public void FrequenciesDriveSeparateBandsAndMutingClearsTheOutput(int sampleRate)
    {
        var analyzer = new CscoreSpectrumAnalyzer(TimeProvider.System);
        var samples = new short[2048];
        for (var i = 0; i < samples.Length; i++)
        {
            var time = (double)i / sampleRate;
            samples[i] = (short)(16000 * Math.Sin(2 * Math.PI * 280 * time) +
                4000 * Math.Sin(2 * Math.PI * 2100 * time));
        }

        analyzer.Add(sampleRate, 1, samples);
        var bands = analyzer.Read();
        Assert.HasCount(AudioSpectrum.BandEdgesHz.Length - 1, bands);
        Assert.IsGreaterThan(bands[9], bands[3]);
        Assert.IsGreaterThan(bands[6], bands[9]);
        Assert.IsGreaterThan(30, bands[9]);
        CollectionAssert.AreEqual(AudioSpectrum.Silence.ToArray(), analyzer.Read(0).ToArray());
        analyzer.Clear();
        CollectionAssert.AreEqual(AudioSpectrum.Silence.ToArray(), analyzer.Read().ToArray());
    }

    [TestMethod]
    public void HistoryMeterShowsBothStereoChannelsAndLetsBriefPeaksFallGradually()
    {
        const int rate = 16000;
        var clock = new LiveRecordingClock();
        var transport = new HistoryPlaybackTransport(clock);
        var stereo = new short[4096];
        for (var frame = 0; frame < stereo.Length / 2; frame++)
        {
            var sample = (short)(8000 * Math.Sin(Math.Tau * 700 * frame / rate));
            stereo[2 * frame] = sample;
            stereo[2 * frame + 1] = (short)-sample;
        }

        transport.AddSamples(rate, 2, stereo);
        var peak = transport.ReadSpectrum().Max();
        Assert.IsGreaterThan(30, peak);
        clock.Advance(80);
        transport.AddSamples(rate, 2, new short[stereo.Length]);
        var falling = transport.ReadSpectrum().Max();
        Assert.IsTrue(falling > 0 && falling < peak);
        clock.Advance(450);
        Assert.AreEqual(0, transport.ReadSpectrum().Max());

        transport.AddSamples(rate, 2, stereo);
        Assert.IsGreaterThan(0, transport.ReadSpectrum().Max());
        transport.Paused = true;
        transport.Paused = false;
        Assert.AreEqual(0, transport.ReadSpectrum().Max());
    }
}

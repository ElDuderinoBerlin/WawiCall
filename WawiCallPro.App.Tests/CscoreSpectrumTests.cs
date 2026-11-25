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
}

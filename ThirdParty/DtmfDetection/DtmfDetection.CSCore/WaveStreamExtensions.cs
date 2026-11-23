using CSCore;

namespace DtmfDetection.CSCore;

public static class WaveStreamExtensions
{
    /// <summary>Detects DTMF tones by sample position. The caller retains ownership of the wave stream.</summary>
    public static IEnumerable<DtmfOccurence> DtmfTones(this IWaveSource waveFile, bool forceMono = true)
    {
        ArgumentNullException.ThrowIfNull(waveFile);
        var config = new DetectorConfig();
        var initial = waveFile.CanSeek ? TimeSpan.FromSeconds((double)waveFile.Position / waveFile.WaveFormat.BytesPerSecond) : TimeSpan.Zero;
        using var source = new StaticSampleSource(config, waveFile, forceMono);
        var audio = DtmfAudio.CreateFrom(source, config);
        var tones = new Queue<DtmfOccurence>();
        bool more;
        do
        {
            var position = initial + TimeSpan.FromSeconds((double)source.SamplesRead / source.Channels / source.SampleRate);
            more = audio.Forward((_, _) => position, (channel, start, tone) =>
                tones.Enqueue(new DtmfOccurence(tone, channel, start, position - start)));
            while (tones.TryDequeue(out var tone)) yield return tone;
        }
        while (more);
    }
}

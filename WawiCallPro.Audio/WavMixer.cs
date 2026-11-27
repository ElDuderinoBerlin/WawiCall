using System.Buffers.Binary;
using CSCore;
using CSCore.Codecs.WAV;
using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Audio;

// WAV path from WawiCallPro_old/TestCSCoreRecording/WavMixer and SimpleMixer.
// The recording worker supplies verified, time-aligned sources and owns publication.
internal static class WavMixer
{
    internal static long WriteMono(IWaveSource sent, IWaveSource received, Stream output, CancellationToken token)
    {
        using var sentSamples = sent.ToSampleSource();
        using var receivedSamples = received.ToSampleSource();
        var mixer = new SimpleMixer(sentSamples, receivedSamples);
        using var writer = new WaveWriter(output, sent.WaveFormat);
        var samples = new float[1024];
        var pcm = new byte[samples.Length * sizeof(short)];
        long clipped = 0;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            var count = mixer.Read(samples);
            if (count == 0) return clipped;
            for (var i = 0; i < count; i++)
            {
                // Undo CSCore's PCM16-to-float scaling before saturating the sum.
                var sample = (int)(samples[i] * 32768f);
                if (sample is < short.MinValue or > short.MaxValue) clipped++;
                BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(i * sizeof(short)), (short)Math.Clamp(sample, short.MinValue, short.MaxValue));
            }
            writer.Write(pcm, 0, count * sizeof(short));
        }
    }

    private sealed class SimpleMixer(ISampleSource sent, ISampleSource received)
    {
        private readonly ISampleSource[] _sources = [sent, received];
        private readonly float[] _input = new float[1024];

        // The original mixer adds corresponding samples, keeping the extent of the longest input.
        internal int Read(float[] buffer)
        {
            var stored = 0;
            foreach (var source in _sources)
            {
                var read = source.Read(_input, 0, buffer.Length);
                for (var i = 0; i < read; i++)
                    buffer[i] = i < stored ? buffer[i] + _input[i] : _input[i];
                stored = Math.Max(stored, read);
            }
            return stored;
        }
    }
}

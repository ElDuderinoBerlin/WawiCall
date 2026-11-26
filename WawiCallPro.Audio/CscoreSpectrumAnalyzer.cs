using System.Collections.Immutable;
using CSCore.DSP;
using CSCore.Utils;
using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Audio;

/// <summary>Collects bounded PCM and computes a CSCore FFT when the meter requests a frame.</summary>
internal sealed class CscoreSpectrumAnalyzer(TimeProvider clock)
{
    private const FftSize Size = FftSize.Fft2048;
    private static readonly TimeSpan SignalTimeout = TimeSpan.FromMilliseconds(350);
    private readonly Lock _gate = new();
    private readonly float[] _mono = new float[(int)Size];
    private readonly Complex[] _bins = new Complex[(int)Size];
    private FftProvider? _fft;
    private int _sampleRate;
    private long _lastSample;
    private ImmutableArray<int> _bands = AudioSpectrum.Silence;
    private int _lastGain = 100;

    internal void Add(int sampleRate, int channels, ReadOnlySpan<short> pcm) => Add(sampleRate, channels, pcm, null);

    internal void Add(int sampleRate, int channels, ReadOnlySpan<short> pcm, int? selectedChannel)
    {
        if (sampleRate <= 0 || channels <= 0 || pcm.Length < channels) return;
        lock (_gate)
        {
            if (_fft is null || _sampleRate != sampleRate)
            {
                _sampleRate = sampleRate;
                _fft = new FftProvider(1, Size) { WindowFunction = WindowFunctions.Hanning };
            }

            var frames = pcm.Length / channels;
            var count = Math.Min(frames, _mono.Length);
            var offset = (frames - count) * channels;
            for (var frame = 0; frame < count; frame++)
            {
                if (selectedChannel is { } selected)
                {
                    _mono[frame] = pcm[offset + selected] / 32768f;
                    offset += channels;
                    continue;
                }
                var sum = 0;
                for (var channel = 0; channel < channels; channel++) sum += pcm[offset++];
                _mono[frame] = sum / (32768f * channels);
            }
            _fft.Add(_mono, count);
            _lastSample = clock.GetTimestamp();
        }
    }

    internal ImmutableArray<int> Read(int gainPercent = 100)
    {
        lock (_gate)
        {
            if (_fft is null) return AudioSpectrum.Silence;
            if (clock.GetElapsedTime(_lastSample) >= SignalTimeout)
            {
                // Start with an empty window when input returns after device loss or silence.
                _fft = null;
                _bands = AudioSpectrum.Silence;
                return _bands;
            }
            if (_fft.IsNewDataAvailable || gainPercent != _lastGain)
            {
                _fft.GetFftData(_bins);
                _bands = ReadBands(gainPercent);
                _lastGain = gainPercent;
            }
            return _bands;
        }
    }

    private ImmutableArray<int> ReadBands(int gainPercent)
    {
        if (gainPercent == 0) return AudioSpectrum.Silence;
        var edges = AudioSpectrum.BandEdgesHz;
        var bands = ImmutableArray.CreateBuilder<int>(edges.Length - 1);
        for (var band = 0; band < edges.Length - 1; band++)
        {
            var first = (edges[band] * _bins.Length + _sampleRate - 1) / _sampleRate;
            var end = Math.Min(_bins.Length / 2, (edges[band + 1] * _bins.Length + _sampleRate - 1) / _sampleRate);
            var peak = 0f;
            for (var bin = first; bin < end; bin++) peak = Math.Max(peak, (float)_bins[bin]);
            // CSCore normalizes the FFT. Compensate the half-spectrum and Hann window before mapping -60 dBFS to zero.
            var decibels = 20 * MathF.Log10(Math.Max(peak * 0.04f * gainPercent, 0.001f));
            bands.Add((int)MathF.Round(Math.Clamp((decibels + 60) * (100f / 60), 0, 100)));
        }
        return bands.MoveToImmutable();
    }

    internal void Clear()
    {
        lock (_gate)
        {
            _fft = null;
            _bands = AudioSpectrum.Silence;
        }
    }
}

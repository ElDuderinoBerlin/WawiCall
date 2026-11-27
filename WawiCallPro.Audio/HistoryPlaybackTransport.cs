using System.Collections.Immutable;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Audio;

/// <summary>Metadata for the exact artifact verified for playback.</summary>
public sealed record HistoryPlaybackMedia(RecordingArtifactFormat Format, int SampleRate, int Channels, int BitsPerSample,
    TimeSpan Duration, bool CanSeek, string? FilePath);

/// <summary>Shares playback commands and measurements between one player and the audio worker.</summary>
public sealed class HistoryPlaybackTransport
{
    private static readonly TimeSpan SpectrumFallTime = TimeSpan.FromMilliseconds(450);
    private readonly TimeProvider _clock;
    private readonly CscoreSpectrumAnalyzer _leftSpectrum;
    private readonly CscoreSpectrumAnalyzer _rightSpectrum;
    private readonly Lock _spectrumGate = new();
    private readonly Lock _positionGate = new();
    private readonly float[] _displayedBands = new float[AudioSpectrum.BandEdgesHz.Length - 1];
    private long _lastSpectrumRead;
    private HistoryPlaybackMedia? _media;
    private long _position;
    private long _seek = -1;
    private long _applyingSeek = -1;
    private int _paused;
    private int _gain = 100;

    public HistoryPlaybackTransport() : this(TimeProvider.System) { }

    internal HistoryPlaybackTransport(TimeProvider clock)
    {
        _clock = clock;
        _leftSpectrum = new(clock);
        _rightSpectrum = new(clock);
        _lastSpectrumRead = clock.GetTimestamp();
    }

    public HistoryPlaybackMedia? Media => Volatile.Read(ref _media);
    public TimeSpan Position
    {
        get
        {
            lock (_positionGate)
            {
                // Keep the user's target visible until the decoder has reached the new position.
                var ticks = _seek >= 0 ? _seek : _applyingSeek >= 0 ? _applyingSeek : _position;
                return TimeSpan.FromTicks(ticks);
            }
        }
    }

    public bool Paused
    {
        get => Volatile.Read(ref _paused) != 0;
        set
        {
            Volatile.Write(ref _paused, value ? 1 : 0);
            if (value) ClearSpectrum();
        }
    }

    public void Seek(TimeSpan position)
    {
        if (Media is { CanSeek: true } media)
        {
            lock (_positionGate) _seek = Math.Clamp(position.Ticks, 0, media.Duration.Ticks);
        }
    }

    public ImmutableArray<int> ReadSpectrum()
    {
        lock (_spectrumGate)
        {
            var gain = Volatile.Read(ref _gain);
            if (Paused || SeekPending || gain == 0) return AudioSpectrum.Silence;
            var now = _clock.GetTimestamp();
            var fall = (float)(_clock.GetElapsedTime(_lastSpectrumRead, now) / SpectrumFallTime * 100);
            _lastSpectrumRead = now;
            var left = _leftSpectrum.Read(gain);
            var right = _rightSpectrum.Read(gain);
            var bands = ImmutableArray.CreateBuilder<int>(_displayedBands.Length);
            for (var band = 0; band < _displayedBands.Length; band++)
            {
                _displayedBands[band] = Math.Max(Math.Max(left[band], right[band]), _displayedBands[band] - fall);
                bands.Add((int)MathF.Round(_displayedBands[band]));
            }
            return bands.MoveToImmutable();
        }
    }

    internal bool SeekPending
    {
        get { lock (_positionGate) return _seek >= 0 || _applyingSeek >= 0; }
    }

    internal long TakeSeek()
    {
        lock (_positionGate)
        {
            if (_seek < 0) return -1;
            _applyingSeek = _seek;
            _seek = -1;
            return _applyingSeek;
        }
    }

    internal void CompleteSeek()
    {
        lock (_positionGate) _applyingSeek = -1;
    }

    internal void SetGain(int value) => Volatile.Write(ref _gain, value);
    internal void SetMedia(HistoryPlaybackMedia media) => Volatile.Write(ref _media, media);
    internal void SetPosition(TimeSpan position)
    {
        lock (_positionGate) _position = position.Ticks;
    }
    internal void AddSamples(int sampleRate, int channels, ReadOnlySpan<short> samples)
    {
        lock (_spectrumGate)
        {
            if (Paused || SeekPending) return;
            if (channels != 2)
            {
                _leftSpectrum.Add(sampleRate, channels, samples);
                return;
            }
            _leftSpectrum.Add(sampleRate, channels, samples, 0);
            _rightSpectrum.Add(sampleRate, channels, samples, 1);
        }
    }

    internal void ClearSpectrum()
    {
        lock (_spectrumGate)
        {
            _leftSpectrum.Clear();
            _rightSpectrum.Clear();
            Array.Clear(_displayedBands);
            _lastSpectrumRead = _clock.GetTimestamp();
        }
    }
}

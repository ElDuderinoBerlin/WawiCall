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
    private readonly CscoreSpectrumAnalyzer _spectrum = new(TimeProvider.System);
    private HistoryPlaybackMedia? _media;
    private long _position;
    private long _seek = -1;
    private int _paused;
    private int _gain = 100;

    public HistoryPlaybackMedia? Media => Volatile.Read(ref _media);
    public TimeSpan Position => TimeSpan.FromTicks(Interlocked.Read(ref _position));

    public bool Paused
    {
        get => Volatile.Read(ref _paused) != 0;
        set => Volatile.Write(ref _paused, value ? 1 : 0);
    }

    public void Seek(TimeSpan position)
    {
        if (Media is { CanSeek: true } media)
            Interlocked.Exchange(ref _seek, Math.Clamp(position.Ticks, 0, media.Duration.Ticks));
    }

    public ImmutableArray<int> ReadSpectrum() =>
        Paused ? AudioSpectrum.Silence : _spectrum.Read(Volatile.Read(ref _gain));

    internal bool SeekPending => Interlocked.Read(ref _seek) >= 0;
    internal long TakeSeek() => Interlocked.Exchange(ref _seek, -1);
    internal void SetGain(int value) => Volatile.Write(ref _gain, value);
    internal void SetMedia(HistoryPlaybackMedia media) => Volatile.Write(ref _media, media);
    internal void SetPosition(TimeSpan position) => Interlocked.Exchange(ref _position, position.Ticks);
    internal void AddSamples(int sampleRate, int channels, ReadOnlySpan<short> samples) => _spectrum.Add(sampleRate, channels, samples);
    internal void ClearSpectrum() => _spectrum.Clear();
}

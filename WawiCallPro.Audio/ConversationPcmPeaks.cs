using System.Collections.Immutable;
using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Audio;

internal sealed class ConversationPcmPeaks(TimeProvider clock) : IConversationPcmObserver
{
    private readonly Peak _microphone = new(clock);
    private readonly Peak _playback = new(clock);
    private readonly CscoreSpectrumAnalyzer _microphoneSpectrum = new(clock);
    private readonly CscoreSpectrumAnalyzer _playbackSpectrum = new(clock);

    internal int MicrophonePercent => _microphone.Read(100);
    internal int PlaybackPercent(int volume) => _playback.Read(volume);
    internal ImmutableArray<int> MicrophoneSpectrum => _microphoneSpectrum.Read();
    internal ImmutableArray<int> PlaybackSpectrum(int volume) => _playbackSpectrum.Read(volume);
    internal void ResetMicrophone() { _microphone.Clear(); _microphoneSpectrum.Clear(); }
    internal void ResetPlayback() { _playback.Clear(); _playbackSpectrum.Clear(); }

    public bool TryObserve(ConversationAudioSide side, int sampleRate, int channels, ReadOnlySpan<short> samples)
    {
        var amplitude = 0;
        foreach (var sample in samples) amplitude = Math.Max(amplitude, Math.Abs((int)sample));
        (side == ConversationAudioSide.Sent ? _microphone : _playback).Update(amplitude);
        (side == ConversationAudioSide.Sent ? _microphoneSpectrum : _playbackSpectrum).Add(sampleRate, channels, samples);
        return true;
    }

    private sealed class Peak(TimeProvider clock)
    {
        private static readonly TimeSpan Hold = TimeSpan.FromMilliseconds(350);
        private long _timestamp;
        private int _amplitude;

        internal void Update(int amplitude)
        {
            Volatile.Write(ref _amplitude, amplitude);
            Volatile.Write(ref _timestamp, clock.GetTimestamp());
        }

        internal int Read(int gainPercent) => clock.GetElapsedTime(Volatile.Read(ref _timestamp)) < Hold
            ? (Volatile.Read(ref _amplitude) * gainPercent + 16384) / 32768 : 0;

        internal void Clear() => Volatile.Write(ref _amplitude, 0);
    }
}

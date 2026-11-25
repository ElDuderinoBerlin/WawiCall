using System.Collections.Immutable;

namespace WawiCallPro.Interfaces.Audio;

/// <summary>Current per-call settings and recent PCM peaks in percent of full scale.</summary>
public sealed record ConversationAudioSnapshot(int PlaybackVolumePercent, bool MicrophoneMuted, int MicrophonePeakPercent, int PlaybackPeakPercent)
{
    public ImmutableArray<int> MicrophoneSpectrum { get; init; } = AudioSpectrum.Silence;
    public ImmutableArray<int> PlaybackSpectrum { get; init; } = AudioSpectrum.Silence;
}

/// <summary>Controls audio for a live call. A missing snapshot means the call has no controllable audio.</summary>
public interface IConversationAudioService
{
    ConversationAudioSnapshot? GetAudio(Guid callId);
    Task SetPlaybackVolumeAsync(Guid callId, int percent, CancellationToken cancellationToken = default);
    Task SetMicrophoneMutedAsync(Guid callId, bool muted, CancellationToken cancellationToken = default);
}

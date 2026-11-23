using System.Collections.Immutable;

namespace WawiCallPro.Interfaces.Audio;

public enum ConversationAudioSide { Sent, Received }
public enum ConversationWaveLayout { SeparatedStereo, MixedMono }

/// <summary>A finalized mono PCM16 WAV positioned relative to this recording's start, not the call's start.</summary>
public sealed record ConversationWaveSegment(string WavePath, ConversationAudioSide Side, TimeSpan Start)
{
    public override string ToString() => nameof(ConversationWaveSegment);
}

/// <summary>Explicit recording time window. Gaps and missing sides become silence; same-side overlaps are errors.</summary>
public sealed record ConversationWaveMixRequest(Guid CallId, Guid RecordingId, ImmutableArray<ConversationWaveSegment> Segments,
    string WavePath, int SampleRate, TimeSpan Duration, ConversationWaveLayout Layout = ConversationWaveLayout.SeparatedStereo)
{
    /// <summary>On success takes ownership of the flushed temporary WAV and its eventual publication.</summary>
    public Func<string, ConversationWaveMixResult, CancellationToken, Task>? StageWaveAsync { get; init; }
    public override string ToString() => nameof(ConversationWaveMixRequest);
}

/// <summary>Duration is derived from written frames. Mono clipping is counted; stereo keeps sent left and received right.</summary>
public sealed record ConversationWaveMixResult(Guid CallId, Guid RecordingId, long FrameCount, int SampleRate,
    int Channels, long ClippedSamples)
{
    public TimeSpan Duration => TimeSpan.FromSeconds(FrameCount / (double)SampleRate);
}

/// <summary>Mixes already finalized source segments on a worker and publishes only a finalized WAV.
/// All inputs are retained. Cancellation is observed until the non-overwriting destination rename.</summary>
public interface IConversationWaveMixer
{
    Task<ConversationWaveMixResult> MixAsync(ConversationWaveMixRequest request, CancellationToken cancellationToken = default);
}

public enum ConversationWaveMixFailure { InvalidWave, SourceUnavailable, TimelineConflict, DestinationUnavailable, MixingFailed, CleanupFailed }

/// <summary>Fixed diagnostic without filenames, source identities or native exception details.</summary>
public sealed class ConversationWaveMixException(ConversationWaveMixFailure failure) : Exception(failure switch
{
    ConversationWaveMixFailure.InvalidWave => "Eine Audioquelle enthält keine gültige, vollständige Mono-PCM16-WAV-Datei.",
    ConversationWaveMixFailure.SourceUnavailable => "Eine Audioquelle kann nicht zum Lesen geöffnet werden.",
    ConversationWaveMixFailure.TimelineConflict => "Audiosegmente überlappen auf derselben Gesprächsseite oder liegen außerhalb des Aufnahmezeitraums.",
    ConversationWaveMixFailure.DestinationUnavailable => "Die WAV-Zieldatei kann nicht erstellt werden oder ist bereits vorhanden.",
    ConversationWaveMixFailure.CleanupFailed => "Eine temporäre Mischdatei konnte nicht entfernt werden. Die Audioquellen bleiben erhalten.",
    _ => "Die Audiomischung ist fehlgeschlagen. Die Audioquellen bleiben erhalten."
})
{
    public ConversationWaveMixFailure Failure { get; } = failure;
}

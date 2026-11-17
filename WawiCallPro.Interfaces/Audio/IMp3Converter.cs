using System.Collections.Immutable;

namespace WawiCallPro.Interfaces.Audio;

/// <summary>An exact MP3 encoder format: Hz, channel count and bits per second.</summary>
public sealed record Mp3EncodingFormat(int SampleRate, int Channels, int BitRate);

/// <summary>Explicit local files. The finalized PCM16 WAV is retained; an existing destination is never replaced.</summary>
public sealed record Mp3ConversionRequest(string WavePath, string Mp3Path, Mp3EncodingFormat Format)
{
    public override string ToString() => nameof(Mp3ConversionRequest);
}

public sealed record Mp3ConversionResult(TimeSpan SourceDuration, long FileSize, Mp3EncodingFormat Format)
{
    /// <summary>Original finalized output fingerprint, calculated before publication. Legacy providers may
    /// omit it; their output cannot be confirmed by the persisted conversion workflow.</summary>
    public string? Sha256 { get; init; }
}

/// <summary>Runs off the caller thread. Success means the encoder has finalized and the complete MP3 is published.
/// Cancellation is observed until publication; cancellation after that commit does not undo a successful conversion.</summary>
public interface IMp3Converter
{
    Task<ImmutableArray<Mp3EncodingFormat>> GetSupportedFormatsAsync(CancellationToken cancellationToken = default);
    Task<Mp3ConversionResult> ConvertAsync(Mp3ConversionRequest request, CancellationToken cancellationToken = default);
}

public enum Mp3ConversionFailure
{
    EncoderUnavailable, UnsupportedFormat, InvalidWave, SourceUnavailable, DestinationUnavailable, EncodingFailed, CleanupFailed
}

/// <summary>Display-safe failure. Native exceptions and file paths are intentionally not attached.</summary>
public sealed class Mp3ConversionException(Mp3ConversionFailure failure) : Exception(failure switch
{
    Mp3ConversionFailure.EncoderUnavailable => "Der MP3-Encoder ist auf diesem System nicht verfügbar.",
    Mp3ConversionFailure.UnsupportedFormat => "Das gewählte Audioformat wird für diese MP3-Konvertierung nicht unterstützt.",
    Mp3ConversionFailure.InvalidWave => "Die WAV-Datei ist ungültig, unvollständig oder enthält kein unterstütztes PCM16-Audio.",
    Mp3ConversionFailure.SourceUnavailable => "Die WAV-Datei kann nicht zum Lesen geöffnet werden.",
    Mp3ConversionFailure.DestinationUnavailable => "Die MP3-Zieldatei kann nicht erstellt werden oder ist bereits vorhanden.",
    Mp3ConversionFailure.CleanupFailed => "Eine temporäre Audiodatei konnte nicht entfernt werden. Die WAV-Datei bleibt erhalten.",
    _ => "Die MP3-Konvertierung ist fehlgeschlagen. Die WAV-Datei bleibt erhalten."
})
{
    public Mp3ConversionFailure Failure { get; } = failure;
}

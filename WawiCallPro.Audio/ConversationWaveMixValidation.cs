using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Audio;

internal static class ConversationWaveMixValidation
{
    internal static long FrameAt(TimeSpan position, int rate) => (position.Ticks * rate + TimeSpan.TicksPerSecond / 2) / TimeSpan.TicksPerSecond;
    internal static int Channels(ConversationWaveMixRequest request) => request.Layout == ConversationWaveLayout.SeparatedStereo ? 2 : 1;

    internal static void Validate(ConversationWaveMixRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.CallId == Guid.Empty || request.RecordingId == Guid.Empty ||
            request.SampleRate is not (8000 or 16000 or 32000 or 44100 or 48000) || !Enum.IsDefined(request.Layout) ||
            request.Duration <= TimeSpan.Zero || request.Duration > TimeSpan.FromHours(8) || request.Segments.IsDefault || request.Segments.Length > 128)
            throw new ArgumentException("Gültige Aufnahmeidentitäten, Audioformate und ein begrenzter Aufnahmezeitraum sind erforderlich.");
        var frames = FrameAt(request.Duration, request.SampleRate);
        // CSCore WaveWriter maintains a signed 32-bit data length; reject before creating any files.
        if (frames == 0 || frames * Channels(request) * 2 > int.MaxValue - 44)
            throw new ArgumentException("Die Audiomischung überschreitet die unterstützte WAV-Größe.");
        ValidatePath(request.WavePath);
        foreach (var segment in request.Segments)
        {
            if (segment is null || !Enum.IsDefined(segment.Side) || segment.Start < TimeSpan.Zero || segment.Start >= request.Duration)
                throw new ArgumentException("Jedes Audiosegment benötigt eine gültige Gesprächsseite und Startposition.");
            ValidatePath(segment.WavePath);
        }
    }

    private static void ValidatePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || path.StartsWith(@"\\", StringComparison.Ordinal) ||
            path.AsSpan(2).Contains(':') || path.Any(char.IsControl) || !string.Equals(Path.GetExtension(path), ".wav", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Eine lokale absolute WAV-Datei ist erforderlich.");
    }
}

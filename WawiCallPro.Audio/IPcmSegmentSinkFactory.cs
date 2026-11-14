namespace WawiCallPro.Audio;

/// <summary>Worker-only boundary for CSCore/file IO. Each session owns all returned segment sinks.</summary>
public interface IPcmSegmentSinkFactory
{
    IPcmSegmentSink Open(string path, int sampleRate);
}

public interface IPcmSegmentSink : IDisposable
{
    void Write(ReadOnlySpan<short> samples);
    /// <summary>Finalizes and verifies the WAV, flushes to disk and closes it. Dispose alone is not a success signal.</summary>
    void Complete();
}

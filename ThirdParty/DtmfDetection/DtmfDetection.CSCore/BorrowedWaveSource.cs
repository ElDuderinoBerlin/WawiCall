using CSCore;

namespace DtmfDetection.CSCore;

// Conversion stages may dispose their input. The original stream belongs to the caller.
internal sealed class BorrowedWaveSource(IWaveSource source) : IWaveSource
{
    public WaveFormat WaveFormat => source.WaveFormat;
    public bool CanSeek => source.CanSeek;
    public long Position { get => source.Position; set => source.Position = value; }
    public long Length => source.Length;
    public int Read(byte[] buffer, int offset, int count) => source.Read(buffer, offset, count);
    public void Dispose() { }
}

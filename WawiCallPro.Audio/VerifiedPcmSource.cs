using CSCore;
using WawiCallPro.Core.Audio;

namespace WawiCallPro.Audio;

// Feed only the already verified data chunk into CSCore. Parsing the container a second time
// with WaveFileReader would lose RIFF padding on odd-sized metadata chunks in CSCore 1.2.1.5.
internal sealed class VerifiedPcmSource : IWaveSource
{
    private readonly Stream _stream;
    private readonly PcmWaveHeader _header;
    internal VerifiedPcmSource(Stream stream, PcmWaveHeader header)
    {
        _stream = stream;
        _header = header;
        WaveFormat = new WaveFormat(header.SampleRate, 16, header.Channels);
        Position = 0;
    }
    public WaveFormat WaveFormat { get; }
    public bool CanSeek => true;
    public long Length => _header.DataLength;
    public long Position
    {
        get => _stream.Position - _header.DataOffset;
        set
        {
            if (value < 0 || value > Length) throw new ArgumentOutOfRangeException(nameof(value));
            _stream.Position = _header.DataOffset + value - value % WaveFormat.BlockAlign;
        }
    }
    public int Read(byte[] buffer, int offset, int count)
    {
        count = (int)Math.Min(count, Length - Position);
        count -= count % WaveFormat.BlockAlign;
        _stream.ReadExactly(buffer.AsSpan(offset, count));
        return count;
    }
    public void Dispose() { /* The calling operation owns the source stream and its read lock. */ }
}

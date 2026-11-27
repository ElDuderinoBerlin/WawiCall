using System.Text;
using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Core.Audio;

/// <summary>Validated seekable PCM16 WAV metadata shared by recording, mixing and conversion services.</summary>
public sealed record PcmWaveHeader(int SampleRate, int Channels, long DataOffset, long DataLength)
{
    public TimeSpan Duration
    {
        get
        {
            // Use the same rounding as the recording journal, including half-tick frame boundaries.
            var frames = DataLength / (Channels * 2);
            var ticks = ((Int128)frames * TimeSpan.TicksPerSecond + SampleRate / 2) / SampleRate;
            return TimeSpan.FromTicks(checked((long)ticks));
        }
    }

    // Reject unfinished RIFF sizes and truncated chunks before a native decoder sees the input.
    public static PcmWaveHeader Read(Stream stream, CancellationToken token)
    {
        try
        {
            using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);
            if (stream.Length < 44 || reader.ReadUInt32() != 0x46464952 || reader.ReadUInt32() != stream.Length - 8 ||
                reader.ReadUInt32() != 0x45564157) throw Invalid();
            int sampleRate = 0, channels = 0;
            long dataLength = 0, dataOffset = 0;
            var chunks = 0;
            while (stream.Position < stream.Length)
            {
                token.ThrowIfCancellationRequested();
                if (++chunks > 4096 || stream.Length - stream.Position < 8) throw Invalid();
                var id = reader.ReadUInt32();
                var length = reader.ReadUInt32();
                var end = stream.Position + length + (length & 1);
                if (end > stream.Length) throw Invalid();
                if (id == 0x20746D66) // fmt
                {
                    if (sampleRate != 0 || length is not (16 or 18) || reader.ReadUInt16() != 1) throw Invalid();
                    channels = reader.ReadUInt16();
                    sampleRate = reader.ReadInt32();
                    var bytesPerSecond = reader.ReadInt32();
                    var alignment = reader.ReadUInt16();
                    var bits = reader.ReadUInt16();
                    if (channels is not (1 or 2) || sampleRate is < 8000 or > 192000 || bits != 16 ||
                        alignment != channels * 2 || bytesPerSecond != sampleRate * channels * 2 ||
                        length == 18 && reader.ReadUInt16() != 0) throw Invalid();
                }
                else if (id == 0x61746164) // data
                {
                    if (sampleRate == 0 || dataLength != 0 || length == 0 || length % (channels * 2) != 0) throw Invalid();
                    dataLength = length;
                    dataOffset = stream.Position;
                }
                stream.Position = end;
            }
            if (dataLength == 0) throw Invalid();
            return new PcmWaveHeader(sampleRate, channels, dataOffset, dataLength);
        }
        catch (EndOfStreamException) { throw Invalid(); }
        finally { stream.Position = 0; }
    }

    private static Mp3ConversionException Invalid() => new(Mp3ConversionFailure.InvalidWave);
}

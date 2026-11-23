using CSCore;
using CSCore.MediaFoundation;
using WawiCallPro.Core.Audio;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Utilities.Persistence;

namespace WawiCallPro.Audio;

internal sealed class RecordingFormatConverters(IMp3Converter mp3) : IRecordingFormatConverters
{
    private readonly WavToMp3Converter _wma = new(new MediaFoundationMp3Backend(wma: true), ".wma");
    private readonly WavToMp3Converter _ogg = new(new VorbisRecordingEncoder(), ".ogg");

    public IMp3Converter GetConverter(RecordingArtifactFormat format) => format switch
    {
        RecordingArtifactFormat.Mp3 => mp3,
        RecordingArtifactFormat.OggVorbis => _ogg,
        RecordingArtifactFormat.Wma => _wma,
        _ => throw new ArgumentOutOfRangeException(nameof(format))
    };

    internal static IWaveSource OpenPlayback(Stream stream, RecordingArtifactFormat format, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        stream.Position = 0;
        return format switch
        {
            RecordingArtifactFormat.Wave => new VerifiedPcmSource(stream, PcmWaveHeader.Read(stream, token)),
            RecordingArtifactFormat.OggVorbis => new VorbisPlaybackSource(stream).ToWaveSource(16),
            RecordingArtifactFormat.Mp3 or RecordingArtifactFormat.Wma => new MediaFoundationDecoder(new BorrowedStream(stream)),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
    }

    private sealed class VorbisPlaybackSource(Stream stream) : ISampleSource
    {
        private readonly NVorbis.VorbisReader _reader = new(stream, closeOnDispose: false);

        public WaveFormat WaveFormat => new(_reader.SampleRate, 32, _reader.Channels, AudioEncoding.IeeeFloat);
        public bool CanSeek => stream.CanSeek;
        public long Position
        {
            get => _reader.SamplePosition * _reader.Channels;
            set => _reader.SamplePosition = value / _reader.Channels;
        }
        public long Length => _reader.TotalSamples * _reader.Channels;
        public int Read(float[] buffer, int offset, int count) => _reader.ReadSamples(buffer, offset, count);
        public void Dispose() => _reader.Dispose();
    }
}

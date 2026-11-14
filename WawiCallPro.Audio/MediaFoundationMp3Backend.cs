using System.Collections.Immutable;
using System.Runtime.ExceptionServices;
using CSCore;
using CSCore.MediaFoundation;
using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Audio;

/// <summary>Uses CSCore Media Foundation for exact MP3 formats and DMO for sample-rate adaptation.
/// No microphone, playback endpoint, SIP session or desktop loopback is opened.</summary>
public sealed class MediaFoundationMp3Backend : IMp3EncodingBackend
{
    public ImmutableArray<Mp3EncodingFormat> GetSupportedFormats(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var types = ReadTypes();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return types.Select(Describe).Where(Supported).Distinct().OrderBy(item => item.SampleRate)
                .ThenBy(item => item.Channels).ThenBy(item => item.BitRate).ToImmutableArray();
        }
        finally { foreach (var type in types) type.Dispose(); }
    }

    public TimeSpan Encode(Stream wave, Stream mp3, Mp3EncodingFormat format, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var header = PcmWaveHeader.Read(wave, cancellationToken);
        if (header.Channels != format.Channels) throw new Mp3ConversionException(Mp3ConversionFailure.UnsupportedFormat);
        var types = ReadTypes();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var targetType = types.FirstOrDefault(type => Describe(type) == format && Supported(format));
            if (targetType is null) throw new Mp3ConversionException(types.Length == 0
                ? Mp3ConversionFailure.EncoderUnavailable : Mp3ConversionFailure.UnsupportedFormat);
            using var reader = new VerifiedPcmSource(wave, header);
            using var source = reader.ChangeSampleRate(format.SampleRate);
            using var inputType = MFMediaType.FromWaveFormat(source.WaveFormat);
            using var target = new BorrowedStream(mp3);
            // Do not use CreateMP3Encoder: it silently chooses the nearest bitrate and hides media-type ownership.
            using (var encoder = new FinalizingEncoder(target, inputType, targetType))
            {
                var bufferSize = source.WaveFormat.BytesPerSecond / 10;
                var buffer = new byte[bufferSize - bufferSize % source.WaveFormat.BlockAlign];
                int count;
                while ((count = source.Read(buffer, 0, buffer.Length)) != 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    encoder.Write(buffer, 0, count);
                    target.ThrowIfFaulted();
                }
                cancellationToken.ThrowIfCancellationRequested();
            } // FinalizeWriting may fail; that failure must reach the conversion task.
            target.ThrowIfFaulted();
            cancellationToken.ThrowIfCancellationRequested();
            return header.Duration;
        }
        finally { foreach (var type in types) type.Dispose(); }
    }

    private static MFMediaType[] ReadTypes()
    {
        try { return MediaFoundationEncoder.GetEncoderMediaTypes(AudioSubTypes.MpegLayer3); }
        catch (Exception) { throw new Mp3ConversionException(Mp3ConversionFailure.EncoderUnavailable); }
    }

    private static Mp3EncodingFormat Describe(MFMediaType type) => new(type.SampleRate, type.Channels, checked(type.AverageBytesPerSecond * 8));
    private static bool Supported(Mp3EncodingFormat format) => format.SampleRate is >= 8000 and <= 192000 &&
        format.Channels is 1 or 2 && format.BitRate is >= 8000 and <= 320000;

    // Feed only the already verified data chunk into CSCore. Parsing the container a second time
    // with WaveFileReader would lose RIFF padding on odd-sized metadata chunks in CSCore 1.2.1.5.
    private sealed class VerifiedPcmSource : IWaveSource
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
        public void Dispose() { /* The converter owns the source stream and its read lock. */ }
    }

    private sealed class FinalizingEncoder(Stream stream, MFMediaType input, MFMediaType output)
        : MediaFoundationEncoder(stream, input, output, TranscodeContainerTypes.MFTranscodeContainerType_MP3)
    {
        protected override void Dispose(bool disposing)
        {
            if (!disposing)
            {
                // This also runs for a partially constructed encoder. Never flush or finalize IO
                // from the finalizer thread; explicit disposal is the only success boundary.
                try { ReleaseUnfinished(); } catch (Exception) { }
                return;
            }
            try { base.Dispose(disposing); }
            catch (Exception)
            {
                // CSCore stops cleanup when FinalizeWriting fails. Release its remaining wrappers and
                // prevent the finalizer from retrying that failing operation on the finalizer thread.
                ReleaseUnfinished();
                throw;
            }
            finally { GC.SuppressFinalize(this); }
        }

        private void ReleaseUnfinished()
        {
            try { SinkWriter?.Dispose(); }
            finally
            {
                try { TargetStream?.Dispose(); }
                finally { TargetBaseStream?.Dispose(); }
            }
        }
    }

    private sealed class BorrowedStream(Stream stream) : Stream
    {
        private ExceptionDispatchInfo? _failure;
        public override bool CanRead => stream.CanRead;
        public override bool CanSeek => stream.CanSeek;
        public override bool CanWrite => stream.CanWrite;
        public override long Length => Guard(() => stream.Length);
        public override long Position { get => Guard(() => stream.Position); set => Guard(() => { stream.Position = value; }); }
        public override void Flush() => Guard(stream.Flush);
        public override int Read(byte[] buffer, int offset, int count) => Guard(() => stream.Read(buffer, offset, count));
        public override void Write(byte[] buffer, int offset, int count) => Guard(() => stream.Write(buffer, offset, count));
        public override long Seek(long offset, SeekOrigin origin) => Guard(() => stream.Seek(offset, origin));
        public override void SetLength(long value) => Guard(() => stream.SetLength(value));
        protected override void Dispose(bool disposing) { /* The converter owns the target stream, including Flush(true). */ }

        // A native sink can swallow failed COM stream callbacks. Remember the first managed error
        // across threads and observe it again after Write/FinalizeWriting, before publication.
        internal void ThrowIfFaulted() => Volatile.Read(ref _failure)?.Throw();
        private void Guard(Action action)
        {
            try { action(); }
            catch (Exception error) { Interlocked.CompareExchange(ref _failure, ExceptionDispatchInfo.Capture(error), null); throw; }
        }
        private T Guard<T>(Func<T> action)
        {
            try { return action(); }
            catch (Exception error) { Interlocked.CompareExchange(ref _failure, ExceptionDispatchInfo.Capture(error), null); throw; }
        }
    }
}

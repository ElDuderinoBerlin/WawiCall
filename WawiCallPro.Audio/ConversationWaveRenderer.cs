using System.Buffers.Binary;
using CSCore;
using CSCore.Codecs.WAV;
using CSCore.DSP;
using WawiCallPro.Core.Audio;
using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Audio;

/// <summary>Streaming two-side timeline mixer. Mono sums saturate; stereo preserves each side independently.</summary>
public sealed class ConversationWaveRenderer : IConversationWaveRenderer
{
    public ConversationWaveMixResult Render(ConversationWaveMixRequest request, Stream output, CancellationToken cancellationToken)
    {
        var sources = new List<Source>();
        try
        {
            var frames = ConversationWaveMixValidation.FrameAt(request.Duration, request.SampleRate);
            foreach (var segment in request.Segments)
            {
                cancellationToken.ThrowIfCancellationRequested();
                sources.Add(Source.Open(segment, request.SampleRate, frames, cancellationToken));
            }
            foreach (var side in Enum.GetValues<ConversationAudioSide>())
            {
                long previousEnd = 0;
                foreach (var source in sources.Where(item => item.Side == side).OrderBy(item => item.Start))
                {
                    if (source.Start < previousEnd) throw new ConversationWaveMixException(ConversationWaveMixFailure.TimelineConflict);
                    previousEnd = source.End;
                }
            }
            var channels = ConversationWaveMixValidation.Channels(request);
            if (channels == 1 && sources.Count == 2 && sources[0].Side != sources[1].Side &&
                sources.All(source => source.Start == 0 && source.End == frames && source.SampleRate == request.SampleRate))
            {
                using var alignedOutput = new BorrowedStream(output);
                using var first = sources[0].OpenAlignedWave();
                using var second = sources[1].OpenAlignedWave();
                var clippedSamples = WavMixer.WriteMono(first, second, alignedOutput, cancellationToken);
                alignedOutput.ThrowIfFaulted();
                cancellationToken.ThrowIfCancellationRequested();
                return new ConversationWaveMixResult(request.CallId, request.RecordingId, frames, request.SampleRate, channels, clippedSamples);
            }
            long clipped = 0;
            using var borrowed = new BorrowedStream(output);
            using (var writer = new WaveWriter(borrowed, new WaveFormat(request.SampleRate, 16, channels)))
            {
                byte[] sent = new byte[2048], received = new byte[2048], mixed = new byte[2048 * channels];
                for (long position = 0; position < frames;)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var count = (int)Math.Min(1024, frames - position);
                    Array.Clear(sent); Array.Clear(received);
                    foreach (var source in sources)
                        source.CopyTo(position, count, source.Side == ConversationAudioSide.Sent ? sent : received, cancellationToken);
                    for (var frame = 0; frame < count; frame++)
                    {
                        var left = BinaryPrimitives.ReadInt16LittleEndian(sent.AsSpan(frame * 2, 2));
                        var right = BinaryPrimitives.ReadInt16LittleEndian(received.AsSpan(frame * 2, 2));
                        if (channels == 2)
                        {
                            BinaryPrimitives.WriteInt16LittleEndian(mixed.AsSpan(frame * 4, 2), left);
                            BinaryPrimitives.WriteInt16LittleEndian(mixed.AsSpan(frame * 4 + 2, 2), right);
                        }
                        else
                        {
                            var sum = left + right;
                            if (sum is < short.MinValue or > short.MaxValue) clipped++;
                            BinaryPrimitives.WriteInt16LittleEndian(mixed.AsSpan(frame * 2, 2), (short)Math.Clamp(sum, short.MinValue, short.MaxValue));
                        }
                    }
                    writer.Write(mixed, 0, count * channels * 2);
                    borrowed.ThrowIfFaulted();
                    position += count;
                }
            }
            // WaveWriter versions can swallow errors while rewriting the header in Dispose.
            borrowed.ThrowIfFaulted();
            cancellationToken.ThrowIfCancellationRequested();
            return new ConversationWaveMixResult(request.CallId, request.RecordingId, frames, request.SampleRate, channels, clipped);
        }
        finally
        {
            Exception? releaseFailure = null;
            foreach (var source in sources)
            {
                try { source.Dispose(); }
                catch (Exception error) { releaseFailure ??= error; }
            }
            if (releaseFailure is not null) throw new ConversationWaveMixException(ConversationWaveMixFailure.MixingFailed);
        }
    }

    private sealed class Source(FileStream stream, PcmWaveHeader header, ConversationAudioSide side, long start, long end, int outputRate) : IDisposable
    {
        private IWaveSource? _reader;
        private readonly byte[] _buffer = new byte[8192];
        private int _bufferOffset;
        private int _bufferCount;
        internal ConversationAudioSide Side => side;
        internal long Start => start;
        internal long End => end;
        internal int SampleRate => header.SampleRate;
        internal IWaveSource OpenAlignedWave() => new VerifiedPcmSource(stream, header);

        internal static Source Open(ConversationWaveSegment segment, int outputRate, long totalFrames, CancellationToken token)
        {
            FileStream stream;
            try { stream = new FileStream(segment.WavePath, FileMode.Open, FileAccess.Read, FileShare.Read); }
            catch (Exception) { throw new ConversationWaveMixException(ConversationWaveMixFailure.SourceUnavailable); }
            try
            {
                PcmWaveHeader header;
                try { header = PcmWaveHeader.Read(stream, token); }
                catch (Mp3ConversionException) { throw new ConversationWaveMixException(ConversationWaveMixFailure.InvalidWave); }
                if (header.Channels != 1) throw new ConversationWaveMixException(ConversationWaveMixFailure.InvalidWave);
                var start = ConversationWaveMixValidation.FrameAt(segment.Start, outputRate);
                var length = (header.DataLength / 2 * outputRate + header.SampleRate / 2) / header.SampleRate;
                if (length == 0 || start + length > totalFrames)
                    throw new ConversationWaveMixException(ConversationWaveMixFailure.TimelineConflict);
                return new Source(stream, header, segment.Side, start, start + length, outputRate);
            }
            catch { stream.Dispose(); throw; }
        }

        internal void CopyTo(long position, int count, byte[] destination, CancellationToken token)
        {
            var first = Math.Max(position, Start);
            var last = Math.Min(position + count, End);
            if (last <= first) return;
            if (_reader is null)
            {
                IWaveSource raw = new VerifiedPcmSource(stream, header);
                if (header.SampleRate == outputRate) _reader = raw;
                else
                {
                    // CSCore DMO does not drain its filter at source EOF. Feed a bounded silent
                    // tail, then retain only the frames belonging to the original source duration.
                    raw = new ResamplerTailSource(raw);
                    try
                    {
                        var resampler = new DmoResampler(raw, outputRate);
                        try { resampler.Quality = 30; _reader = resampler; }
                        catch { resampler.Dispose(); throw; }
                    }
                    catch { raw.Dispose(); throw; }
                }
            }
            var offset = (int)(first - position) * 2;
            var wanted = (int)(last - first) * 2;
            while (wanted > 0)
            {
                token.ThrowIfCancellationRequested();
                if (_bufferCount == 0)
                {
                    // Fixed reads also avoid a zero-length input request from DMO's integer
                    // ratio calculation when a caller needs only a few remaining output frames.
                    _bufferCount = _reader.Read(_buffer, 0, _buffer.Length);
                    _bufferOffset = 0;
                    if (_bufferCount <= 0 || _bufferCount > _buffer.Length || _bufferCount % 2 != 0)
                        throw new ConversationWaveMixException(ConversationWaveMixFailure.MixingFailed);
                }
                var read = Math.Min(wanted, _bufferCount);
                Buffer.BlockCopy(_buffer, _bufferOffset, destination, offset, read);
                _bufferOffset += read; _bufferCount -= read;
                wanted -= read; offset += read;
            }
            if (last == End) { _reader.Dispose(); _reader = null; }
        }

        public void Dispose()
        {
            try { _reader?.Dispose(); }
            finally { stream.Dispose(); }
        }
    }

    private sealed class ResamplerTailSource(IWaveSource source) : IWaveSource
    {
        private long _position;
        public WaveFormat WaveFormat => source.WaveFormat;
        public bool CanSeek => false;
        public long Length => source.Length + source.WaveFormat.SampleRate / 10 * source.WaveFormat.BlockAlign;
        public long Position { get => _position; set => throw new NotSupportedException(); }
        public int Read(byte[] buffer, int offset, int count)
        {
            count = (int)Math.Min(count, Length - _position);
            count -= count % WaveFormat.BlockAlign;
            var audioBytes = (int)Math.Min(count, Math.Max(0, source.Length - _position));
            if (audioBytes > 0 && source.Read(buffer, offset, audioBytes) != audioBytes)
                throw new ConversationWaveMixException(ConversationWaveMixFailure.MixingFailed);
            Array.Clear(buffer, offset + audioBytes, count - audioBytes);
            _position += count;
            return count;
        }
        public void Dispose() => source.Dispose();
    }
}

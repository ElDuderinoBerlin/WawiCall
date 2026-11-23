using System.Collections.Immutable;
using CSCore;
using CSCore.MediaFoundation;
using WawiCallPro.Core.Audio;
using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Audio;

/// <summary>Uses CSCore Media Foundation for MP3 and WMA with DMO sample-rate adaptation.
/// No microphone, playback endpoint, SIP session or desktop loopback is opened.</summary>
public sealed class MediaFoundationMp3Backend : IMp3EncodingBackend
{
    private static readonly int[] RecordingRates = [8000, 16000, 32000, 44100, 48000];
    private readonly bool _wma;

    public MediaFoundationMp3Backend() { }

    internal MediaFoundationMp3Backend(bool wma) => _wma = wma;

    public ImmutableArray<Mp3EncodingFormat> GetSupportedFormats(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var types = ReadTypes();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var native = types.Select(Describe).Where(item => Supported(item) && (!_wma || item.BitRate % 1000 == 0)).Distinct();
            // WMA may require a different encoder rate than the recording. Keep the job's source format.
            var formats = _wma
                ? native.SelectMany(item => RecordingRates.Select(rate => item with { SampleRate = rate })).Distinct()
                : native;
            return formats.OrderBy(item => item.SampleRate).ThenBy(item => item.Channels).ThenBy(item => item.BitRate).ToImmutableArray();
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
            var targetType = types.Where(type => Supported(Describe(type)) && type.Channels == format.Channels
                    && type.AverageBytesPerSecond * 8 == format.BitRate && (_wma || type.SampleRate == format.SampleRate))
                .OrderBy(type => Math.Abs(type.SampleRate - format.SampleRate)).FirstOrDefault();
            if (targetType is null) throw new Mp3ConversionException(types.Length == 0
                ? Mp3ConversionFailure.EncoderUnavailable : Mp3ConversionFailure.UnsupportedFormat);
            using var reader = new VerifiedPcmSource(wave, header);
            using var source = reader.ChangeSampleRate(targetType.SampleRate);
            using var inputType = MFMediaType.FromWaveFormat(source.WaveFormat);
            using var target = new BorrowedStream(mp3);
            // Do not use CreateMP3Encoder: it silently chooses the nearest bitrate and hides media-type ownership.
            using (var encoder = new FinalizingEncoder(target, inputType, targetType, _wma
                ? TranscodeContainerTypes.MFTranscodeContainerType_ASF : TranscodeContainerTypes.MFTranscodeContainerType_MP3))
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

    private MFMediaType[] ReadTypes()
    {
        try { return MediaFoundationEncoder.GetEncoderMediaTypes(_wma ? AudioSubTypes.WindowsMediaAudio : AudioSubTypes.MpegLayer3); }
        catch (Exception) { throw new Mp3ConversionException(Mp3ConversionFailure.EncoderUnavailable); }
    }

    private static Mp3EncodingFormat Describe(MFMediaType type) => new(type.SampleRate, type.Channels, checked(type.AverageBytesPerSecond * 8));
    private static bool Supported(Mp3EncodingFormat format) => format.SampleRate is >= 8000 and <= 192000 &&
        format.Channels is 1 or 2 && format.BitRate is >= 8000 and <= 320000;

    private sealed class FinalizingEncoder(Stream stream, MFMediaType input, MFMediaType output, Guid container)
        : MediaFoundationEncoder(stream, input, output, container)
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
}

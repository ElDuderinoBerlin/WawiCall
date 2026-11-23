using System.Buffers.Binary;
using CSCore;
using CSCore.Codecs.WAV;
using CSCore.MediaFoundation;
using CSCore.Streams;
using SIPSorcery.Media;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.Audio;

public enum AudioWaveEncoding { Pcm16, ALaw }

public sealed record AudioWaveOptions(int? SampleRate, int? Channels, int VolumePercent,
    AudioWaveEncoding Encoding = AudioWaveEncoding.Pcm16);

/// <summary>Prepares WAV files and previews local audio independently of the call recording pipeline.</summary>
public interface IAudioFileConversion
{
    Task ConvertAsync(string source, string destination, AudioWaveOptions options, IProgress<int>? progress, CancellationToken token);
    Task PreviewAsync(string path, CancellationToken token);
}

public sealed class AudioFileConversionException(string message) : Exception(message);

public sealed class AudioFileConversion(IHistoryRecordingOutput output, ConfigurationStore settings) : IAudioFileConversion
{
    public const int ALawSampleRate = 8000;
    private const int ALawHeaderSize = 58;

    public Task ConvertAsync(string source, string destination, AudioWaveOptions options,
        IProgress<int>? progress, CancellationToken token) => Task.Run(() =>
    {
        if (options.Encoding is not (AudioWaveEncoding.Pcm16 or AudioWaveEncoding.ALaw)
            || options.SampleRate is < 8000 or > 48000 || options.Channels is not (null or 1 or 2)
            || options.VolumePercent is < 0 or > 100)
            throw new AudioFileConversionException("Abtastrate, Kanäle oder Lautstärke liegen außerhalb des gültigen Bereichs.");
        source = Path.GetFullPath(source);
        destination = Path.GetFullPath(destination);
        if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase))
            throw new AudioFileConversionException("Bitte eine andere Zieldatei als die Quelldatei auswählen.");
        if (!Path.GetExtension(destination).Equals(".wav", StringComparison.OrdinalIgnoreCase))
            throw new AudioFileConversionException("Die Zieldatei muss auf .wav enden.");
        if (File.Exists(destination))
            throw new AudioFileConversionException("Die Zieldatei existiert bereits. Bitte einen anderen Namen wählen.");

        using var input = File.OpenRead(source);
        using var decoder = Open(input, source, token);
        if (decoder.WaveFormat.Channels is not (1 or 2))
            throw new AudioFileConversionException("Die Quelldatei muss Mono- oder Stereo-Audio enthalten.");

        var alaw = options.Encoding == AudioWaveEncoding.ALaw;
        var sampleRate = alaw ? ALawSampleRate : options.SampleRate ?? decoder.WaveFormat.SampleRate;
        var channelCount = alaw ? 1 : options.Channels ?? decoder.WaveFormat.Channels;
        using var samples = decoder.ToSampleSource();
        using var channels = channelCount == 1 ? samples.ToMono() : samples.ToStereo();
        using var volume = new VolumeSource(channels) { Volume = options.VolumePercent / 100F };
        using var pcm = volume.ToWaveSource(16);
        using var converted = pcm.ChangeSampleRate(sampleRate);
        WriteWave(converted, destination, alaw, progress, token);
    }, token);

    public async Task PreviewAsync(string path, CancellationToken token)
    {
        var devices = (await settings.LoadAsync(token).ConfigureAwait(false)).Options.Devices;
        await Task.Run(() =>
        {
            using var stream = File.OpenRead(path);
            using var source = Open(stream, path, token);
            output.Play(source, devices, token);
        }, token).ConfigureAwait(false);
    }

    private static IWaveSource Open(Stream stream, string path, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension == ".wav")
        {
            try { return RecordingFormatConverters.OpenPlayback(stream, RecordingArtifactFormat.Wave, token); }
            catch (Mp3ConversionException error) when (error.Failure == Mp3ConversionFailure.InvalidWave)
            {
                stream.Position = 0;
                return new MediaFoundationDecoder(new BorrowedStream(stream));
            }
        }
        var format = extension switch
        {
            ".mp3" => RecordingArtifactFormat.Mp3,
            ".wma" => RecordingArtifactFormat.Wma,
            ".ogg" => RecordingArtifactFormat.OggVorbis,
            _ => throw new AudioFileConversionException("Unterstützte Quelldateien sind WAV, MP3, Ogg/Vorbis und WMA.")
        };
        return RecordingFormatConverters.OpenPlayback(stream, format, token);
    }

    private static void WriteWave(IWaveSource source, string destination, bool alaw, IProgress<int>? progress, CancellationToken token)
    {
        var folder = Path.GetDirectoryName(destination)!;
        var temporary = Path.Combine(folder, ".wawi-audio-" + Guid.NewGuid().ToString("N") + ".tmp");
        var created = false;
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write))
            {
                created = true;
                using var writer = alaw ? null : new WaveWriter(file, source.WaveFormat);
                if (alaw) file.Position = ALawHeaderSize;
                var buffer = new byte[64 * 1024];
                var expected = source.CanSeek ? source.Length : 0;
                long written = 0;
                var lastProgress = -1;
                while (true)
                {
                    token.ThrowIfCancellationRequested();
                    var count = source.Read(buffer, 0, buffer.Length);
                    if (count == 0) break;
                    written += count;
                    var outputBytes = alaw ? written / sizeof(short) : written;
                    if (outputBytes > int.MaxValue - ALawHeaderSize - 2)
                        throw new AudioFileConversionException("Die Ausgabedatei überschreitet die unterstützte WAV-Größe.");
                    if (writer is not null) writer.Write(buffer, 0, count);
                    else WriteALawSamples(file, buffer.AsSpan(0, count));
                    if (expected <= 0) continue;
                    var percent = (int)Math.Min(99L, written * 100L / expected);
                    if (percent == lastProgress) continue;
                    lastProgress = percent;
                    progress?.Report(percent);
                }
                if (written == 0) throw new AudioFileConversionException("Die Quelldatei enthält keine abspielbaren Audiodaten.");
                if (alaw) CompleteALawWave(file, checked((int)(written / sizeof(short))));
            }
            token.ThrowIfCancellationRequested();
            File.Move(temporary, destination);
            created = false;
            progress?.Report(100);
        }
        finally { if (created) File.Delete(temporary); }
    }

    private static void WriteALawSamples(Stream file, Span<byte> pcm)
    {
        var samples = pcm.Length / sizeof(short);
        for (var index = 0; index < samples; index++)
            pcm[index] = ALawEncoder.LinearToALawSample(BinaryPrimitives.ReadInt16LittleEndian(pcm[(index * sizeof(short))..]));
        file.Write(pcm[..samples]);
    }

    private static void CompleteALawWave(Stream file, int sampleCount)
    {
        var padding = sampleCount & 1;
        if (padding != 0) file.WriteByte(0);
        file.Position = 0;
        using var header = new BinaryWriter(file, System.Text.Encoding.ASCII, leaveOpen: true);
        header.Write("RIFF"u8);
        header.Write(ALawHeaderSize - 8 + sampleCount + padding);
        header.Write("WAVEfmt "u8);
        header.Write(18);
        header.Write((ushort)AudioEncoding.ALaw);
        header.Write((ushort)1);
        header.Write(ALawSampleRate);
        header.Write(ALawSampleRate);
        header.Write((ushort)1);
        header.Write((ushort)8);
        header.Write((ushort)0);
        header.Write("fact"u8);
        header.Write(4);
        header.Write(sampleCount);
        header.Write("data"u8);
        header.Write(sampleCount);
    }
}

using CSCore;
using CSCore.Codecs.WAV;
using CSCore.MediaFoundation;
using CSCore.Streams;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.Audio;

public sealed record AudioWaveOptions(int SampleRate, int Channels, int VolumePercent);

/// <summary>Prepares WAV files and previews local audio independently of the call recording pipeline.</summary>
public interface IAudioFileConversion
{
    Task ConvertAsync(string source, string destination, AudioWaveOptions options, IProgress<int>? progress, CancellationToken token);
    Task PreviewAsync(string path, CancellationToken token);
}

public sealed class AudioFileConversionException(string message) : Exception(message);

public sealed class AudioFileConversion(IHistoryRecordingOutput output, ConfigurationStore settings) : IAudioFileConversion
{
    public Task ConvertAsync(string source, string destination, AudioWaveOptions options,
        IProgress<int>? progress, CancellationToken token) => Task.Run(() =>
    {
        if (options.SampleRate is < 8000 or > 48000 || options.Channels is not (1 or 2) || options.VolumePercent is < 0 or > 100)
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

        using var samples = decoder.ToSampleSource();
        using var channels = options.Channels == 1 ? samples.ToMono() : samples.ToStereo();
        using var volume = new VolumeSource(channels) { Volume = options.VolumePercent / 100F };
        using var pcm = volume.ToWaveSource(16);
        using var converted = pcm.ChangeSampleRate(options.SampleRate);
        WriteWave(converted, destination, progress, token);
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

    private static void WriteWave(IWaveSource source, string destination, IProgress<int>? progress, CancellationToken token)
    {
        var folder = Path.GetDirectoryName(destination)!;
        var temporary = Path.Combine(folder, ".wawi-audio-" + Guid.NewGuid().ToString("N") + ".tmp");
        var created = false;
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write))
            {
                created = true;
                using var writer = new WaveWriter(file, source.WaveFormat);
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
                    if (written > int.MaxValue - 64)
                        throw new AudioFileConversionException("Die Ausgabedatei überschreitet die unterstützte WAV-Größe.");
                    writer.Write(buffer, 0, count);
                    if (expected <= 0) continue;
                    var percent = (int)Math.Min(99L, written * 100L / expected);
                    if (percent == lastProgress) continue;
                    lastProgress = percent;
                    progress?.Report(percent);
                }
                if (written == 0) throw new AudioFileConversionException("Die Quelldatei enthält keine abspielbaren Audiodaten.");
            }
            token.ThrowIfCancellationRequested();
            File.Move(temporary, destination);
            created = false;
            progress?.Report(100);
        }
        finally { if (created) File.Delete(temporary); }
    }
}

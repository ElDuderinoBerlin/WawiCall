using System.Runtime.InteropServices;
using CSCore;
using CSCore.MediaFoundation;
using WawiCallPro.Core.Audio;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.Audio;

/// <summary>Finds Windows ringtone WAVs in the local Media directory and opens validated selections.</summary>
internal sealed class WindowsRingtoneFiles(string? mediaDirectory)
{
    internal WindowsRingtoneFiles() : this(GetMediaDirectory()) { }

    internal IReadOnlyList<string> ReadNames() => EnumerateFiles().Select(path => Path.GetFileName(path)!)
        .Order(StringComparer.OrdinalIgnoreCase).ToArray();

    // The caller owns the validated stream. Compare against discovered names before opening a file.
    internal FileStream? TryOpen(string? fileName, CancellationToken token)
    {
        if (fileName is null) return null;
        var path = EnumerateFiles().FirstOrDefault(path => string.Equals(Path.GetFileName(path), fileName, StringComparison.OrdinalIgnoreCase));
        return path is null ? null : OpenWave(path, token, out _);
    }

    internal static FileStream? TryOpenCustom(string path, CancellationToken token, out PcmWaveHeader? format)
    {
        format = null;
        return ApplicationOptionsValidator.IsLocalRingtonePath(path)
            && Path.GetExtension(path).Equals(".wav", StringComparison.OrdinalIgnoreCase)
            ? OpenWave(path, token, out format) : null;
    }

    internal static IWaveSource? TryOpenCustomAudio(string path, CancellationToken token)
    {
        if (!ApplicationOptionsValidator.IsLocalRingtonePath(path)) return null;
        FileStream? stream = null;
        IWaveSource? decoder = null;
        try
        {
            token.ThrowIfCancellationRequested();
            stream = File.OpenRead(path);
            decoder = Path.GetExtension(path).Equals(".wav", StringComparison.OrdinalIgnoreCase)
                ? RecordingFormatConverters.OpenPlayback(stream, RecordingArtifactFormat.Wave, token)
                : AudioFileConversion.Open(stream, path, token);
            var format = decoder.WaveFormat;
            if (!decoder.CanSeek || format.Channels is not (1 or 2) || format.BlockAlign <= 0) return null;
            var frame = new byte[format.BlockAlign];
            if (decoder.Read(frame, 0, frame.Length) < frame.Length) return null;
            decoder.Position = 0;
            token.ThrowIfCancellationRequested();

            var result = new RingtoneFileSource(decoder, stream);
            stream = null;
            decoder = null;
            return result;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException
            or Mp3ConversionException or MediaFoundationException or COMException or ArgumentException
            or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
        finally
        {
            try { decoder?.Dispose(); }
            finally { stream?.Dispose(); }
        }
    }

    private static FileStream? OpenWave(string path, CancellationToken token, out PcmWaveHeader? format)
    {
        format = null;
        FileStream? stream = null;
        try
        {
            stream = File.OpenRead(path);
            format = PcmWaveHeader.Read(stream, token);
            return stream;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or Mp3ConversionException or ArgumentException)
        {
            stream?.Dispose();
            return null;
        }
        catch
        {
            stream?.Dispose();
            throw;
        }
    }

    private string[] EnumerateFiles()
    {
        if (mediaDirectory is null) return [];
        try { return Directory.EnumerateFiles(mediaDirectory, "*.wav").Where(IsRingtoneFile).ToArray(); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { return []; }
    }

    private static bool IsRingtoneFile(string path)
    {
        var name = Path.GetFileName(path);
        return name.StartsWith("Ring", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("Windows Ring", StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetMediaDirectory()
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        return windows.Length > 0 ? Path.Combine(windows, "Media") : null;
    }

    private sealed class RingtoneFileSource(IWaveSource decoder, FileStream stream) : WaveAggregatorBase(decoder)
    {
        protected override void Dispose(bool disposing)
        {
            try { base.Dispose(disposing); }
            finally { if (disposing) stream.Dispose(); }
        }
    }
}

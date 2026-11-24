using WawiCallPro.Core.Audio;
using WawiCallPro.Interfaces.Audio;

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
        if (path is null) return null;
        FileStream? stream = null;
        try
        {
            stream = File.OpenRead(path);
            _ = PcmWaveHeader.Read(stream, token);
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
}

using WawiCallPro.Utilities.Operations;

namespace WawiCallPro.Utilities.Persistence;

/// <summary>Reports absence only after checking path components. Access errors and links must not look like a missing WAV.</summary>
public interface IRecordingFileProbe
{
    bool IsAbsent(string relativePath);
}

public sealed class LocalRecordingFileProbe(ApplicationPaths paths) : IRecordingFileProbe
{
    public bool IsAbsent(string relativePath)
    {
        var resolved = paths.ResolveRecordingPath(relativePath);
        if (relativePath.Contains('\\') || relativePath.Split('/').Any(part => part is "" or "." or "..")) throw new RecordingOutboxException();
        var current = paths.RecordingsDirectory;
        var absent = IsMissing(current, directory: true);
        if (absent) return true;
        foreach (var part in relativePath.Split('/'))
        {
            current = Path.Combine(current, part);
            if (IsMissing(current, directory: current.Length < resolved.Length)) return true;
        }
        if (!string.Equals(current, resolved, StringComparison.OrdinalIgnoreCase)) throw new RecordingOutboxException();
        return false;
    }

    private static bool IsMissing(string path, bool directory)
    {
        try
        {
            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReparsePoint) != 0 || directory && (attributes & FileAttributes.Directory) == 0)
                throw new RecordingOutboxException();
            return false;
        }
        catch (FileNotFoundException) { return true; }
        catch (DirectoryNotFoundException) { return true; }
    }
}

using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.Utilities.Operations;

/// <summary>Local, product-specific paths. Resolving a path does not create files or directories.</summary>
public sealed class ApplicationPaths
{
    public ApplicationPaths(ConfigurationLocation configuration)
    {
        RootDirectory = Path.GetDirectoryName(configuration.FilePath)!;
        LogsDirectory = Path.Combine(RootDirectory, "logs");
        RecordingsDirectory = Path.Combine(RootDirectory, "recordings");
        BackupsDirectory = Path.Combine(RootDirectory, "backups");
    }

    public string RootDirectory { get; }
    public string LogsDirectory { get; }
    public string RecordingsDirectory { get; }
    public string BackupsDirectory { get; }

    public string ResolveRecordingPath(string relativePath) => ResolveRelative(RecordingsDirectory, relativePath);
    public string ResolveBackupPath(string relativePath) => ResolveRelative(BackupsDirectory, relativePath);

    private static string ResolveRelative(string root, string relativePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath) || relativePath.Contains(':'))
                throw OperationException.InvalidPath();
            var resolved = Path.GetFullPath(Path.Combine(root, relativePath));
            if (!resolved.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw OperationException.InvalidPath();
            return resolved;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            throw OperationException.InvalidPath();
        }
    }
}

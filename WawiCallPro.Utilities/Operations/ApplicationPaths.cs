using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.Utilities.Operations;

/// <summary>Local, product-specific paths. Resolving a path does not create files or directories.</summary>
public sealed class ApplicationPaths
{
    public ApplicationPaths(ConfigurationLocation configuration)
    {
        RootDirectory = Path.GetDirectoryName(configuration.FilePath)!;
        LogsDirectory = Path.Combine(RootDirectory, "logs");
        DefaultRecordingsDirectory = Path.Combine(RootDirectory, "recordings");
        BackupsDirectory = Path.Combine(RootDirectory, "backups");
    }

    public string RootDirectory { get; }
    public string LogsDirectory { get; }
    private RecordingStorageSettings _recordingStorage = new();
    public string DefaultRecordingsDirectory { get; }
    public string RecordingsDirectory => GetRecordingDirectory(_recordingStorage.Directory);
    public string RecordingFileNamePattern => _recordingStorage.FileNamePattern;
    public string BackupsDirectory { get; }

    public string ResolveRecordingPath(string relativePath) => ResolveRelative(RecordingsDirectory, relativePath);
    public string ResolveBackupPath(string relativePath) => ResolveRelative(BackupsDirectory, relativePath);

    internal string GetRecordingDirectory(string? configured) =>
        configured is null ? DefaultRecordingsDirectory : RecordingArchive.NormalizeDirectory(configured);

    internal void SetRecordingStorage(RecordingStorageSettings settings) => _recordingStorage = settings;

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

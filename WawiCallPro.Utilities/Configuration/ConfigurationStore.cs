namespace WawiCallPro.Utilities.Configuration;

/// <summary>A validated, detached startup snapshot. Configured means declaratively complete, not connected.</summary>
public sealed record ConfigurationSnapshot(ApplicationOptions Options, bool FileFound)
{
    public bool IsSipConfigured => Options.SipAccount is not null;
    public int FormatVersion { get; init; } = ConfigurationFormat.CurrentVersion;
    public bool RequiresUpgrade => FileFound && FormatVersion < ConfigurationFormat.CurrentVersion;
    internal string? ContentHash { get; init; }
    internal string? SourceFile { get; init; }
}

/// <summary>Strict typed JSON persistence. Invalid input is never repaired or replaced with defaults silently.</summary>
public sealed class ConfigurationStore(ConfigurationLocation location, ApplicationOptionsValidator validator)
{
    private const int MaximumFileSize = 131072;

    public async Task<ConfigurationSnapshot> LoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var bytes = await ReadBytesAsync(cancellationToken).ConfigureAwait(false);
            var snapshot = ConfigurationFormat.Decode(bytes);
            validator.Validate(snapshot.Options);
            return snapshot with { SourceFile = location.FilePath };
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return new ConfigurationSnapshot(new ApplicationOptions(), false) { SourceFile = location.FilePath };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw ConfigurationException.FileError("Die Datei konnte nicht gelesen werden.");
        }
    }

    /// <summary>Creates a file only. Updating an existing file requires the snapshot overload to prevent lost updates.</summary>
    public Task SaveAsync(ApplicationOptions options, CancellationToken cancellationToken = default) =>
        SaveCoreAsync(options, null, cancellationToken);

    /// <summary>Saves only if the file still matches the loaded snapshot. Callers reload after a successful save.</summary>
    public Task SaveAsync(ApplicationOptions options, ConfigurationSnapshot expected, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expected);
        if (!string.Equals(expected.SourceFile, location.FilePath, StringComparison.OrdinalIgnoreCase))
            throw ConfigurationException.FileError("Der Bearbeitungsstand gehört zu einer anderen Einstellungsdatei.");
        return SaveCoreAsync(options, expected.ContentHash, cancellationToken);
    }

    private async Task SaveCoreAsync(ApplicationOptions options, string? expectedHash, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        validator.Validate(options);
        var bytes = ConfigurationFormat.Encode(options);
        if (bytes.Length > MaximumFileSize) throw ConfigurationException.FileError("Die Datei ist zu groß.");
        var directory = Path.GetDirectoryName(location.FilePath)!;
        var temporaryFile = Path.Combine(directory, ".connection-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(directory);
            // The stable lock file serializes cooperating writers across the App and Console processes.
            await using var writeLock = AcquireWriteLock();
            var currentHash = await ReadCurrentHashAsync(cancellationToken).ConfigureAwait(false);
            if (!string.Equals(currentHash, expectedHash, StringComparison.Ordinal))
                throw ConfigurationException.FileError("Die Einstellungen wurden inzwischen geändert. Bitte neu laden.");
            await File.WriteAllBytesAsync(temporaryFile, bytes, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryFile, location.FilePath, true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw ConfigurationException.FileError("Die Datei konnte nicht gespeichert werden.");
        }
        finally
        {
            if (File.Exists(temporaryFile)) File.Delete(temporaryFile);
        }
    }

    private FileStream AcquireWriteLock()
    {
        try { return new FileStream(location.FilePath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException)
        {
            throw ConfigurationException.FileError("Die Einstellungen werden bereits gespeichert. Bitte erneut laden und versuchen.");
        }
    }

    private async Task<string?> ReadCurrentHashAsync(CancellationToken cancellationToken)
    {
        try { return ConfigurationFormat.ComputeHash(await ReadBytesAsync(cancellationToken).ConfigureAwait(false)); }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException) { return null; }
    }

    private async Task<byte[]> ReadBytesAsync(CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(location.FilePath, FileMode.Open, FileAccess.Read,
            FileShare.Read | FileShare.Delete, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (stream.Length > MaximumFileSize) throw ConfigurationException.FileError("Die Datei ist zu groß.");
        var bytes = new byte[(int)stream.Length];
        await stream.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
        return bytes;
    }
}

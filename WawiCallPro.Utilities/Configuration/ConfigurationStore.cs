using WawiCallPro.Utilities.Persistence;

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
public sealed class ConfigurationStore(
    ConfigurationLocation location,
    ApplicationOptionsValidator validator,
    PhoneSearchChanges? phoneSearchChanges = null)
{
    private const int MaximumFileSize = 131072;

    /// <summary>Exports saved portable preferences atomically. No credentials, connections or local identity mappings are included.</summary>
    public async Task ExportPortableAsync(string path, CancellationToken cancellationToken = default)
    {
        path = ValidateTransferPath(path);
        var snapshot = await LoadAsync(cancellationToken).ConfigureAwait(false);
        var bytes = PortableSettingsFormat.Encode(snapshot.Options);
        var temporary = Path.Combine(Path.GetDirectoryName(path)!, ".wawi-export-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
            {
                await output.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                await output.FlushAsync(cancellationToken).ConfigureAwait(false);
                output.Flush(true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, path, true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { throw ConfigurationException.FileError("Die Einstellungen konnten nicht exportiert werden. Ziel und Zugriffsrechte prüfen."); }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            { throw ConfigurationException.FileError("Die temporäre Exportdatei konnte nicht entfernt werden. Zielordner und Zugriffsrechte prüfen."); }
        }
    }

    /// <summary>Validates a portable JSON file, preserves local connections/identities and saves with the existing conflict guard.</summary>
    public async Task<ConfigurationSnapshot> ImportPortableAsync(string path, ConfigurationSnapshot expected, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expected);
        path = ValidateTransferPath(path);
        byte[] bytes;
        try
        {
            await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 4096, FileOptions.Asynchronous);
            if (input.Length > MaximumFileSize) throw ConfigurationException.FileError("Die Einstellungsdatei ist zu groß.");
            bytes = new byte[(int)input.Length];
            await input.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { throw ConfigurationException.FileError("Die Einstellungsdatei konnte nicht gelesen werden."); }
        var options = PortableSettingsFormat.Decode(bytes, expected.Options);
        await SaveAsync(options, expected, cancellationToken).ConfigureAwait(false);
        return await LoadAsync(cancellationToken).ConfigureAwait(false);
    }

    private string ValidateTransferPath(string path)
    {
        try
        {
            if (!Path.IsPathFullyQualified(path) || !string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Path.GetFileName(path), Path.GetFileName(location.FilePath), StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException();
            return Path.GetFullPath(path);
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
        { throw ConfigurationException.FileError("Eine JSON-Datei auswählen. Die interne Einstellungsdatei ist kein Import-/Exportziel."); }
    }

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
            phoneSearchChanges?.ConfigurationSaved(options);
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

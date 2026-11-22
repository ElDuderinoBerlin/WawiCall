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
    internal string? ProductFile { get; init; }
}

/// <summary>Strict typed JSON persistence. Invalid input is never repaired or replaced with defaults silently.</summary>
public sealed class ConfigurationStore(
    ConfigurationLocation location,
    ApplicationOptionsValidator validator,
    PhoneSearchChanges? phoneSearchChanges = null)
{
    private const int MaximumFileSize = 131072;
    public event Action<ErpConnectionSettings, bool>? ErpSaved;

    /// <summary>Creates dialog-local connection edits without changing the saved or running configuration.</summary>
    public PluginConnectionEditContext BeginPluginConnectionEdit(ConfigurationSnapshot snapshot, WawiCallPro.Utilities.Security.ISecretProtector secrets)
        => new(snapshot, secrets);

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
            if (location.ProductFilePath is not null)
            {
                var defaults = ApplyProductDefaults(new(new ApplicationOptions(), false));
                var target = System.Text.Json.Nodes.JsonNode.Parse(ConfigurationFormat.Encode(defaults.Options))!.AsObject();
                target.Remove("Logging");
                var localBytes = bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }) ? bytes[3..] : bytes;
                var local = System.Text.Json.Nodes.JsonNode.Parse(localBytes)!.AsObject();
                Merge(target["settings"]!.AsObject(), snapshot.FormatVersion == 0 ? local : local["settings"]!.AsObject());
                if (local["Logging"] is { } logging) target["Logging"] = logging.DeepClone();
                var merged = ConfigurationFormat.Decode(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(target));
                snapshot = snapshot with { Options = merged.Options with { ProductClientKeyAvailable = defaults.Options.ProductClientKeyAvailable } };
            }
            validator.Validate(snapshot.Options);
            return snapshot with { SourceFile = location.FilePath, ProductFile = location.ProductFilePath };
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            var defaults = ApplyProductDefaults(new ConfigurationSnapshot(new ApplicationOptions(), false));
            validator.Validate(defaults.Options);
            return defaults with { SourceFile = location.FilePath, ProductFile = location.ProductFilePath };
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
            using var keyEdit = options.ProductClientKeyEdit is { } edit ? new ProductAppSettings(edit.Path).BeginKeyUpdate(edit.Value, edit.ExpectedHash) : null;
            File.Move(temporaryFile, location.FilePath, true);
            keyEdit?.Commit();
            ErpSaved?.Invoke(options.Erp, options.ProductClientKeyEdit is not null);
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

    private ConfigurationSnapshot ApplyProductDefaults(ConfigurationSnapshot snapshot)
    {
        if (location.ProductFilePath is null) return snapshot;
        var productFile = new ProductAppSettings(location.ProductFilePath);
        var product = productFile.Read();
        snapshot = snapshot with { Options = snapshot.Options with { ProductClientKeyAvailable = productFile.ReadClientKey() is not null } };
        if (product["settings"] is not null and not System.Text.Json.Nodes.JsonObject) throw ConfigurationException.FileError("Ungültige appsettings.json-Vorgaben.");
        var defaults = product["settings"] as System.Text.Json.Nodes.JsonObject ?? new();
        var target = System.Text.Json.Nodes.JsonNode.Parse(ConfigurationFormat.Encode(new ApplicationOptions()))!.AsObject();
        target.Remove("Logging");
        Merge(target["settings"]!.AsObject(), defaults);
        if (product["AppUi"] is { } ui)
        {
            var user = target["settings"]!["user"]!;
            if (ui["FontFamily"] is { } family) user["fontFamily"] = family.DeepClone();
            if (ui["FontSize"] is { } size) user["fontSize"] = size.DeepClone();
            if (ui["ShowStartPage"] is { } start) user["showStartPage"] = start.DeepClone();
        }
        if (product["Serilog"]?["MinimumLevel"]?["Default"] is { } minimum)
            target["settings"]!["user"]!["logLevel"] = minimum.DeepClone();
        if (product["Logging"] is { } logging)
        {
            var baseOptions = ConfigurationFormat.Decode(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(target)).Options;
            baseOptions = baseOptions with { User = ConfigurationFormat.ApplyLogging(System.Text.Json.JsonSerializer.SerializeToElement(logging), baseOptions.User) };
            target = System.Text.Json.Nodes.JsonNode.Parse(ConfigurationFormat.Encode(baseOptions))!.AsObject();
        }
        target.Remove("Logging");
        if (snapshot.FileFound)
            Merge(target["settings"]!.AsObject(), System.Text.Json.Nodes.JsonNode.Parse(ConfigurationFormat.Encode(snapshot.Options))!["settings"]!.AsObject());
        var merged = ConfigurationFormat.Decode(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(target));
        return snapshot with { Options = merged.Options with { ProductClientKeyAvailable = snapshot.Options.ProductClientKeyAvailable } };
    }
    private static void Merge(System.Text.Json.Nodes.JsonObject target, System.Text.Json.Nodes.JsonObject source)
    {
        foreach (var pair in source)
        {
            if (pair.Value is System.Text.Json.Nodes.JsonObject child && target[pair.Key] is System.Text.Json.Nodes.JsonObject existing) Merge(existing, child);
            else target[pair.Key] = pair.Value?.DeepClone();
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
        await using var stream = new FileStream(File.Exists(location.FilePath) ? location.FilePath : location.LegacyFilePath, FileMode.Open, FileAccess.Read,
            FileShare.Read | FileShare.Delete, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (stream.Length > MaximumFileSize) throw ConfigurationException.FileError("Die Datei ist zu groß.");
        var bytes = new byte[(int)stream.Length];
        await stream.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
        return bytes;
    }
}

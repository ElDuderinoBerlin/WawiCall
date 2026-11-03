using System.Text.Json;
using System.Text.Json.Serialization;

namespace WawiCallPro.Utilities.Configuration;

/// <summary>A validated, detached startup snapshot. Configured means declaratively complete, not connected.</summary>
public sealed record ConfigurationSnapshot(ApplicationOptions Options, bool FileFound)
{
    public bool IsSipConfigured => Options.SipAccount is not null;
}

/// <summary>Strict typed JSON persistence. Invalid input is never repaired or replaced with defaults silently.</summary>
public sealed class ConfigurationStore(ConfigurationLocation location, ApplicationOptionsValidator validator)
{
    private const int MaximumFileSize = 131072;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false,
        RespectNullableAnnotations = true,
        MaxDepth = 16,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter<SipTransport>(allowIntegerValues: false) }
    };

    public async Task<ConfigurationSnapshot> LoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await using var stream = new FileStream(location.FilePath, FileMode.Open, FileAccess.Read,
                FileShare.Read | FileShare.Delete, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
            if (stream.Length > MaximumFileSize) throw ConfigurationException.FileError("Die Datei ist zu groß.");
            var options = await JsonSerializer.DeserializeAsync<ApplicationOptions>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
            if (options is null) throw ConfigurationException.FileError("Ein Konfigurationsobjekt ist erforderlich.");
            validator.Validate(options);
            return new ConfigurationSnapshot(options, true);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return new ConfigurationSnapshot(new ApplicationOptions(), false);
        }
        catch (JsonException)
        {
            throw ConfigurationException.FileError("Das JSON-Format, ein Feld oder ein geschützter Wert ist ungültig.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw ConfigurationException.FileError("Die Datei konnte nicht gelesen werden.");
        }
    }

    public async Task SaveAsync(ApplicationOptions options, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        validator.Validate(options);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(options, JsonOptions);
        if (bytes.Length > MaximumFileSize) throw ConfigurationException.FileError("Die Datei ist zu groß.");
        var directory = Path.GetDirectoryName(location.FilePath)!;
        var temporaryFile = Path.Combine(directory, ".connection-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(directory);
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
}

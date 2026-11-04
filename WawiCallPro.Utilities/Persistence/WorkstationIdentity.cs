using System.Text.Json;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Operations;

namespace WawiCallPro.Utilities.Persistence;

public interface IWorkstationIdentity
{
    Task<Guid?> GetAsync(CancellationToken cancellationToken = default);
    Task<Guid> InitializeAsync(CancellationToken cancellationToken = default);
}

/// <summary>One identity per local user/product configuration directory, shared by App and Console. No hardware fingerprint.</summary>
public sealed class WorkstationIdentity(ApplicationPaths paths) : IWorkstationIdentity
{
    private string FilePath => Path.Combine(paths.RootDirectory, "workstation.local.json");

    public async Task<Guid?> GetAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var file = await OpenReadAsync(cancellationToken).ConfigureAwait(false);
            if (file.Length is < 1 or > 1024) throw Invalid();
            using var document = await JsonDocument.ParseAsync(file, cancellationToken: cancellationToken).ConfigureAwait(false);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 2 ||
                !root.TryGetProperty("formatVersion", out var version) || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var format) || format != 1 ||
                !root.TryGetProperty("workstationId", out var identifier) || identifier.ValueKind != JsonValueKind.String) throw Invalid();
            var text = identifier.GetString();
            if (!Guid.TryParseExact(text, "N", out var id) || id == Guid.Empty || text != id.ToString("N")) throw Invalid();
            return id;
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException) { return null; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException) { throw Invalid(); }
    }

    public async Task<Guid> InitializeAsync(CancellationToken cancellationToken = default)
    {
        var existing = await GetAsync(cancellationToken).ConfigureAwait(false);
        if (existing is { } saved) return saved;
        cancellationToken.ThrowIfCancellationRequested();
        var id = Guid.NewGuid(); var temporary = Path.Combine(paths.RootDirectory, ".workstation-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            Directory.CreateDirectory(paths.RootDirectory);
            await using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024, useAsync: true))
            {
                await JsonSerializer.SerializeAsync(file, new { formatVersion = 1, workstationId = id.ToString("N") }, cancellationToken: cancellationToken).ConfigureAwait(false);
                await file.FlushAsync(cancellationToken).ConfigureAwait(false); file.Flush(flushToDisk: true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            try { File.Move(temporary, FilePath, overwrite: false); }
            catch (IOException) when (File.Exists(FilePath)) { return await GetAsync(cancellationToken).ConfigureAwait(false) ?? throw Invalid(); }
            return id;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { throw Invalid(); }
        finally { try { File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }

    private static ConfigurationException Invalid() => ConfigurationException.FileError("Die Arbeitsplatzkennung ist nicht verfügbar oder hat ein unbekanntes Format. Sie wurde nicht ersetzt.");

    private async Task<FileStream> OpenReadAsync(CancellationToken token)
    {
        for (var attempt = 0; ; attempt++)
        {
            token.ThrowIfCancellationRequested();
            try { return new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 1024, useAsync: true); }
            catch (IOException exception) when ((exception.HResult & 0xFFFF) is 32 or 33 && attempt < 10)
            {
                // A competing atomic publication can briefly hold Windows rename/delete access.
                await Task.Delay(10, token).ConfigureAwait(false);
            }
        }
    }
}

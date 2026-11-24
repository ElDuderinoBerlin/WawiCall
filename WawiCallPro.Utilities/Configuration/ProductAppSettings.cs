using System.Text.Json;
using System.Text.Json.Nodes;

namespace WawiCallPro.Utilities.Configuration;

/// <summary>Product-local defaults and portable client-key protection. Personal passwords remain CurrentUser DPAPI.</summary>
public sealed class ProductAppSettings(string path)
{
    private static readonly JsonDocumentOptions ReadOptions = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true, AllowDuplicateProperties = false, MaxDepth = 20 };
    public string FilePath { get; } = Path.GetFullPath(path);
    public JsonObject Read()
    {
        try
        {
            if (!File.Exists(FilePath)) return new();
            var bytes = File.ReadAllBytes(FilePath);
            if (bytes.Length > 131072) throw new JsonException();
            using var document = JsonDocument.Parse(bytes, ReadOptions);
            return JsonNode.Parse(document.RootElement.GetRawText()) as JsonObject ?? throw new JsonException();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        { throw ConfigurationException.FileError("Die lokale appsettings.json konnte nicht gelesen werden. Format und Zugriffsrechte prüfen."); }
    }
    public bool ReadDirectXRendering()
    {
        var root = Read();
        if (!root.TryGetPropertyValue("AppUi", out var ui)) return true;
        if (ui is JsonObject options)
        {
            if (!options.TryGetPropertyValue("UseDirectXRendering", out var preference)) return true;
            if (preference is JsonValue value && value.TryGetValue<bool>(out var enabled)) return enabled;
        }
        throw ConfigurationException.FileError("AppUi.UseDirectXRendering in der lokalen appsettings.json muss true oder false sein.");
    }

    public string? ReadClientKey()
    {
        string? raw;
        try { raw = Read()["LexwareApi"]?["ClientKey"]?.GetValue<string>()?.Trim(); }
        catch (InvalidOperationException) { throw ConfigurationException.FileError("Der lokale Lexware-Client-Key ist ungültig oder nicht lesbar."); }
        if (string.IsNullOrEmpty(raw)) return null;
        if (ValidKey(raw)) return raw;
        // Guid parsing permits surrounding whitespace. The API compares the actual credential string.
        if (ClientKeyEncryptor.TryDecrypt(raw, out var plaintext) && ValidKey(plaintext)) return plaintext.Trim();
        throw ConfigurationException.FileError("Der lokale Lexware-Client-Key ist ungültig oder nicht lesbar.");
    }
    public bool HasPlaintextClientKey() => ValidKey(Read()["LexwareApi"]?["ClientKey"]?.GetValue<string>());
    public bool EncryptClientKey()
    {
        return Update(root =>
        {
            var raw = root["LexwareApi"]?["ClientKey"]?.GetValue<string>()?.Trim();
            if (!ValidKey(raw))
            {
                if (string.IsNullOrWhiteSpace(raw)) throw ConfigurationException.FileError("Kein lokaler Client-Key vorhanden.");
                if (!ClientKeyEncryptor.TryDecrypt(raw, out var clear) || !ValidKey(clear))
                    throw ConfigurationException.FileError("Der lokale Lexware-Client-Key ist ungültig oder nicht lesbar.");
                return false;
            }
            root["LexwareApi"]!["ClientKey"] = ClientKeyEncryptor.Encrypt(raw!);
            return true;
        });
    }
    internal void WriteClientKey(string value)
    {
        if (!ValidKey(value)) throw ConfigurationException.FileError("Der Client-Key muss eine gültige GUID mit geschweiften Klammern sein.");
        Update(root =>
        {
            root["LexwareApi"] ??= new JsonObject();
            root["LexwareApi"]!["ClientKey"] = value.Trim();
            return true;
        });
    }
    internal string? Fingerprint() => File.Exists(FilePath) ? Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(FilePath))) : null;
    internal KeyUpdate BeginKeyUpdate(string value, string? expectedHash)
    {
        if (!ValidKey(value)) throw ConfigurationException.FileError("Der lokale Client-Key ist ungültig.");
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var fileLock = new FileStream(FilePath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        try
        {
            if (Fingerprint() != expectedHash) throw ConfigurationException.FileError("Die lokale appsettings.json wurde inzwischen geändert. Einstellungen erneut öffnen.");
            var original = File.Exists(FilePath) ? File.ReadAllBytes(FilePath) : null;
            var root = Read(); root["LexwareApi"] ??= new JsonObject(); root["LexwareApi"]!["ClientKey"] = value.Trim();
            WriteBytes(JsonSerializer.SerializeToUtf8Bytes(root, new JsonSerializerOptions { WriteIndented = true }));
            return new KeyUpdate(this, fileLock, original, Fingerprint());
        }
        catch { fileLock.Dispose(); throw; }
    }
    private void WriteBytes(byte[] bytes)
    {
        var temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { output.Write(bytes); output.Flush(true); }
            File.Move(temporary, FilePath, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    internal sealed class KeyUpdate(ProductAppSettings owner, FileStream fileLock, byte[]? original, string? writtenHash) : IDisposable
    {
        private bool _committed, _disposed;
        internal void Commit() => _committed = true;
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            try
            {
                if (!_committed && owner.Fingerprint() == writtenHash)
                { if (original is null) File.Delete(owner.FilePath); else owner.WriteBytes(original); }
            }
            finally { fileLock.Dispose(); }
        }
    }
    private bool Update(Func<JsonObject, bool> edit)
    {
        var temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            using var fileLock = new FileStream(FilePath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            var root = Read();
            if (!edit(root)) return false;
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, root, new JsonSerializerOptions { WriteIndented = true });
                stream.Flush(true);
            }
            File.Move(temporary, FilePath, true);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { throw ConfigurationException.FileError("Die lokale appsettings.json konnte nicht gespeichert werden. Zugriffsrechte prüfen."); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private static bool ValidKey(string? value) => Guid.TryParseExact(value?.Trim(), "B", out var key) && key != Guid.Empty;
}

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using WawiCallPro.Interfaces.Office;
using WawiCallPro.Utilities.Operations;
using WawiCallPro.Utilities.Security;

namespace WawiCallPro.Office;

/// <summary>Bounded chunking preserves the existing secret protector's per-value size limit.</summary>
public sealed class ProtectedMailData(ISecretProtector secrets)
{
    private static readonly JsonSerializerOptions Json = new() { MaxDepth = 4, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, AllowDuplicateProperties = false };
    public string Protect(string text, string purpose)
    {
        if (Encoding.UTF8.GetByteCount(text) > 2 * 1024 * 1024) throw new MailException(MailFailure.LimitExceeded);
        var count = Math.Max(1, (text.Length + 2999) / 3000);
        var binding = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(purpose))).ToLowerInvariant();
        var parts = Enumerable.Range(0, count).Select(index => secrets.Protect("p" + text.Substring(index * 3000, Math.Min(3000, text.Length - index * 3000)), "mail-v1-" + binding + "-" + index + "-" + count)).ToArray();
        return JsonSerializer.Serialize(new Envelope(1, parts), Json);
    }
    public string Unprotect(string encrypted, string purpose)
    {
        try
        {
            if (encrypted.Length > 4 * 1024 * 1024) throw new MailException(MailFailure.LimitExceeded);
            var envelope = JsonSerializer.Deserialize<Envelope>(encrypted, Json) ?? throw new JsonException();
            if (envelope.Version != 1 || envelope.Parts is not { Length: > 0 and <= 700 }) throw new JsonException();
            var result = new StringBuilder();
            var binding = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(purpose))).ToLowerInvariant();
            for (var index = 0; index < envelope.Parts.Length; index++)
            {
                using var part = secrets.Unprotect(envelope.Parts[index], "mail-v1-" + binding + "-" + index + "-" + envelope.Parts.Length);
                var text = part.Reveal(); if (!text.StartsWith('p')) throw new JsonException();
                result.Append(text.AsSpan(1));
            }
            if (Encoding.UTF8.GetByteCount(result.ToString()) > 2 * 1024 * 1024) throw new JsonException();
            return result.ToString();
        }
        catch (Exception) { throw new MailException(MailFailure.Configuration); }
    }
    private sealed record Envelope(int Version, ProtectedSecret[] Parts);
}

/// <summary>Account/client-bound token state. No plaintext fallback and no portable export.</summary>
public sealed class MailTokenStore(ApplicationPaths paths, ProtectedMailData encryption)
{
    private string Root => Path.Combine(paths.RootDirectory, "mail-tokens-v1");
    public static string Key(WawiCallPro.Utilities.Configuration.MailAccountSettings account) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        account.Id.ToString("N") + "\n" + account.Provider + "\n" + account.ClientId + "\n" + account.Tenant + "\n" + account.ApplicationAuthentication + "\n" + account.Mailbox))).ToLowerInvariant();
    public async Task<string?> ReadAsync(string key, CancellationToken token)
    {
        try
        {
            ValidateKey(key); var path = Path.Combine(Root, key + ".json");
            if (!Directory.Exists(Root) || !File.Exists(path)) return null;
            CheckLink(Root); CheckLink(path);
            await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
            if (file.Length is < 1 or > 4194304) throw new MailException(MailFailure.Configuration);
            using var reader = new StreamReader(file);
            return encryption.Unprotect(await reader.ReadToEndAsync(token), "mail-token-v1:" + key);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception) { throw new MailException(MailFailure.SignInRequired); }
    }
    public async Task WriteAsync(string key, string text, CancellationToken token)
    {
        ValidateKey(key); Directory.CreateDirectory(Root); CheckLink(Root);
        var path = Path.Combine(Root, key + ".json"); var temporary = Path.Combine(Root, "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            var encrypted = encryption.Protect(text, "mail-token-v1:" + key);
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, true))
            { await output.WriteAsync(Encoding.UTF8.GetBytes(encrypted), token); await output.FlushAsync(token); output.Flush(true); }
            token.ThrowIfCancellationRequested(); if (File.Exists(path)) CheckLink(path); File.Move(temporary, path, true);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception) { throw new MailException(MailFailure.Configuration); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public void Delete(string key)
    { ValidateKey(key); if (!Directory.Exists(Root)) return; CheckLink(Root); var path = Path.Combine(Root, key + ".json"); if (File.Exists(path)) { CheckLink(path); File.Delete(path); } }
    public async Task<IAsyncDisposable> AcquireAsync(string key, CancellationToken token)
    {
        ValidateKey(key); Directory.CreateDirectory(Root); CheckLink(Root); var path = Path.Combine(Root, key + ".lock");
        for (var attempt = 0; ; attempt++)
        {
            token.ThrowIfCancellationRequested();
            try { if (File.Exists(path)) CheckLink(path); return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose); }
            catch (IOException) when (attempt < 50) { await Task.Delay(100, token); }
            catch (Exception) { throw new MailException(MailFailure.Conflict); }
        }
    }
    private static void ValidateKey(string key) { if (key.Length != 64 || key.Any(ch => !(ch is >= '0' and <= '9' or >= 'a' and <= 'f'))) throw new MailException(MailFailure.Configuration); }
    private static void CheckLink(string path) { if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new MailException(MailFailure.Configuration); }
}

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using WawiCallPro.Interfaces.Office;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.Office;

internal sealed class MailCursorExpiredException : Exception;

/// <summary>Bearer tokens are sent only to the fixed provider/account path. Redirects are disabled by the owner.</summary>
public sealed class MailHttp(HttpClient client, IMailAuthentication authentication)
{
    public async Task<JsonElement?> GetAsync(MailAccountSettings account, Uri root, string path, CancellationToken token,
        bool missing = false, bool cursor = false)
    {
        try
        {
            var uri = new Uri(root, path);
            if (uri.Scheme != "https" || uri.Host != root.Host || uri.Port != 443 || !uri.AbsolutePath.StartsWith(root.AbsolutePath, StringComparison.Ordinal) ||
                uri.UserInfo.Length != 0 || uri.Fragment.Length != 0 || uri.AbsoluteUri.Length > 32768) throw new MailException(MailFailure.InvalidResponse);
            for (var attempt = 0; attempt < 4; attempt++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                var access = await authentication.GetAsync(account, attempt == 1, token);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access.Value);
                if (account.Provider == MailProviderKind.Microsoft365) request.Headers.TryAddWithoutValidation("Prefer", "IdType=\"ImmutableId\"");
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
                if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 0) continue;
                if ((int)response.StatusCode is 429 or 503 && attempt < 3)
                {
                    var delay = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(attempt + 1);
                    if (delay < TimeSpan.Zero || delay > TimeSpan.FromSeconds(30)) throw new MailException(MailFailure.ProviderUnavailable);
                    await Task.Delay(delay, token); continue;
                }
                if (cursor && response.StatusCode is HttpStatusCode.Gone or HttpStatusCode.NotFound) throw new MailCursorExpiredException();
                if (missing && response.StatusCode == HttpStatusCode.NotFound) return null;
                if (!response.IsSuccessStatusCode) throw new MailException(response.StatusCode switch
                { HttpStatusCode.Unauthorized => MailFailure.SignInRequired, HttpStatusCode.Forbidden => MailFailure.AccessDenied, _ => MailFailure.ProviderUnavailable });
                var maximum = uri.AbsolutePath.Contains("/attachments/", StringComparison.Ordinal) ? MailLimits.AttachmentResponseBytes : 8 * 1024 * 1024;
                if (response.Content.Headers.ContentLength > maximum) throw new MailException(MailFailure.LimitExceeded);
                await using var input = await response.Content.ReadAsStreamAsync(token);
                using var output = new MemoryStream(); var buffer = new byte[65536]; int read;
                while ((read = await input.ReadAsync(buffer, token)) != 0)
                { if (output.Length + read > maximum) throw new MailException(MailFailure.LimitExceeded); await output.WriteAsync(buffer.AsMemory(0, read), token); }
                using var document = JsonDocument.Parse(output.ToArray(), new JsonDocumentOptions { MaxDepth = 64, AllowDuplicateProperties = false });
                return document.RootElement.Clone();
            }
            throw new MailException(MailFailure.ProviderUnavailable);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (MailCursorExpiredException) { throw; }
        catch (MailException) { throw; }
        catch (Exception) { throw new MailException(MailFailure.InvalidResponse); }
    }
}

internal static class MailJson
{
    internal static string Text(this JsonElement item, string name, string fallback = "") => item.ValueKind == JsonValueKind.Object && item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? fallback : fallback;
    internal static IEnumerable<JsonElement> Items(this JsonElement item, string name) => item.ValueKind == JsonValueKind.Object && item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array ? value.EnumerateArray() : [];
    internal static JsonElement Child(this JsonElement item, string name) => item.ValueKind == JsonValueKind.Object && item.TryGetProperty(name, out var value) ? value : default;
    internal static bool Flag(this JsonElement item, string name) => item.Child(name).ValueKind == JsonValueKind.True;
    internal static long? Number(this JsonElement item, string name) => item.Child(name) is { ValueKind: JsonValueKind.Number } number && number.TryGetInt64(out var value) ? value : null;
    internal static string Required(this JsonElement item, string name) => item.Text(name) is { Length: > 0 and <= 4096 } value ? value : throw new MailException(MailFailure.InvalidResponse);
    internal static DateTimeOffset Date(this JsonElement item, string name) => DateTimeOffset.TryParse(item.Text(name), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var date) ? date.ToUniversalTime() : throw new MailException(MailFailure.InvalidResponse);
    internal static string Encode(string text) => Uri.EscapeDataString(text);
}

using System.Net;
using System.Text.Json;

namespace WawiCallPro.Plugins.Lexware.Pro;

public interface IProHttpHandlerFactory
{
    HttpMessageHandler Create();
}

public sealed class ProHttpHandlerFactory : IProHttpHandlerFactory
{
    public HttpMessageHandler Create() => new SocketsHttpHandler { AllowAutoRedirect = false };
}

/// <summary>Preserves structured failures across the API client's nullable return convention.</summary>
internal sealed class ProResponseGuard(HttpMessageHandler inner) : DelegatingHandler(inner)
{
    // Owned by a single serialized session; no request can overwrite another request's failure.
    public ProFailure? Failure { get; private set; }
    private bool _expectCompanies;
    public void Reset(bool expectCompanies = false) { Failure = null; _expectCompanies = expectCompanies; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try { response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false); }
        catch (OperationCanceledException) { Failure = ProFailure.Timeout; throw; }
        catch (HttpRequestException) { Failure = ProFailure.ApiUnavailable; throw; }
        try
        {
            if (!response.IsSuccessStatusCode)
            {
                Failure = response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized => ProFailure.InvalidToken,
                    HttpStatusCode.Forbidden => ProFailure.AuthenticationFailed,
                    _ => ProFailure.ApiUnavailable
                };
                return response;
            }
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;
            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0)
            {
                // Retry only an unambiguous token expiration/rejection, never permissions or mixed errors.
                var failures = errors.EnumerateArray().Select(ClassifyError).Distinct().ToArray();
                Failure = failures.Length == 1 ? failures[0] : ProFailure.InvalidResponse;
            }
            else if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object
                || (root.TryGetProperty("errors", out var malformed) && malformed.ValueKind is not (JsonValueKind.Array or JsonValueKind.Null)))
                Failure = ProFailure.InvalidResponse;
            // The upstream companies model defaults a missing field to an empty list. Require the field
            // explicitly so malformed successful responses cannot appear as an authorized empty catalog.
            else if (_expectCompanies && (!data.TryGetProperty("companies", out var companies) || companies.ValueKind != JsonValueKind.Array))
                Failure = ProFailure.InvalidResponse;
            return response;
        }
        catch (OperationCanceledException) { response.Dispose(); throw; }
        catch { Failure = ProFailure.InvalidResponse; response.Dispose(); throw; }
    }

    private static ProFailure ClassifyError(JsonElement error)
    {
        if (error.ValueKind != JsonValueKind.Object || !error.TryGetProperty("extensions", out var extensions)
            || extensions.ValueKind != JsonValueKind.Object || !extensions.TryGetProperty("code", out var code)) return ProFailure.InvalidResponse;
        var value = code.ValueKind == JsonValueKind.String ? code.GetString()
            : code.ValueKind == JsonValueKind.Number && code.TryGetInt32(out var number) ? number.ToString(System.Globalization.CultureInfo.InvariantCulture) : null;
        // Only documented codes are classified; error messages and arbitrary extension fields are never retained.
        return value switch
        {
            "1002" or "1003" => ProFailure.ConfigurationRequired,
            "1005" or "1006" or "1007" => ProFailure.AuthenticationFailed,
            "1010" or "2004" => ProFailure.PermissionDenied,
            "2001" => ProFailure.ClientKeyMissing,
            "2002" => ProFailure.ClientKeyUnknown,
            "2003" or "3003" => ProFailure.ClientKeyUnauthorized,
            "2005" => ProFailure.ClientConfigurationExpired,
            "2006" => ProFailure.ClientConfigurationMissing,
            "2007" => ProFailure.ClientConfigurationInvalid,
            "3001" or "3002" => ProFailure.InvalidToken,
            "4002" => ProFailure.ApiUnavailable,
            _ => ProFailure.InvalidResponse
        };
    }
}

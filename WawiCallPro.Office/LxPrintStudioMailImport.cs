using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WawiCallPro.Interfaces.Office;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Security;

namespace WawiCallPro.Office;

/// <summary>Explicit local import. Reprotects only the selected Microsoft app secret; never opens a mailbox or changes the source.</summary>
public sealed class LxPrintStudioMailImport(ISecretProtector secrets)
{
    public async Task<IReadOnlyList<MailAccountSettings>> ReadAsync(string path, CancellationToken token)
    {
        try
        {
            await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
            if (file.Length is < 1 or > 4 * 1024 * 1024) throw new MailException(MailFailure.Configuration);
            using var document = await JsonDocument.ParseAsync(file, new JsonDocumentOptions { MaxDepth = 32, AllowDuplicateProperties = false }, token);
            var delivery = document.RootElement.GetProperty("EmailDelivery"); var result = new List<MailAccountSettings>();
            foreach (var profile in delivery.GetProperty("Profiles").EnumerateArray())
            {
                if (profile.Text("Transport") != "Microsoft365Graph") continue;
                var settings = profile.GetProperty("Microsoft365");
                if (settings.Text("AuthenticationMode") != "ClientSecret") continue;
                var credentialId = settings.Required("ClientSecretCredentialId");
                var stored = delivery.GetProperty("ProtectedMicrosoft365ClientSecrets").GetProperty(credentialId);
                if (stored.Number("Version") != 1 || !OperatingSystem.IsWindows()) throw new MailException(MailFailure.Configuration);
                var plaintext = ProtectedData.Unprotect(Convert.FromBase64String(stored.Required("ProtectedSecret")), Encoding.UTF8.GetBytes("LxPrintStudio.Microsoft365ClientSecret.v1|" + credentialId), DataProtectionScope.CurrentUser);
                try
                {
                    var identity = settings.Required("TenantId") + "\n" + settings.Required("ClientId") + "\n" + profile.Required("Sender");
                    result.Add(new() { Id = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(identity)).AsSpan(0, 16)), Provider = MailProviderKind.Microsoft365,
                        DisplayName = profile.Text("Name", "Microsoft 365"), ClientId = settings.Required("ClientId"), Tenant = settings.Required("TenantId"),
                        Mailbox = profile.Required("Sender"), ApplicationAuthentication = true, MicrosoftClientSecret = secrets.Protect(Encoding.UTF8.GetString(plaintext), MailAccountSettings.MicrosoftSecretPurpose) });
                }
                finally { CryptographicOperations.ZeroMemory(plaintext); }
            }
            if (result.Count == 0) throw new MailException(MailFailure.Configuration);
            return result;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception) { throw new MailException(MailFailure.Configuration); }
    }
}

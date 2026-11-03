using System.Collections.Immutable;
using WawiCallPro.Utilities.Security;

namespace WawiCallPro.Utilities.Configuration;

public sealed class ApplicationOptionsValidator(ISecretProtector secrets)
{
    public void Validate(ApplicationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.SipAccount is not { } account) return;
        var issues = ImmutableArray.CreateBuilder<ConfigurationIssue>();
        if (string.IsNullOrWhiteSpace(account.Server) || account.Server != account.Server.Trim() ||
            Uri.CheckHostName(account.Server) == UriHostNameType.Unknown)
            issues.Add(new("sipAccount.server", "Ein Hostname oder eine IP-Adresse ohne URI und Zugangsdaten ist erforderlich."));
        if (account.Port is < 1 or > 65535)
            issues.Add(new("sipAccount.port", "Der Port muss zwischen 1 und 65535 liegen."));
        if (!Enum.IsDefined(account.Transport))
            issues.Add(new("sipAccount.transport", "Erlaubt sind Udp, Tcp und Tls."));
        if (!IsValidUsername(account.Username))
            issues.Add(new("sipAccount.username", "Ein Benutzername ohne Leerraum und Steuerzeichen ist erforderlich."));
        if (account.AuthenticationUsername is not null && !IsValidUsername(account.AuthenticationUsername))
            issues.Add(new("sipAccount.authenticationUsername", "Der optionale Authentifizierungsname ist ungültig."));
        if (account.Password is null)
            issues.Add(new("sipAccount.password", "Ein geschütztes Kennwort ist erforderlich."));
        else
        {
            try
            {
                using var password = secrets.Unprotect(account.Password, SipAccountOptions.PasswordPurpose);
                if (password.IsEmpty) issues.Add(new("sipAccount.password", "Das geschützte Kennwort ist leer."));
            }
            catch (SecretProtectionException)
            {
                issues.Add(new("sipAccount.password", "Das geschützte Kennwort ist für dieses Benutzerkonto nicht verfügbar oder beschädigt."));
            }
        }
        if (issues.Count != 0) throw new ConfigurationException(issues.ToImmutable());
    }

    private static bool IsValidUsername(string? username) =>
        !string.IsNullOrWhiteSpace(username) && username.Length <= 256 && !username.Any(character => char.IsWhiteSpace(character) || char.IsControl(character));
}

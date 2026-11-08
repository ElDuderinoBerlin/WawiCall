using WawiCallPro.Utilities.Security;
using System.Collections.Immutable;
using WawiCallPro.Interfaces.Companies;

namespace WawiCallPro.Utilities.Configuration;

public enum SipTransport
{
    Udp = 1,
    Tcp = 2,
    Tls = 3
}

/// <summary>Independent scopes in one immutable settings snapshot; no company setting overrides workstation credentials.</summary>
public sealed record ApplicationOptions
{
    public ErpConnectionSettings Erp { get; init; } = new();
    public SipAccountOptions? SipAccount { get; init; }
    public UserSettings User { get; init; } = new();
    public AudioDeviceSettings Devices { get; init; } = new();
    public CommunicationDatabaseSettings Database { get; init; } = new();
    public ImmutableArray<CompanySettings> Companies { get; init; } = [];

    public CompanySettings? FindCompanySettings(CompanyKey company)
    {
        ArgumentNullException.ThrowIfNull(company);
        return Companies.IsDefaultOrEmpty ? null : Companies.FirstOrDefault(settings => settings?.Company == company);
    }

    public override string ToString() => nameof(ApplicationOptions);
}

/// <summary>Declarative account settings only. Loading them never registers an account or opens a network connection.</summary>
public sealed class SipAccountOptions
{
    public const string PasswordPurpose = "sip-account-password";
    public string? Server { get; init; }
    public int Port { get; init; } = 5060;
    public SipTransport Transport { get; init; } = SipTransport.Udp;
    public string? Username { get; init; }
    public string? AuthenticationUsername { get; init; }
    public ProtectedSecret? Password { get; init; }
}

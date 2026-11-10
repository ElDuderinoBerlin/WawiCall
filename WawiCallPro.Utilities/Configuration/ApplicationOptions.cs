using WawiCallPro.Utilities.Security;
using System.Collections.Immutable;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.PhoneNumbers;

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
    public CallerLineAssignment? CallerLineAssignment { get; init; }

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
    public bool RegisterOnStartup { get; init; }
    public SipConnectionOptions Connection { get; init; } = new();
}

/// <summary>Explicit network and media settings; defaults match the supplied FRITZ!Box examples.</summary>
public sealed record SipConnectionOptions
{
    public string BindAddress { get; init; } = "0.0.0.0";
    public int LocalPort { get; init; } = 5060;
    public int RegistrationExpirySeconds { get; init; } = 120;
    public int RegistrationTimeoutSeconds { get; init; } = 10;
    public int RetryIntervalSeconds { get; init; } = 30;
    public int RingTimeoutSeconds { get; init; } = 60;
    public bool AcceptRtpFromAny { get; init; }
    public bool EnableG722 { get; init; } = true;
}

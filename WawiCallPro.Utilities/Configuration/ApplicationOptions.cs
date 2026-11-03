using WawiCallPro.Utilities.Security;

namespace WawiCallPro.Utilities.Configuration;

public enum SipTransport
{
    Udp = 1,
    Tcp = 2,
    Tls = 3
}

/// <summary>Initial typed connection configuration; scoped user/company/device settings follow in the next plan item.</summary>
public sealed class ApplicationOptions
{
    public SipAccountOptions? SipAccount { get; init; }
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

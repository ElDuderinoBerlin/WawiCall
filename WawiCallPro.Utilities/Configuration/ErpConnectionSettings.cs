using WawiCallPro.Utilities.Security;

namespace WawiCallPro.Utilities.Configuration;

/// <summary>Workstation ERP connection, independent of the active company and SIP account.</summary>
public sealed record ErpConnectionSettings
{
    public ProErpSettings? Pro { get; init; }
    public string? FinOfficeInstallationId { get; init; }
    public override string ToString() => nameof(ErpConnectionSettings);
}

public sealed record ProErpSettings
{
    public const string PasswordPurpose = "lexware-pro-password";
    public const string ClientKeyPurpose = "lexware-pro-client-key";
    public Uri? ApiUrl { get; init; }
    public string? InstallationId { get; init; }
    public string? Username { get; init; }
    public ProtectedSecret? Password { get; init; }
    public ProtectedSecret? ClientKey { get; init; }
    public int TimeoutSeconds { get; init; } = 30;
    public int TokenTimeoutSeconds { get; init; } = 10;
    public override string ToString() => nameof(ProErpSettings);
}

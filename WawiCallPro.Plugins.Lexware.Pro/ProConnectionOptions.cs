using WawiCallPro.Plugin.Windows;

namespace WawiCallPro.Plugins.Lexware.Pro;

/// <summary>Plugin-private connection inputs. Persistence and WawiCallPro client provisioning follow separately.</summary>
public sealed class ProConnectionOptions
{
    public Uri? ApiUrl { get; init; }
    public string? InstallationId { get; init; }
    public string? Username { get; init; }
    public string? Password { get; init; }
    public string? ClientKey { get; init; }
    public int TimeoutSeconds { get; init; } = 30;
    public int TokenTimeoutSeconds { get; init; } = 10;
    public override string ToString() => nameof(ProConnectionOptions);
}

public enum ProFailure
{
    ConfigurationRequired,
    InstallationUnavailable,
    PropertyStoreInvalid,
    AuthenticationFailed,
    InvalidToken,
    ApiUnavailable,
    InvalidResponse,
    ForeignCompany,
    ClientKeyMissing, ClientKeyUnknown, ClientKeyUnauthorized, PermissionDenied, ClientConfigurationExpired,
    ClientConfigurationMissing, ClientConfigurationInvalid, Timeout
}

/// <summary>Contains a fixed code only; source responses, tokens and credentials never become exception text.</summary>
public sealed class ProAdapterException(ProFailure failure) : Exception($"Lexware Pro: {failure}."), IPluginCompanyFailure
{
    public ProFailure Failure { get; } = failure;
    public PluginCompanyFailure CompanyFailure => Failure switch
    {
        ProFailure.PropertyStoreInvalid => PluginCompanyFailure.CompanyNames,
        ProFailure.InvalidToken => PluginCompanyFailure.AuthenticationFailed,
        ProFailure.ForeignCompany => PluginCompanyFailure.SourceAccess,
        _ => Enum.TryParse<PluginCompanyFailure>(Failure.ToString(), out var result) && Enum.IsDefined(result)
            ? result : PluginCompanyFailure.Unknown
    };
}

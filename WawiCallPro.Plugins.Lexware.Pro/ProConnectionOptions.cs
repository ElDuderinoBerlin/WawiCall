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
    ForeignCompany
}

/// <summary>Contains a fixed code only; source responses, tokens and credentials never become exception text.</summary>
public sealed class ProAdapterException(ProFailure failure) : Exception($"Lexware Pro: {failure}.")
{
    public ProFailure Failure { get; } = failure;
}

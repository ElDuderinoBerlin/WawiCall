namespace WawiCallPro.Plugins.Lexware.FinOffice;

/// <summary>Optional persistent identity for an installation moved to another path or server alias.</summary>
public sealed class FinOfficeConnectionOptions
{
    public string? InstallationId { get; init; }
    public override string ToString() => nameof(FinOfficeConnectionOptions);
}

public enum FinOfficeFailure
{
    ConfigurationRequired,
    InstallationUnavailable,
    InvalidCatalog,
    ForeignCompany,
    CompanyUnavailable,
    ReadFailed
}

/// <summary>Exposes a fixed failure code without source paths, database contents or native error text.</summary>
public sealed class FinOfficeAdapterException(FinOfficeFailure failure) : Exception($"Lexware Financial Office: {failure}.")
{
    public FinOfficeFailure Failure { get; } = failure;
}

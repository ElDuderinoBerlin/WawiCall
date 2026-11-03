namespace WawiCallPro.Interfaces.Companies;

public enum LexwareProductLine
{
    Pro = 1,
    FinOffice = 2
}

/// <summary>
/// Identifies an external company across product lines and installations.
/// IDs are opaque, case-sensitive source values; leading zeros are significant.
/// </summary>
public sealed record CompanyKey
{
    public CompanyKey(LexwareProductLine productLine, string installationId, string externalId)
    {
        if (!Enum.IsDefined(productLine))
            throw new ArgumentOutOfRangeException(nameof(productLine));
        ArgumentException.ThrowIfNullOrWhiteSpace(installationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);

        ProductLine = productLine;
        InstallationId = installationId;
        ExternalId = externalId;
    }

    public LexwareProductLine ProductLine { get; }
    public string InstallationId { get; }
    public string ExternalId { get; }
}

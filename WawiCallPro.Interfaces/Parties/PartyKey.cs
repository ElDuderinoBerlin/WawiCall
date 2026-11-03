using WawiCallPro.Interfaces.Companies;

namespace WawiCallPro.Interfaces.Parties;

public enum PartyKind
{
    Customer = 1,
    Supplier = 2,
    CustomerContact = 3,
    SupplierContact = 4
}

/// <summary>
/// Identifies a source record, not a display/customer number. Contact IDs are scoped to their owner.
/// Customer and supplier records are kept distinct even when they describe the same person.
/// </summary>
public sealed record PartyKey
{
    public PartyKey(CompanyKey company, PartyKind kind, string externalId, string? ownerExternalId = null)
    {
        ArgumentNullException.ThrowIfNull(company);
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind));
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);

        if (kind is PartyKind.CustomerContact or PartyKind.SupplierContact)
            ArgumentException.ThrowIfNullOrWhiteSpace(ownerExternalId);
        else if (ownerExternalId is not null)
            throw new ArgumentException("Only contact records have an owner.", nameof(ownerExternalId));

        Company = company;
        Kind = kind;
        ExternalId = externalId;
        OwnerExternalId = ownerExternalId;
    }

    public CompanyKey Company { get; }
    public PartyKind Kind { get; }
    public string ExternalId { get; }
    public string? OwnerExternalId { get; }
}

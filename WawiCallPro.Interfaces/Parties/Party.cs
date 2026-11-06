using System.Collections.Immutable;

namespace WawiCallPro.Interfaces.Parties;

public enum AddressKind
{
    Billing = 1,
    Shipping = 2,
    Additional = 3,
    Contact = 4
}

public enum PhoneKind
{
    Telephone = 1,
    Mobile = 2,
    Fax = 3
}

/// <summary>
/// Locates a field within a party's source data, e.g. an additional address or contact record.
/// Adapters preserve their source record type, ID and field name rather than exposing native objects.
/// </summary>
public sealed record SourceField(string RecordType, string RecordId, string FieldName);

/// <summary>Identifies the native source entity without exposing a product-specific model.</summary>
public sealed record SourceRecord(string RecordType, string RecordId);

/// <summary>A source phone value; normalization in phase 6 must preserve the original and any extension.</summary>
public sealed record PartyPhone(
    string OriginalValue,
    PhoneKind Kind,
    SourceField Source,
    string? E164 = null,
    string? Extension = null);

/// <summary>An address belonging to the enclosing party; its source ID is not globally unique.</summary>
public sealed record PartyAddress
{
    public required string ExternalId { get; init; }
    public required AddressKind Kind { get; init; }
    public string? CompanyName { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? Street { get; init; }
    public string? HouseNumber { get; init; }
    public string? AddressLine2 { get; init; }
    public string? PostalCode { get; init; }
    public string? City { get; init; }
    public string? CountryCode { get; init; }
    public string? Email { get; init; }
    public ImmutableArray<PartyPhone> Phones { get; init; } = [];
}

/// <summary>
/// Detached master-data snapshot. Contacts are separate parties linked by their key's owner ID.
/// A source timestamp may be absent; retrieval time must not be presented as a source modification time.
/// </summary>
public sealed record Party
{
    public required PartyKey Key { get; init; }
    public required string DisplayName { get; init; }
    public string? Number { get; init; }
    public SourceRecord? Source { get; init; }
    public string? Email { get; init; }
    public bool IsInactive { get; init; }
    public DateTimeOffset? SourceUpdatedAtUtc { get; init; }
    public ImmutableArray<PartyAddress> Addresses { get; init; } = [];
    public ImmutableArray<PartyPhone> Phones { get; init; } = [];
}

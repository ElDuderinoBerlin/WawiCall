using WawiCallPro.Interfaces.Parties;

namespace WawiCallPro.Interfaces.PhoneNumbers;

public enum PhoneNumberParseStatus
{
    Empty = 1,
    Unparseable = 2,
    Invalid = 3,
    Possible = 4,
    Valid = 5,
    UnsupportedLength = 6
}

/// <summary>Number-plan classification, independent of any provider's types or a party's declared PhoneKind.</summary>
public enum PhoneNumberType
{
    Unknown = 0,
    FixedLine = 1,
    Mobile = 2,
    FixedLineOrMobile = 3,
    TollFree = 4,
    PremiumRate = 5,
    SharedCost = 6,
    Voip = 7,
    PersonalNumber = 8,
    Pager = 9,
    UniversalAccess = 10,
    Voicemail = 11
}

/// <summary>Detached parsing evidence; does not assign a company, contact, stem or inferred extension.</summary>
public sealed record PhoneNumberParseResult
{
    public required string OriginalValue { get; init; }
    public SourceField? Source { get; init; }
    public required string ParserVersion { get; init; }
    public required PhoneNumberParseStatus Status { get; init; }

    /// <summary>Fixed machine-readable diagnostic code, null for a valid supported number.</summary>
    public string? Code { get; init; }

    /// <summary>Present only for a valid number fitting E.164's maximum of 15 digits; never truncated.</summary>
    public string? E164 { get; init; }
    public string? Extension { get; init; }
    public int? CountryCallingCode { get; init; }

    /// <summary>ISO region, or 001 for a non-geographic calling code; not the caller's present country.</summary>
    public string? RegionCode { get; init; }

    /// <summary>National significant number, including any significant leading zeros.</summary>
    public string? NationalNumber { get; init; }
    /// <summary>National destination prefix when the metadata supplies one; excludes the country/trunk prefix.</summary>
    public string? NationalDestinationCode { get; init; }
    /// <summary>Digits after the destination prefix, including a public DDI; not an inferred extension.</summary>
    public string? SubscriberNumber { get; init; }
    public PhoneNumberType NumberType { get; init; }

    /// <summary>Optional offline number-range description in German, not a live location.</summary>
    public string? RegionDescription { get; init; }

    // Records otherwise include the original phone value in their generated diagnostic representation.
    public override string ToString() => $"{nameof(PhoneNumberParseResult)}({Status})";
}

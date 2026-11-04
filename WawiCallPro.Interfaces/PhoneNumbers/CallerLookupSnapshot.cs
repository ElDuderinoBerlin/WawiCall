using System.Collections.Immutable;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Parties;

namespace WawiCallPro.Interfaces.PhoneNumbers;

public enum PhoneNumberMatchKind { Exact = 1, StemPrefix = 2, ConfirmedBlock = 3, InferredBlock = 4 }

/// <summary>Evidence observed at lookup time, independent of subsequent cache or rule updates.</summary>
public sealed record PhoneNumberMatchEvidence
{
    public required PhoneNumberMatchKind Kind { get; init; }
    public required string ParserVersion { get; init; }
    public long CacheRevision { get; init; }
    public long RuleRevision { get; init; }
    public Guid? RuleId { get; init; }
    public int? CountryCallingCode { get; init; }
    public string? DestinationCode { get; init; }
    public string? Stem { get; init; }
    public int? StartWidth { get; init; }
    public string? ApparatusNumber { get; init; }
    public string? MatchedE164 { get; init; }
    public SourceField? Source { get; init; }
}

public sealed record CallerSearchCompanySnapshot(CompanyKey Company, long CacheRevision, long RuleRevision,
    bool IsComplete, DateTimeOffset? RefreshedAtUtc, string? CacheParserVersion);

/// <summary>The complete configured search scope, including unavailable companies, and the decision reason.</summary>
public sealed record CallerLookupSnapshot
{
    public required ImmutableArray<CallerSearchCompanySnapshot> SearchCompanies { get; init; }
    public required string ParserVersion { get; init; }
    public required DateTimeOffset SearchedAtUtc { get; init; }
    public bool IsComplete { get; init; }
    public string? SelectionReason { get; init; }
}

public sealed record PhoneMatchingSettings(CompanyKey Company, bool StemMatchingEnabled, int StartWidth);
public enum PhoneNumberBlockKind { Confirmed = 1, Inferred = 2 }

/// <summary>A public number-block rule belongs to one source organisation; it never identifies a contact by itself.</summary>
public sealed record PhoneNumberBlockRule
{
    public required Guid Id { get; init; }
    public required PartyKey Organisation { get; init; }
    public required PhoneNumberBlockKind Kind { get; init; }
    public bool IsEnabled { get; init; } = true;
    public required int CountryCallingCode { get; init; }
    public required string DestinationCode { get; init; }
    public required string Stem { get; init; }
    public int MinimumSuffixDigits { get; init; }
    public required int MaximumSuffixDigits { get; init; }
    public required string Evidence { get; init; }
}

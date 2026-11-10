using System.Collections.Immutable;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Parties;

namespace WawiCallPro.Interfaces.PhoneNumbers;

/// <summary>Company-wide settings, separate from the user's search-company selection. Null version means insert only.</summary>
public sealed record PhoneMatchingProfile(PhoneMatchingSettings Settings, long? Version, long RuleRevision);

/// <summary>Detached input for an immutable company index; the caller supplies one coherent cache/rule snapshot.</summary>
public sealed record CompanyPhoneMatchSource(PhoneMatchingProfile Profile, long CacheRevision,
    string? CacheParserVersion, bool IsComplete, ImmutableArray<Party> Parties, ImmutableArray<PhoneNumberBlockRule> Blocks);

/// <summary>The source contact remains evidence even when a prefix identifies only its owning organisation.</summary>
public sealed record PhoneMatchObservation(PartyKey SourceParty, string? RegisteredExtension, PhoneNumberMatchEvidence Match);

public sealed record PhoneMatchCandidate(PartyKey Party, string DisplayName, ImmutableArray<PhoneMatchObservation> Evidence)
{
    public bool IsPositive => Evidence.Any(item => item.Match.Kind != PhoneNumberMatchKind.InferredBlock);
    public override string ToString() => nameof(PhoneMatchCandidate);
}

/// <summary>Offline lookup. No party is selected automatically; callers must combine all authorised search companies first.</summary>
public interface ICompanyPhoneMatchIndex
{
    PhoneMatchingProfile Profile { get; }
    long CacheRevision { get; }
    string Version { get; }
    bool IsComplete { get; }
    ImmutableArray<PhoneMatchCandidate> Find(PhoneNumberInterpretation incoming, CancellationToken cancellationToken = default);
}

/// <summary>Builds exact and structured prefix indexes without database, network or UI access.</summary>
public interface ICompanyPhoneMatchIndexFactory
{
    ICompanyPhoneMatchIndex Create(CompanyPhoneMatchSource source, CancellationToken cancellationToken = default);
}

/// <summary>Explicit access to registered local company caches. This alone does not establish current ERP authorisation.</summary>
public interface ICompanyPhoneMatchingService
{
    Task<PhoneMatchingProfile> GetProfileAsync(CompanyKey company, CancellationToken cancellationToken = default);
    Task<PhoneMatchingProfile> SaveProfileAsync(PhoneMatchingSettings settings, long? expectedVersion, CancellationToken cancellationToken = default);
    Task<ICompanyPhoneMatchIndex> BuildIndexAsync(CompanyKey company, CancellationToken cancellationToken = default);
}

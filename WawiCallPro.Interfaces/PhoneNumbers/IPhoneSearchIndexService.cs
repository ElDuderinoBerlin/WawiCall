using System.Collections.Immutable;
using WawiCallPro.Interfaces.Companies;

namespace WawiCallPro.Interfaces.PhoneNumbers;

public enum PhoneSearchScopeState
{
    NotReady, Refreshing, NoSelection, Ready, Partial, CatalogUnavailable, ConfigurationUnavailable,
    RestartRequired, ScopeChanged, ScopeExpired, Cancelled, Stopped
}
public enum PhoneSearchCompanyState
{
    Ready, NotAuthorized, CacheMissing, CacheStale, NormalizationOutdated, ContextChanged, Incomplete, Unavailable
}

/// <summary>Observed cache metadata. An unavailable/not-authorised company remains in the configured search scope.</summary>
public sealed record PhoneSearchCompanyStatus(CompanyKey Company, string DisplayName, PhoneSearchCompanyState State,
    long CacheRevision = 0, long RuleRevision = 0, DateTimeOffset? RefreshedAtUtc = null, string? ParserVersion = null);

public sealed record PhoneSearchIndexStatus(long Generation, PhoneSearchScopeState State, DateTimeOffset? CatalogCheckedAtUtc,
    ImmutableArray<PhoneSearchCompanyStatus> Companies, bool IsComplete);

/// <summary>Detached evidence for one lookup; later publication or revocation never rewrites this result.</summary>
public sealed record PhoneSearchResult(PhoneSearchIndexStatus Index, CallerLookupSnapshot Lookup, ImmutableArray<PhoneMatchCandidate> Candidates)
{
    public override string ToString() => nameof(PhoneSearchResult);
}

/// <summary>Refresh performs explicit background I/O. Find/GetStatus only read the published memory snapshot and never wait for refresh.</summary>
public interface IPhoneSearchIndexService
{
    Task<PhoneSearchIndexStatus> RefreshAsync(CancellationToken cancellationToken = default);
    PhoneSearchIndexStatus GetStatus();
    PhoneSearchResult Find(PhoneNumberInterpretation incoming, CancellationToken cancellationToken = default);
}

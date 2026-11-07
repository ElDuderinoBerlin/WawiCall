using System.Collections.Immutable;
using WawiCallPro.Interfaces.Companies;

namespace WawiCallPro.Interfaces.PhoneNumbers;

public enum PhoneCacheRefreshState { Updated, NotAuthorized, Failed, SelectionChanged }
public enum PhoneCacheRefreshFailure { CatalogUnavailable, InvalidSource, SourceUnavailable, StorageUnavailable, Conflict, RestartRequired, DatabaseNotReady, Unsupported }
public enum PhoneCacheRefreshPhase { Reading, Normalizing, Saving }

public sealed record PhoneCacheProgress(string CompanyName, int CompanyNumber, int CompanyCount,
    PhoneCacheRefreshPhase Phase, int PartyCount);

/// <summary>Each company is replaced independently. A failed company keeps its previously committed cache.</summary>
public sealed record PhoneCacheCompanyResult(CompanyKey Company, string DisplayName, PhoneCacheRefreshState State,
    int PartyCount = 0, int PhoneCount = 0, int UnresolvedPhoneCount = 0, long? Revision = null,
    PhoneCacheRefreshFailure? Failure = null);

public sealed record PhoneCacheRefreshResult(ImmutableArray<PhoneCacheCompanyResult> Companies);

/// <summary>Explicit, cancellable refresh of the saved cache sources, independent of an active view company.</summary>
public interface IPhoneCacheService
{
    Task<PhoneCacheRefreshResult> RefreshAsync(IProgress<PhoneCacheProgress>? progress = null, CancellationToken cancellationToken = default);
}

/// <summary>Fixed failures only; source credentials, provider responses and raw phone values are never exception text.</summary>
public sealed class PhoneCacheRefreshException(PhoneCacheRefreshFailure failure) : Exception(failure switch
{
    PhoneCacheRefreshFailure.RestartRequired => "Die Datenbankeinstellungen wurden geändert. Bitte vor der Cacheaktualisierung neu starten.",
    PhoneCacheRefreshFailure.DatabaseNotReady => "Bitte zuerst die Kommunikationsdatenbank einrichten oder aktualisieren.",
    PhoneCacheRefreshFailure.CatalogUnavailable => "Die berechtigten Mandanten konnten nicht gelesen werden. Bitte die Warenwirtschaftsverbindung prüfen.",
    _ => "Die Warenwirtschaft hat keinen vollständigen, gültigen Datenbestand geliefert. Der bisherige Cache bleibt erhalten."
})
{
    public PhoneCacheRefreshFailure Failure { get; } = failure;
}

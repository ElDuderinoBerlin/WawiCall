using System.Collections.Immutable;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Parties;

namespace WawiCallPro.Interfaces.Directory;

/// <summary>An explicit company and optional role; no dependency on the host's current company selection.</summary>
public sealed record PartySearchQuery
{
    public PartySearchQuery(CompanyKey company, string text, PartyKind? kind = null)
    {
        ArgumentNullException.ThrowIfNull(company);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (text.Length > 200 || text.Any(char.IsControl))
            throw new ArgumentException("Der Suchtext darf höchstens 200 Zeichen und keine Steuerzeichen enthalten.", nameof(text));
        if (kind is { } role && !Enum.IsDefined(role)) throw new ArgumentOutOfRangeException(nameof(kind));
        Company = company;
        Text = text.Trim();
        Kind = kind;
    }

    public CompanyKey Company { get; }
    public string Text { get; }
    public PartyKind? Kind { get; }
    public override string ToString() => nameof(PartySearchQuery);
}

/// <summary>Complete detached search result for exactly this query. Failures never masquerade as empty/partial results.</summary>
public sealed record PartySearchResult(PartySearchQuery Query, ImmutableArray<Party> Items)
{
    public override string ToString() => nameof(PartySearchResult);
}

/// <summary>Searches names, business numbers and email addresses through the active source adapter.</summary>
public interface IPartySearch
{
    Task<PartySearchResult> SearchAsync(PartySearchQuery query, CancellationToken cancellationToken = default);
}

public enum PartySearchFailure { InvalidResponse, TooManyResults }

/// <summary>Fixed display-safe failure without source identities, search terms or connection details.</summary>
public sealed class PartySearchException(PartySearchFailure failure) : Exception(failure == PartySearchFailure.TooManyResults
    ? "Zu viele Suchtreffer. Bitte den Suchtext oder die Rolle weiter eingrenzen."
    : "Die Suchquelle hat einen ungültigen oder veränderten Datenbestand geliefert. Bitte erneut suchen.")
{
    public PartySearchFailure Failure { get; } = failure;
}

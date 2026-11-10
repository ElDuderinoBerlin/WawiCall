using System.Collections.Immutable;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Interfaces.PhoneNumbers;

namespace WawiCallPro.Core.PhoneNumbers;

/// <summary>Conservative decisions across the complete configured search scope. Rank never proves uniqueness.</summary>
public sealed class CallerMatchDecisionService : ICallerMatchDecisionService
{
    public CallerMatchDecision Evaluate(PhoneSearchResult result, CallerMatchContext context, CallerMatchSelection? previous = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(context);
        var scope = result.Index.Companies.ToDictionary(company => company.Company);
        // A candidate must belong to the observed, available scope. Fail closed for malformed adapter results.
        if (result.Candidates.Any(candidate => !scope.TryGetValue(candidate.Party.Company, out var company)
            || company.State is PhoneSearchCompanyState.NotAuthorized or PhoneSearchCompanyState.Unavailable or PhoneSearchCompanyState.CacheMissing
            || candidate.Evidence.IsDefaultOrEmpty || candidate.Evidence.Any(item => item.SourceParty.Company != candidate.Party.Company || !Enum.IsDefined(item.Match.Kind)))
            || result.Candidates.Select(candidate => candidate.Party).Distinct().Count() != result.Candidates.Length)
            throw new ArgumentException("Candidates must belong to the observed company scope.", nameof(result));
        var groups = result.Index.Companies
            .OrderBy(company => company.Company == context.LineAssignment?.Company && context.LineAssignment.IsVerified ? 0
                : company.Company == context.ActiveCompany ? 1 : 2)
            .ThenBy(company => company.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(company => company.Company.InstallationId, StringComparer.Ordinal)
            .ThenBy(company => company.Company.ExternalId, StringComparer.Ordinal)
            .Select(company => new CallerMatchGroup(company, result.Candidates.Where(candidate => candidate.Party.Company == company.Company)
                .OrderBy(candidate => candidate.Evidence.Min(item => Rank(item.Match.Kind)))
                .ThenBy(candidate => candidate.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(candidate => candidate.Party.Kind).ThenBy(candidate => candidate.Party.ExternalId, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.Party.OwnerExternalId, StringComparer.Ordinal).ToImmutableArray())).ToImmutableArray();
        CallerMatchDecision Decision(CallerMatchDecisionKind kind, CallerMatchSelection? choice = null)
        {
            var lookup = result.Lookup with { SelectionReason = choice?.Kind switch
            { CallerMatchSelectionKind.Unique => "unique", CallerMatchSelectionKind.ConfiguredLine => "configured-line", _ => null } };
            return new(kind, groups, context.LineAssignment, choice is null ? null : choice with { Lookup = lookup }, lookup)
            { IndexGeneration = result.Index.Generation };
        }
        if (result.Index.State == PhoneSearchScopeState.NoSelection) return Decision(CallerMatchDecisionKind.NoSearchCompanies);
        if (previous is { Kind: CallerMatchSelectionKind.Manual })
            return result.Candidates.Any(candidate => candidate.Party == previous.Party)
                ? Choose(Decision(CallerMatchDecisionKind.Manual), previous.Party)
                : Decision(CallerMatchDecisionKind.SelectionUnavailable);
        var positives = result.Candidates.Where(candidate => candidate.IsPositive).ToArray();
        var line = context.LineAssignment is { IsVerified: true } mapping ? mapping : null;
        var onLine = positives.Where(candidate => candidate.Party.Company == line?.Company).ToArray();
        if (line is not null && onLine.Length == 0 && positives.Length > 0) return Decision(CallerMatchDecisionKind.LineConflict);
        var complete = result.Index.State == PhoneSearchScopeState.Ready && result.Index.IsComplete && result.Lookup.IsComplete
            && groups.Length > 0 && groups.All(group => group.Company.State == PhoneSearchCompanyState.Ready)
            && result.Lookup.SearchCompanies.Length == groups.Length
            && groups.All(group => result.Lookup.SearchCompanies.Any(company => company.Company == group.Company.Company && company.IsComplete
                && company.CacheRevision == group.Company.CacheRevision && company.RuleRevision == group.Company.RuleRevision))
            && groups.All(group => group.Candidates.All(candidate => candidate.Evidence.All(item => item.Match.ParserVersion == result.Lookup.ParserVersion
                && item.Match.CacheRevision == group.Company.CacheRevision && item.Match.RuleRevision == group.Company.RuleRevision)));
        if (!complete) return Decision(CallerMatchDecisionKind.Incomplete);
        if (line is not null && onLine.Length == 1)
            return Decision(CallerMatchDecisionKind.LineUnique, new(onLine[0].Party, CallerMatchSelectionKind.ConfiguredLine, false, result.Lookup));
        if (line is null && positives.Length == 1)
            return Decision(CallerMatchDecisionKind.Unique, new(positives[0].Party, CallerMatchSelectionKind.Unique, false, result.Lookup));
        return Decision(result.Candidates.IsEmpty ? CallerMatchDecisionKind.NoMatch : CallerMatchDecisionKind.Ambiguous);
    }

    public CallerMatchDecision Choose(CallerMatchDecision decision, PartyKey party)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(party);
        var group = decision.Groups.SingleOrDefault(group => group.Company.Company == party.Company);
        var candidate = group?.Candidates.SingleOrDefault(candidate => candidate.Party == party)
            ?? throw new ArgumentException("Choose a candidate from this lookup.", nameof(party));
        var provisional = !decision.Lookup.IsComplete || !candidate.IsPositive || group!.Company.State != PhoneSearchCompanyState.Ready;
        var lookup = decision.Lookup with { SelectionReason = "manual" };
        return decision with { Kind = CallerMatchDecisionKind.Manual, Lookup = lookup,
            Selection = new(party, CallerMatchSelectionKind.Manual, provisional, lookup) };
    }
    private static int Rank(PhoneNumberMatchKind kind) => kind switch
    { PhoneNumberMatchKind.Exact => 0, PhoneNumberMatchKind.ConfirmedBlock => 1, PhoneNumberMatchKind.StemPrefix => 2, _ => 3 };
}

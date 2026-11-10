using System.Collections.Immutable;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Parties;

namespace WawiCallPro.Interfaces.PhoneNumbers;

/// <summary>Explicit mapping supplied by the configured line adapter, never inferred from a caller match.</summary>
public sealed record CallerLineAssignment(PhoneLineContext Line, CompanyKey Company, string DisplayName, bool IsVerified);
public sealed record CallerMatchContext(CompanyKey? ActiveCompany = null, CallerLineAssignment? LineAssignment = null);
public enum CallerMatchDecisionKind { NoMatch, Incomplete, Ambiguous, LineConflict, Unique, LineUnique, Manual, SelectionUnavailable, NoSearchCompanies }
public enum CallerMatchSelectionKind { Unique, ConfiguredLine, Manual }

/// <summary>Detached choice for one lookup; a subsequent caller must explicitly preserve or replace it.</summary>
public sealed record CallerMatchSelection(PartyKey Party, CallerMatchSelectionKind Kind, bool IsProvisional, CallerLookupSnapshot Lookup);
public sealed record CallerMatchGroup(PhoneSearchCompanyStatus Company, ImmutableArray<PhoneMatchCandidate> Candidates);
public sealed record CallerMatchDecision(CallerMatchDecisionKind Kind, ImmutableArray<CallerMatchGroup> Groups,
    CallerLineAssignment? LineAssignment, CallerMatchSelection? Selection, CallerLookupSnapshot Lookup)
{
    public long IndexGeneration { get; init; }
    public override string ToString() => nameof(CallerMatchDecision);
}

/// <summary>Pure grouping/decision policy; does not query data, change the active company, or open views.</summary>
public interface ICallerMatchDecisionService
{
    CallerMatchDecision Evaluate(PhoneSearchResult result, CallerMatchContext context, CallerMatchSelection? previous = null);
    CallerMatchDecision Choose(CallerMatchDecision decision, PartyKey party);
}

using System.Collections.Immutable;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Interfaces.PhoneNumbers;

namespace WawiCallPro.Utilities.Persistence;

public sealed class DatabaseCallAssociation(ICallRepository calls) : ICallAssociationService
{
    public async Task<CallHistory> SaveAsync(Guid callId, CallerMatchDecision decision, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(decision);
        for (var attempt = 0; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var stored = await calls.GetAsync(callId, cancellationToken).ConfigureAwait(false)
                ?? throw new RepositoryConflictException(RepositoryConflictKind.MissingRecord);
            var history = stored.History;
            if (history.Lookup?.SelectionReason == "manual" && decision.Selection?.Kind != CallerMatchSelectionKind.Manual) return history;
            var old = history.Parties.ToDictionary(party => party.Party);
            var scope = history.Lookup?.SearchCompanies.Select(company => company.Company).ToHashSet();
            var incoming = decision.Groups.SelectMany(group => group.Candidates.Select(candidate =>
                old.TryGetValue(candidate.Party, out var previous) ? previous with { IsSelected = candidate.Party == decision.Selection?.Party }
                : new CallPartySnapshot(candidate.Party, candidate.DisplayName, candidate.Number, candidate.Party == decision.Selection?.Party)
                { CompanyDisplayName = group.Company.DisplayName, Match = scope is null || scope.Contains(candidate.Party.Company) ? candidate.Evidence.FirstOrDefault()?.Match : null })).ToImmutableArray();
            if (decision.Selection is { } selected && !incoming.Any(party => party.Party == selected.Party))
                throw new ArgumentException("Selected party is absent from the decision.", nameof(decision));
            var lookup = (history.Lookup ?? decision.Lookup) with { SelectionReason = decision.Lookup.SelectionReason };
            try
            {
                var call = history.Call with { Company = decision.Selection?.Party.Company ?? history.Call.Company };
                return (await calls.WriteAsync(new(call, stored.Version, incoming, [], call.Origin!, lookup), cancellationToken).ConfigureAwait(false)).History;
            }
            catch (RepositoryConflictException exception) when (exception.Kind == RepositoryConflictKind.VersionMismatch && attempt < 7) { }
        }
    }
}

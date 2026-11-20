using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Parties;

namespace WawiCallPro.Automation;

/// <summary>Resolves an exact business number in one company and role; source IDs and contacts cannot substitute for it.</summary>
public sealed class NumberedPartyResolver(IPartySearch source)
{
    public async Task<PartyKey> ResolveAsync(CompanyKey company, PartyKind kind, string number, CancellationToken token)
    {
        if (kind is not (PartyKind.Customer or PartyKind.Supplier)) throw new AutomationFault(AutomationFailure.InvalidArgument);
        var query = new PartySearchQuery(company, AutomationInput.Text(number, 200), kind);
        var result = await source.SearchAsync(query, token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        if (result is null || result.Query != query || result.Items.IsDefault || result.Items.Length > 25000
            || result.Items.Any(party => party?.Key is null || party.Key.Company != company || party.Key.Kind != kind)
            || result.Items.Select(party => party.Key).Distinct().Count() != result.Items.Length)
            throw new AutomationFault(AutomationFailure.ProviderUnavailable);
        var matches = result.Items.Where(party => string.Equals(party.Number?.Trim(), query.Text, StringComparison.Ordinal)).Take(2).ToArray();
        return matches.Length switch
        {
            0 => throw new AutomationFault(AutomationFailure.NotFound),
            1 => matches[0].Key,
            _ => throw new AutomationFault(AutomationFailure.Ambiguous)
        };
    }
}

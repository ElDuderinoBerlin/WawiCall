using System.Collections.Immutable;
using WawiCallPro.Interfaces.Common;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Parties;

namespace WawiCallPro.Core.Directory;

/// <summary>Reads source pages into one bounded result. The caller owns the source lifetime for the entire operation.</summary>
public sealed class PartySearchReader
{
    public const int MaximumResults = 25000;
    private const int BatchSize = 500;
    private readonly int _maximumResults;

    public PartySearchReader(int maximumResults = MaximumResults)
    {
        if (maximumResults is < 1 or > MaximumResults) throw new ArgumentOutOfRangeException(nameof(maximumResults));
        _maximumResults = maximumResults;
    }

    public async Task<PartySearchResult> ReadAsync(IBusinessDirectory source, PartySearchQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(query);
        var rows = ImmutableArray.CreateBuilder<Party>();
        var identities = new HashSet<PartyKey>();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pageRequest = new PageRequest(rows.Count, Math.Min(BatchSize, _maximumResults - rows.Count));
            var response = await source.GetPartiesAsync(new(query.Company, pageRequest, query.Text, query.Kind), cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (response is null || response.Company != query.Company || response.Page is null
                || response.Page.Items.IsDefault || response.Page.Items.Length > pageRequest.Limit
                || response.Page.HasMore && response.Page.Items.IsEmpty)
                throw new PartySearchException(PartySearchFailure.InvalidResponse);
            foreach (var party in response.Page.Items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (party?.Key is null || party.Key.Company != query.Company || string.IsNullOrWhiteSpace(party.DisplayName)
                    || query.Kind is { } kind && party.Key.Kind != kind || !identities.Add(party.Key))
                    throw new PartySearchException(PartySearchFailure.InvalidResponse);
                rows.Add(party);
            }
            if (!response.Page.HasMore) break;
            if (rows.Count >= _maximumResults) throw new PartySearchException(PartySearchFailure.TooManyResults);
        }
        cancellationToken.ThrowIfCancellationRequested();
        var sorted = rows.OrderBy(party => party.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(party => party.Number, StringComparer.OrdinalIgnoreCase).ThenBy(party => party.Key.Kind)
            .ThenBy(party => party.Key.ExternalId, StringComparer.Ordinal)
            .ThenBy(party => party.Key.OwnerExternalId, StringComparer.Ordinal).ToImmutableArray();
        cancellationToken.ThrowIfCancellationRequested();
        return new(query, sorted);
    }
}

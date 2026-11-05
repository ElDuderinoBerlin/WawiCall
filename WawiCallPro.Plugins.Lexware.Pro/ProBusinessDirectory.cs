using System.Collections.Immutable;
using System.Globalization;
using WawiCallPro.Interfaces.Capabilities;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Parties;

namespace WawiCallPro.Plugins.Lexware.Pro;

public sealed class ProBusinessDirectory(IProApiSession session, IProPropertyStore propertyStore) : IBusinessDirectory
{
    public async Task<ImmutableArray<Company>> GetCompaniesAsync(CancellationToken cancellationToken = default)
    {
        var installation = await session.GetInstallationAsync(cancellationToken).ConfigureAwait(false);
        var authorized = await session.GetCompaniesAsync(cancellationToken).ConfigureAwait(false);
        var names = await propertyStore.ReadNamesAsync(installation.PropertyStorePath, cancellationToken).ConfigureAwait(false);
        var companies = ImmutableArray.CreateBuilder<Company>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var company in authorized)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (company is null || company.Id <= 0) throw new ProAdapterException(ProFailure.InvalidResponse);
            var id = company.Id.ToString(CultureInfo.InvariantCulture);
            var name = string.IsNullOrWhiteSpace(company.Name) ? names.GetValueOrDefault(id) : company.Name;
            if (string.IsNullOrWhiteSpace(name) || !ids.Add(id)) throw new ProAdapterException(ProFailure.InvalidResponse);
            companies.Add(new Company(new CompanyKey(LexwareProductLine.Pro, installation.Id, id), name));
        }
        return companies.OrderBy(company => company.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(company => company.Key.ExternalId, StringComparer.Ordinal).ToImmutableArray();
    }

    public Task<CapabilitySet> GetCapabilitiesAsync(CompanyKey company, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new NotSupportedException("Der nachgewiesene Fähigkeitsbericht folgt in Phase 5.");
    }

    public Task<PartyPage> GetPartiesAsync(PartyQuery query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new NotSupportedException("Die Geschäftspartnerabbildung folgt im gesonderten Phase-5-Punkt.");
    }

    public Task<Party?> GetPartyAsync(PartyKey key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new NotSupportedException("Die Geschäftspartnerabbildung folgt im gesonderten Phase-5-Punkt.");
    }
}

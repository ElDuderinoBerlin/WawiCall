using System.Collections.Immutable;
using WawiCallPro.Interfaces.Capabilities;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Parties;

namespace WawiCallPro.Plugins.Lexware.FinOffice;

public sealed class FinOfficeBusinessDirectory(IFinOfficeInstallationSource source, FinOfficePartyDirectory parties, IFinOfficeCompanySession sessions) : IBusinessDirectory
{
    public async Task<ImmutableArray<Company>> GetCompaniesAsync(CancellationToken cancellationToken = default)
        => (await source.DetectAsync(cancellationToken).ConfigureAwait(false)).Companies;

    public async Task<CapabilitySet> GetCapabilitiesAsync(CompanyKey company, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(company);
        return await sessions.ReadAsync(company, (_, token) =>
        {
            token.ThrowIfCancellationRequested();
            return Task.FromResult(FinOfficeCapabilities.Create(company));
        }, cancellationToken).ConfigureAwait(false);
    }

    public Task<PartyPage> GetPartiesAsync(PartyQuery query, CancellationToken cancellationToken = default)
        => parties.GetPartiesAsync(query, cancellationToken);

    public Task<Party?> GetPartyAsync(PartyKey key, CancellationToken cancellationToken = default)
        => parties.GetPartyAsync(key, cancellationToken);
}

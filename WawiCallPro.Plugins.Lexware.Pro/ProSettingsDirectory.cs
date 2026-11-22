using System.Collections.Immutable;
using WawiCallPro.Interfaces.Capabilities;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Interfaces.Plugins;

namespace WawiCallPro.Plugins.Lexware.Pro;

/// <summary>Explicit read-only company queries against an unsaved input snapshot, with a short-lived owned session.</summary>
internal sealed class ProSettingsDirectory(
    IPluginConnectionPreview preview,
    Func<ProConnectionEditorChange> read,
    IPluginConnectionDirectoryFactory sessions) : IBusinessDirectory
{
    public async Task<ImmutableArray<Company>> GetCompaniesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var settings = preview.ReadProPreview(read()) ?? throw new ProAdapterException(ProFailure.ConfigurationRequired);
        await using var session = sessions.Create(settings);
        return await session.Directory.GetCompaniesAsync(cancellationToken).ConfigureAwait(false);
    }
    public Task<CapabilitySet> GetCapabilitiesAsync(CompanyKey company, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<PartyPage> GetPartiesAsync(PartyQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<Party?> GetPartyAsync(PartyKey key, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

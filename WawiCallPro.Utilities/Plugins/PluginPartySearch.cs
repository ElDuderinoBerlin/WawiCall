using WawiCallPro.Core.Directory;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Plugins;

namespace WawiCallPro.Utilities.Plugins;

/// <summary>The complete search owns one runtime operation; a plugin switch cancels and awaits all source pages.</summary>
public sealed class PluginPartySearch(IProductPluginRuntime runtime, PartySearchReader reader) : IPartySearch
{
    public Task<PartySearchResult> SearchAsync(PartySearchQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return runtime.UseAsync((plugin, token) => reader.ReadAsync(plugin.Directory, query, token), cancellationToken);
    }
}

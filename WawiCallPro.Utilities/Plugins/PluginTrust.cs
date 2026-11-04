using System.Collections.Immutable;
using WawiCallPro.Interfaces.Plugins;

namespace WawiCallPro.Utilities.Plugins;

public interface IPluginTrust
{
    IReadOnlySet<string> GetAllowedTokens();
}
/// <summary>The compiled, signed host contract is the trust anchor, never a DLL from the plugin directory.</summary>
public sealed class PluginTrust : IPluginTrust
{
    public IReadOnlySet<string> GetAllowedTokens()
    {
        var token = typeof(IWawiCallPlugin).Assembly.GetName().GetPublicKeyToken();
        if (token is not { Length: 8 }) throw new PluginRuntimeException();
        return ImmutableHashSet.Create(StringComparer.Ordinal, Convert.ToHexString(token).ToLowerInvariant());
    }
}

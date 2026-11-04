using Quantarix.Plugin.Hosting;

namespace WawiCallPro.Utilities.Plugins;

/// <summary>Creates caller-owned instances of the existing Quantarix runtime. Contracts must be loaded before Create.</summary>
public interface IPluginManagerFactory
{
    /// <summary>The explicit token set is snapshotted. An empty set permits unsigned development fixtures.</summary>
    PluginManager Create(IReadOnlySet<string> allowedPublicKeyTokens);
}

/// <summary>Composition boundary for Quantarix; does not discover plugins or activate their services at host startup.</summary>
public sealed class QuantarixPluginManagerFactory(Quantarix.Plugin.Abstractions.ILogger logger) : IPluginManagerFactory
{
    public PluginManager Create(IReadOnlySet<string> allowedPublicKeyTokens)
    {
        ArgumentNullException.ThrowIfNull(allowedPublicKeyTokens);
        return new PluginManager(logger, new HashSet<string>(allowedPublicKeyTokens, StringComparer.Ordinal));
    }
}

using Quantarix.Plugin.Abstractions;
using WawiCallPro.Interfaces.Parties;

[assembly: SharedAssembly]
namespace WawiCallPro.Plugin.Windows;

public sealed record PluginViewDescriptor(string Id, string DisplayName);

/// <summary>Optional Windows-only extension. The host invokes it on its UI thread; Console never requests views.</summary>
public interface IWawiCallPluginViews
{
    IReadOnlyList<PluginViewDescriptor> GetViews();
    /// <summary>The returned control is owned by this lease. Dispose it on the UI thread before unloading the plugin.</summary>
    IPluginViewLease CreateView(string id, PartyKey party);
}
public interface IPluginViewLease : IDisposable
{
    Control View { get; }
}

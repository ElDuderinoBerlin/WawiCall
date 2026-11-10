using Quantarix.Plugin.Abstractions;
using WawiCallPro.Interfaces.Parties;

[assembly: SharedAssembly]
namespace WawiCallPro.Plugin.Windows;

public sealed record PluginViewDescriptor(string Id, string DisplayName);

/// <summary>Optional Windows-only extension. The host invokes it on its UI thread; Console never requests views.</summary>
public interface IWawiCallPluginViews
{
    IReadOnlyList<PluginViewDescriptor> GetViews();
    /// <summary>Keep the supplied full party identity for the entire lease; never rebind to the host's global company. Dispose on the UI thread before unload.</summary>
    IPluginViewLease CreateView(string id, PartyKey party);
}
public interface IPluginViewLease : IDisposable
{
    Control View { get; }
}

/// <summary>Optional asynchronous detail loading. Invoked on the UI context; each request retains its explicit party.</summary>
public interface IAsyncWawiCallPluginViews
{
    Task<IPluginViewLease> CreateViewAsync(string id, PartyKey party, CancellationToken cancellationToken);
}
/// <summary>Optional edit guard, read on the UI thread before an explicit close or plugin switch.</summary>
public interface IPluginViewEditState { bool HasUnsavedChanges { get; } }
public sealed class UnsavedPluginChangesException() : Exception("Zuerst die ungespeicherten Änderungen in den Fachansichten speichern oder dort verwerfen.");

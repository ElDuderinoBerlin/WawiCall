namespace WawiCallPro.Interfaces.Plugins;

/// <summary>Callbacks return detached domain data and never retain plugin objects beyond the callback.</summary>
public interface IProductPluginRuntime
{
    bool IsLoaded { get; }
    Task<T> UseAsync<T>(Func<IWawiCallPlugin, CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default);
}
/// <summary>Optional stop, awaited before Quantarix Shutdown/unload.</summary>
public interface IWawiCallPluginLifetime
{
    ValueTask StopAsync(CancellationToken cancellationToken = default);
}
/// <summary>Plugin-owned resources registered by a runtime callback; closed before plugin shutdown.</summary>
public interface IPluginLifetimeResource : IAsyncDisposable { }

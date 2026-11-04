using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WawiCallPro.Interfaces.Plugins;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Capabilities;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Parties;
using System.Collections.Immutable;

namespace WawiCallPro.Utilities.Plugins;

public sealed record ProductPluginLocation(string Directory);

/// <summary>Cancels/awaits callbacks before shutting down and releasing the owned plugin session.</summary>
public sealed class ProductPluginRuntime(IPluginSessionFactory sessions, ProductPluginLocation location) : IProductPluginRuntime, IHostedService, IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1);
    private readonly CancellationTokenSource _stopping = new();
    private IPluginSession? _session;
    private volatile bool _loaded;
    private bool _disposed;
    private readonly List<IPluginLifetimeResource> _resources = [];
    public bool IsLoaded => _loaded;
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _session = sessions.CreateTrusted();
        try { await _session.LoadAsync(location.Directory).ConfigureAwait(false); _loaded = true; }
        catch { await _session.DisposeAsync().ConfigureAwait(false); _session = null; throw; }
    }
    public async Task<T> UseAsync<T>(Func<IWawiCallPlugin, CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (!_loaded) throw new PluginRuntimeException();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stopping.Token);
        await _gate.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            if (!_loaded) throw new PluginRuntimeException();
            var result = await operation(_session!.GetPlugins().Single(), linked.Token).ConfigureAwait(false);
            if (result is IPluginLifetimeResource resource) _resources.Add(resource);
            return result;
        }
        finally { _gate.Release(); }
    }
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _loaded = false;
        await _stopping.CancelAsync().ConfigureAwait(false);
        // Unload must await callbacks even when the host's shutdown deadline expires.
        await _gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            var failures = new List<Exception>();
            foreach (var resource in _resources.AsEnumerable().Reverse())
                try { await resource.DisposeAsync().ConfigureAwait(false); } catch (Exception exception) { failures.Add(exception); }
            if (failures.Count != 0) throw new PluginRuntimeException();
            _resources.Clear();
            if (_session is not null)
            {
                try { await _session.DisposeAsync().ConfigureAwait(false); } catch (Exception exception) { failures.Add(exception); }
                finally { _session = null; }
            }
            if (failures.Count != 0) throw new PluginRuntimeException();
        }
        finally { _gate.Release(); }
    }
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        _disposed = true; _stopping.Dispose(); _gate.Dispose();
    }
}
public static class ProductPluginServices
{
    public static void AddProductPluginRuntime(this IServiceCollection services)
    {
        services.AddSingleton(new ProductPluginLocation(Path.Combine(AppContext.BaseDirectory, "plugins")));
        services.AddSingleton<ProductPluginRuntime>();
        services.AddSingleton<IProductPluginRuntime>(provider => provider.GetRequiredService<ProductPluginRuntime>());
        services.AddHostedService(provider => provider.GetRequiredService<ProductPluginRuntime>());
        services.AddSingleton<IBusinessDirectory, PluginBusinessDirectory>();
    }
}
/// <summary>Source access remains plugin-owned and is protected by the runtime's lifetime gate.</summary>
public sealed class PluginBusinessDirectory(IProductPluginRuntime runtime) : IBusinessDirectory
{
    public Task<ImmutableArray<Company>> GetCompaniesAsync(CancellationToken cancellationToken = default) => runtime.UseAsync((plugin, token) => plugin.Directory.GetCompaniesAsync(token), cancellationToken);
    public Task<CapabilitySet> GetCapabilitiesAsync(CompanyKey company, CancellationToken cancellationToken = default) => runtime.UseAsync((plugin, token) => plugin.Directory.GetCapabilitiesAsync(company, token), cancellationToken);
    public Task<PartyPage> GetPartiesAsync(PartyQuery query, CancellationToken cancellationToken = default) => runtime.UseAsync((plugin, token) => plugin.Directory.GetPartiesAsync(query, token), cancellationToken);
    public Task<Party?> GetPartyAsync(PartyKey key, CancellationToken cancellationToken = default) => runtime.UseAsync((plugin, token) => plugin.Directory.GetPartyAsync(key, token), cancellationToken);
}

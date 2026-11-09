using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Reflection;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Interfaces.Plugins;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Capabilities;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Parties;
using System.Collections.Immutable;

namespace WawiCallPro.Utilities.Plugins;

public sealed record ProductPluginLocation(string Directory);

/// <summary>Cancels/awaits callbacks before shutting down and releasing the owned plugin session.</summary>
public sealed class ProductPluginRuntime(IPluginSessionFactory sessions, ProductPluginLocation location, ApplicationConfigurationService? configuration = null) : IProductPluginRuntime, IProductPluginSelection, IHostedService, IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1);
    private readonly CancellationTokenSource _stopping = new();
    private IPluginSession? _session;
    private readonly SemaphoreSlim _switchGate = new(1);
    private CancellationTokenSource _sessionStopping = new();
    private bool _releaseFailed;
    public string? ActivePluginId { get; private set; }

    public IReadOnlyList<ProductPluginChoice> GetAvailablePlugins()
    {
        if (!Directory.Exists(location.Directory)) return [];
        return Directory.EnumerateDirectories(location.Directory).Select(directory =>
        {
            var id = Path.GetFileName(directory);
            var file = Path.Combine(directory, id + ".dll");
            if (!File.Exists(file)) return null;
            var version = AssemblyName.GetAssemblyName(file).Version?.ToString() ?? "";
            var name = id.EndsWith(".Dummy", StringComparison.Ordinal) ? "Dummy-Plugin (Testbetrieb)"
                : id.EndsWith(".FinOffice", StringComparison.Ordinal) ? "Lexware Financial Office"
                : id.EndsWith(".Pro", StringComparison.Ordinal) ? "Lexware Pro" : id;
            return new ProductPluginChoice(id, name, version);
        }).OfType<ProductPluginChoice>().OrderBy(choice => choice.Id, StringComparer.Ordinal).ToArray();
    }

    public async Task SwitchAsync(string id, CancellationToken cancellationToken = default)
    {
        var choice = GetAvailablePlugins().SingleOrDefault(choice => choice.Id == id) ?? throw new PluginRuntimeException();
        await _switchGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_startCalled || _stopping.IsCancellationRequested || _releaseFailed) throw new PluginRuntimeException();
            if (_loaded && ActivePluginId == choice.Id) return;
            _loaded = false;
            await _sessionStopping.CancelAsync().ConfigureAwait(false);
            await _gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                await ReleaseSessionAsync().ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                _stopping.Token.ThrowIfCancellationRequested();
                _sessionStopping.Dispose(); _sessionStopping = new CancellationTokenSource();
                _session = sessions.CreateTrusted();
                try
                {
                    if (_session is not ISelectedPluginSession selected) throw new PluginRuntimeException();
                    await selected.LoadSelectedAsync(Path.Combine(location.Directory, choice.Id)).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    _stopping.Token.ThrowIfCancellationRequested();
                    ActivePluginId = choice.Id; _loaded = true;
                }
                catch { await ReleaseSessionAsync().ConfigureAwait(false); throw; }
            }
            finally { _gate.Release(); }
        }
        finally { _switchGate.Release(); }
    }

    private async Task ReleaseSessionAsync()
    {
        ActivePluginId = null;
        var failed = false;
        foreach (var resource in _resources.AsEnumerable().Reverse())
            try { await resource.DisposeAsync().ConfigureAwait(false); } catch { failed = true; }
        if (failed) { _releaseFailed = true; throw new PluginRuntimeException(); }
        _resources.Clear();
        try
        {
            if (_session is not null) { await _session.DisposeAsync().ConfigureAwait(false); _session = null; }
        }
        catch { _releaseFailed = true; throw new PluginRuntimeException(); }
    }

    private volatile bool _loaded;
    private bool _disposed;
    private bool _startCalled;
    private readonly List<IPluginLifetimeResource> _resources = [];
    public bool IsLoaded => _loaded;
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_startCalled || _stopping.IsCancellationRequested)
                throw new InvalidOperationException("Die Warenwirtschaftspluginlaufzeit wurde bereits gestartet oder beendet.");
            _startCalled = true;
            _session = sessions.CreateTrusted();
            try
            {
                var choices = GetAvailablePlugins();
                var requested = configuration?.Current.Options.User.ActivePluginId;
                if (_session is ISelectedPluginSession selected && (requested is not null || choices.Count > 1))
                {
                    var choice = requested is null ? choices.FirstOrDefault(choice => !choice.Id.EndsWith(".Dummy", StringComparison.Ordinal))
                        : choices.SingleOrDefault(choice => choice.Id == requested);
                    if (choice is null) throw new PluginRuntimeException();
                    await selected.LoadSelectedAsync(Path.Combine(location.Directory, choice.Id)).ConfigureAwait(false);
                    ActivePluginId = choice.Id;
                }
                else
                {
                    await _session.LoadAsync(location.Directory).ConfigureAwait(false);
                    ActivePluginId = choices.SingleOrDefault()?.Id;
                }
                _stopping.Token.ThrowIfCancellationRequested();
                _loaded = true;
            }
            catch { await _session.DisposeAsync().ConfigureAwait(false); _session = null; throw; }
        }
        finally { _gate.Release(); }
    }
    public async Task<T> UseAsync<T>(Func<IWawiCallPlugin, CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (!_loaded) throw new PluginRuntimeException();
        var sessionLifetime = _sessionStopping;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stopping.Token, sessionLifetime.Token);
        await _gate.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            if (!_loaded || !ReferenceEquals(sessionLifetime, _sessionStopping)) throw new PluginRuntimeException();
            linked.Token.ThrowIfCancellationRequested();
            var result = await operation(_session!.GetPlugins().Single(), linked.Token).ConfigureAwait(false);
            if (result is IPluginLifetimeResource resource) _resources.Add(resource);
            // A switch invalidates results immediately, even before linked cancellation callbacks run.
            if (!_loaded || !ReferenceEquals(sessionLifetime, _sessionStopping))
                throw new OperationCanceledException(sessionLifetime.Token);
            // Track returned resources before rejecting cancelled results; StopAsync still owns their cleanup.
            linked.Token.ThrowIfCancellationRequested();
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
            _loaded = false;
            await ReleaseSessionAsync().ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        _disposed = true; _stopping.Dispose(); _sessionStopping.Dispose(); _gate.Dispose(); _switchGate.Dispose();
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
        services.AddSingleton<WawiCallPro.Interfaces.PhoneNumbers.IPhoneCacheService, WawiCallPro.Utilities.Persistence.PhoneCacheService>();
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

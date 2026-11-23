using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quantarix.Plugin.Abstractions;
using Quantarix.Plugin.Hosting;
using WawiCallPro.Interfaces.Plugins;
using WawiCallPro.Utilities.Operations;

namespace WawiCallPro.Utilities.Plugins;

public sealed record FunctionPluginLocation(string Directory);
public sealed record FunctionPluginChoice(string Id, string Name, string Version, bool Enabled);

public interface IFunctionPluginRuntime
{
    Task<IReadOnlyList<FunctionPluginChoice>> ListAsync(CancellationToken token = default);
    Task SetEnabledAsync(string id, bool enabled, CancellationToken token = default);
    Task ExecuteAsync(string id, CancellationToken token = default);
}

/// <summary>Owns an independent Quantarix session for each enabled function.</summary>
public sealed class FunctionPluginRuntime(
    FunctionPluginLocation location,
    ApplicationPaths paths,
    IPluginManagerFactory managers,
    IPluginTrust trust,
    IServiceScopeFactory scopes,
    Microsoft.Extensions.Logging.ILogger<FunctionPluginRuntime> logger) : IFunctionPluginRuntime, IHostedService, IAsyncDisposable
{
    private readonly string _stateFile = Path.Combine(paths.RootDirectory, "function-plugins.json");
    private readonly Dictionary<string, Session> _active = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _gate = new(1);
    private readonly CancellationTokenSource _stopping = new();
    private HashSet<string> _enabled = new(StringComparer.Ordinal);
    private bool _disposed;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _enabled = await ReadStateAsync(cancellationToken).ConfigureAwait(false);
            foreach (var id in _enabled)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try { _active.Add(id, await LoadAsync(id, cancellationToken).ConfigureAwait(false)); }
                catch (FunctionPluginException) { logger.LogWarning("Funktionsplugin {PluginId} konnte beim Start nicht aktiviert werden.", id); }
            }
        }
        finally { _gate.Release(); }
    }

    public async Task<IReadOnlyList<FunctionPluginChoice>> ListAsync(CancellationToken token = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_stopping.Token, token);
        await _gate.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            return await Task.Run(() =>
            {
                if (!System.IO.Directory.Exists(location.Directory)) return [];
                return System.IO.Directory.EnumerateDirectories(location.Directory)
                    .Select(Path.GetFileName).OfType<string>().Select(ReadChoice).OfType<FunctionPluginChoice>()
                    .OrderBy(choice => choice.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
            }, linked.Token).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    public async Task SetEnabledAsync(string id, bool enabled, CancellationToken token = default)
    {
        if (!ValidId(id)) throw new FunctionPluginException();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_stopping.Token, token);
        await _gate.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            if (enabled && _active.ContainsKey(id) || !enabled && !_enabled.Contains(id)) return;
            Session? loaded = enabled ? await LoadAsync(id, linked.Token).ConfigureAwait(false) : null;
            try
            {
                var next = new HashSet<string>(_enabled, StringComparer.Ordinal);
                if (enabled) next.Add(id);
                else next.Remove(id);
                await WriteStateAsync(next, linked.Token).ConfigureAwait(false);
                _enabled = next;
                if (loaded is not null)
                {
                    _active.Add(id, loaded);
                    loaded = null;
                }
                else if (_active.Remove(id, out var previous)) await ReleaseAsync(id, previous).ConfigureAwait(false);
            }
            finally { if (loaded is not null) await ReleaseAsync(id, loaded).ConfigureAwait(false); }
        }
        finally { _gate.Release(); }
    }

    public async Task ExecuteAsync(string id, CancellationToken token = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_stopping.Token, token);
        await _gate.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            if (!_active.TryGetValue(id, out var session)) throw new FunctionPluginException();
            await Task.Run(() => session.Plugin.ExecuteAsync(linked.Token), linked.Token).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _stopping.CancelAsync().ConfigureAwait(false);
        await _gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            foreach (var (id, session) in _active.Reverse()) await ReleaseAsync(id, session).ConfigureAwait(false);
            _active.Clear();
        }
        finally { _gate.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        _stopping.Dispose();
        _gate.Dispose();
    }

    private FunctionPluginChoice? ReadChoice(string id)
    {
        if (!ValidId(id)) return null;
        var file = Path.Combine(location.Directory, id, id + ".dll");
        if (!File.Exists(file)) return null;
        try
        {
            var assembly = AssemblyName.GetAssemblyName(file);
            var token = assembly.GetPublicKeyToken();
            if (assembly.Name != id || token is not { Length: 8 } ||
                !trust.GetAllowedTokens().Contains(Convert.ToHexString(token).ToLowerInvariant())) return null;
            var name = FileVersionInfo.GetVersionInfo(file).FileDescription;
            return new(id, string.IsNullOrWhiteSpace(name) ? id : name, assembly.Version?.ToString() ?? "", _active.ContainsKey(id));
        }
        catch (Exception error) when (error is IOException or BadImageFormatException or UnauthorizedAccessException) { return null; }
    }

    private async Task<Session> LoadAsync(string id, CancellationToken token)
    {
        var scope = scopes.CreateAsyncScope();
        PluginManager? manager = null;
        var loaded = false;
        try
        {
            return await Task.Run(() =>
            {
                if (ReadChoice(id) is null) throw new FunctionPluginException();
                manager = managers.CreateWithActivation(trust.GetAllowedTokens(), type =>
                {
                    if (!typeof(IFunctionPlugin).IsAssignableFrom(type) || typeof(IWawiCallPlugin).IsAssignableFrom(type))
                        throw new FunctionPluginException();
                    return (IPlugin)ActivatorUtilities.CreateInstance(scope.ServiceProvider, type);
                });
                manager.LoadSelectedDirectory(Path.Combine(location.Directory, id));
                if (manager.LoadFailureCount != 0 || manager.Plugins.Count != 1 || manager.Plugins[0] is not IFunctionPlugin plugin)
                    throw new FunctionPluginException();
                token.ThrowIfCancellationRequested();
                loaded = true;
                return new Session(manager, scope, plugin);
            }, token).ConfigureAwait(false);
        }
        catch (Exception) when (!token.IsCancellationRequested) { throw new FunctionPluginException(); }
        finally
        {
            if (!loaded)
            {
                if (manager is not null) await manager.UnloadAllAsync(() => ReleaseScopeAsync(id, scope)).ConfigureAwait(false);
                else await ReleaseScopeAsync(id, scope).ConfigureAwait(false);
            }
        }
    }

    private async Task ReleaseAsync(string id, Session session)
    {
        try
        {
            await session.Manager.UnloadAllAsync(() => ReleaseScopeAsync(id, session.Scope)).ConfigureAwait(false);
            if (session.Manager.ShutdownFailureCount != 0) throw new FunctionPluginException();
        }
        catch (Exception) { logger.LogWarning("Funktionsplugin {PluginId} konnte nicht vollständig freigegeben werden.", id); }
    }

    private async ValueTask ReleaseScopeAsync(string id, AsyncServiceScope scope)
    {
        try { await scope.DisposeAsync().ConfigureAwait(false); }
        catch (Exception) { logger.LogWarning("Dienste für Funktionsplugin {PluginId} konnten nicht vollständig freigegeben werden.", id); }
    }

    private async Task<HashSet<string>> ReadStateAsync(CancellationToken token)
    {
        if (!File.Exists(_stateFile)) return new(StringComparer.Ordinal);
        try
        {
            if (new FileInfo(_stateFile).Length > 32768) throw new JsonException();
            var ids = JsonSerializer.Deserialize<string[]>(await File.ReadAllBytesAsync(_stateFile, token).ConfigureAwait(false));
            if (ids is null || ids.Any(id => !ValidId(id))) throw new JsonException();
            return new(ids, StringComparer.Ordinal);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            logger.LogWarning("Gespeicherte Aktivierungen der Funktionsplugins konnten nicht gelesen werden.");
            return new(StringComparer.Ordinal);
        }
    }

    private async Task WriteStateAsync(HashSet<string> ids, CancellationToken token)
    {
        var directory = Path.GetDirectoryName(_stateFile)!;
        System.IO.Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, ".function-plugins-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            await File.WriteAllBytesAsync(temporary, JsonSerializer.SerializeToUtf8Bytes(ids.Order(StringComparer.Ordinal)), token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            File.Move(temporary, _stateFile, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static bool ValidId(string? id) => id is { Length: > 0 and <= 160 }
        && id.StartsWith("WawiCallPro.Functions.", StringComparison.Ordinal)
        && id.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_');

    private sealed record Session(PluginManager Manager, AsyncServiceScope Scope, IFunctionPlugin Plugin);
}

public sealed class FunctionPluginException : Exception
{
    public FunctionPluginException() : base("Das Funktionsplugin konnte nicht geladen werden. Bitte Plugin-Dateien und Version prüfen.") { }
}

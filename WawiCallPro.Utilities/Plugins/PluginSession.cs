using Microsoft.Extensions.DependencyInjection;
using Quantarix.Plugin.Abstractions;
using Quantarix.Plugin.Hosting;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Interfaces.Plugins;
using WawiCallPro.Utilities.Hosting;

namespace WawiCallPro.Utilities.Plugins;

public sealed class WawiCallHostServices(ICompanyContext companies, IUiDispatcher dispatcher, ICallHistoryService history) : IWawiCallHostServices
{
    public ICompanyContext Companies => companies;
    public IUiDispatcher UiDispatcher => dispatcher;
    public ICallHistoryService History => history;
}
public interface IPluginSession : IDisposable, IAsyncDisposable
{
    void Load(string absoluteDirectory);
    Task LoadAsync(string absoluteDirectory);
    IReadOnlyList<IWawiCallPlugin> GetPlugins();
}
public interface IPluginSessionFactory
{
    IPluginSession Create(IReadOnlySet<string> tokens);
    IPluginSession CreateTrusted();
}
public sealed class PluginRuntimeException : Exception
{
    public PluginRuntimeException() : base("Das Warenwirtschaftsplugin konnte nicht geladen oder konfiguriert werden.") { }
}
public sealed class PluginSessionFactory(IServiceScopeFactory scopes, IPluginManagerFactory managers,
    IWawiCallHostServices services, ApplicationDescriptor application, IPluginTrust trust) : IPluginSessionFactory
{
    public IPluginSession CreateTrusted() => Create(trust.GetAllowedTokens());
    public IPluginSession Create(IReadOnlySet<string> tokens)
    {
        var scope = scopes.CreateScope();
        try
        {
            var activated = new List<IPlugin>();
            var manager = managers.CreateWithActivation(tokens, type =>
            {
                var plugin = (IPlugin)ActivatorUtilities.CreateInstance(scope.ServiceProvider, type);
                activated.Add(plugin); return plugin;
            });
            return new Session(scope, manager, services, application.ProductLine == "Pro" ? LexwareProductLine.Pro : LexwareProductLine.FinOffice, activated);
        }
        catch { scope.Dispose(); throw; }
    }
    private sealed class Session(IServiceScope scope, PluginManager manager, IWawiCallHostServices services, LexwareProductLine product, List<IPlugin> activated) : IPluginSession
    {
        private bool _loaded;
        private bool _disposed;
        public void Load(string absoluteDirectory)
        {
            try { LoadCore(absoluteDirectory); }
            catch { Dispose(); throw new PluginRuntimeException(); }
        }
        public async Task LoadAsync(string absoluteDirectory)
        {
            try { LoadCore(absoluteDirectory); }
            catch { await DisposeAsync().ConfigureAwait(false); throw new PluginRuntimeException(); }
        }
        private void LoadCore(string absoluteDirectory)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_loaded) throw new InvalidOperationException("Plugin session is already loaded.");
            if (!Path.IsPathFullyQualified(absoluteDirectory) || !Directory.Exists(absoluteDirectory)) throw new PluginRuntimeException();
            try
            {
                manager.LoadFromDirectory(absoluteDirectory);
                if (manager.LoadFailureCount != 0 || manager.Plugins.Count != 1 || manager.Plugins[0] is not IWawiCallPlugin plugin || plugin.ProductLine != product) throw new PluginRuntimeException();
                plugin.Configure(services);
                _loaded = true;
            }
            catch { throw new PluginRuntimeException(); }
        }
        public IReadOnlyList<IWawiCallPlugin> GetPlugins()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return manager.Plugins.Cast<IWawiCallPlugin>().ToArray();
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { manager.Dispose(); }
            finally
            {
                try { foreach (var plugin in activated.OfType<IDisposable>()) plugin.Dispose(); }
                finally { activated.Clear(); scope.Dispose(); }
            }
        }
        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            foreach (var plugin in activated.OfType<IWawiCallPluginLifetime>()) await plugin.StopAsync(CancellationToken.None).ConfigureAwait(false);
            await manager.UnloadAllAsync(async () =>
            {
                try
                {
                    foreach (var plugin in activated)
                        if (plugin is IAsyncDisposable asynchronousPlugin) await asynchronousPlugin.DisposeAsync().ConfigureAwait(false);
                        else if (plugin is IDisposable disposablePlugin) disposablePlugin.Dispose();
                }
                finally
                {
                    activated.Clear();
                    if (scope is IAsyncDisposable asynchronous) await asynchronous.DisposeAsync().ConfigureAwait(false);
                    else scope.Dispose();
                }
            }).ConfigureAwait(false);
            _disposed = true;
            if (manager.ShutdownFailureCount != 0) throw new PluginRuntimeException();
        }
    }
}

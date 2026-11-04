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
public interface IPluginSession : IDisposable
{
    void Load(string absoluteDirectory);
    IReadOnlyList<IWawiCallPlugin> GetPlugins();
}
public interface IPluginSessionFactory
{
    IPluginSession Create(IReadOnlySet<string> tokens);
}
public sealed class PluginRuntimeException : Exception
{
    public PluginRuntimeException() : base("Das Warenwirtschaftsplugin konnte nicht geladen oder konfiguriert werden.") { }
}
public sealed class PluginSessionFactory(IServiceScopeFactory scopes, IPluginManagerFactory managers,
    IWawiCallHostServices services, ApplicationDescriptor application) : IPluginSessionFactory
{
    public IPluginSession Create(IReadOnlySet<string> tokens)
    {
        var scope = scopes.CreateScope();
        try
        {
            var manager = managers.CreateWithActivation(tokens, type => (IPlugin)ActivatorUtilities.CreateInstance(scope.ServiceProvider, type));
            return new Session(scope, manager, services, application.ProductLine == "Pro" ? LexwareProductLine.Pro : LexwareProductLine.FinOffice);
        }
        catch { scope.Dispose(); throw; }
    }
    private sealed class Session(IServiceScope scope, PluginManager manager, IWawiCallHostServices services, LexwareProductLine product) : IPluginSession
    {
        private bool _loaded;
        private bool _disposed;
        public void Load(string absoluteDirectory)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_loaded) throw new InvalidOperationException("Plugin session is already loaded.");
            if (!Path.IsPathFullyQualified(absoluteDirectory) || !Directory.Exists(absoluteDirectory)) throw new PluginRuntimeException();
            try
            {
                manager.LoadFromDirectory(absoluteDirectory);
                if (manager.Plugins.Count != 1 || manager.Plugins[0] is not IWawiCallPlugin plugin || plugin.ProductLine != product) throw new PluginRuntimeException();
                plugin.Configure(services);
                _loaded = true;
            }
            catch { Dispose(); throw new PluginRuntimeException(); }
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
            try { manager.Dispose(); } finally { scope.Dispose(); }
        }
    }
}

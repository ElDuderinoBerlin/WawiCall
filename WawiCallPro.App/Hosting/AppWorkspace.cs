using System.Collections.Immutable;
using Microsoft.Extensions.DependencyInjection;
using WawiCallPro.Core.Companies;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Interfaces.Plugins;
using WawiCallPro.Interfaces.PhoneNumbers;
using WawiCallPro.Plugin.Windows;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Hosting;
using WawiCallPro.Utilities.Operations;
using WawiCallPro.Utilities.Persistence;
using WawiCallPro.Utilities.Security;

namespace WawiCallPro.App.Hosting;

/// <summary>Common application services; ERP models and source access stay inside the active plugin.</summary>
public sealed class AppWorkspace(ApplicationDescriptor application, ApplicationConfigurationService configuration,
    ConfigurationStore settings, ISecretProtector secrets, ICommunicationDatabase database,
    IBusinessDirectory directory, IProductPluginRuntime plugins, CompanyContext companies,
    ICallHistoryService history, ApplicationPaths paths, IPluginViewAccess views, IUiDispatcher dispatcher, IPhoneCacheService phoneCache)
{
    public ApplicationDescriptor Application { get; } = application;
    public ApplicationConfigurationService Configuration { get; } = configuration;
    public ConfigurationStore Settings { get; } = settings;
    public ISecretProtector Secrets { get; } = secrets;
    public ICommunicationDatabase Database { get; } = database;
    public IBusinessDirectory Directory { get; } = directory;
    public IProductPluginRuntime Plugins { get; } = plugins;
    public CompanyContext Companies { get; } = companies;
    public ICallHistoryService History { get; } = history;
    public ApplicationPaths Paths { get; } = paths;
    public IPluginViewAccess Views { get; } = views;
    public IUiDispatcher Dispatcher { get; } = dispatcher;
    public IPhoneCacheService PhoneCache { get; } = phoneCache;

    public Task<PluginSummary> GetPluginAsync(CancellationToken cancellationToken) =>
        Plugins.UseAsync(async (plugin, token) =>
        {
            var descriptors = ImmutableArray<PluginViewDescriptor>.Empty;
            if (plugin is IWawiCallPluginViews windows)
                await Dispatcher.InvokeAsync(() => descriptors = windows.GetViews().ToImmutableArray(), token);
            return new PluginSummary(plugin.Name, plugin.Version.ToString(), plugin.ProductLine.ToString(), descriptors);
        }, cancellationToken);
}

public sealed record PluginSummary(string Name, string Version, string ProductLine, ImmutableArray<PluginViewDescriptor> Views);

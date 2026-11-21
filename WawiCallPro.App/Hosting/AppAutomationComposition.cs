using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WawiCallPro.Automation;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Utilities.Diagnostics;

namespace WawiCallPro.App.Hosting;

internal static class AppAutomationComposition
{
    internal static void AddAppAutomation(this IServiceCollection services, StartupExecutionMode mode, AppComIdentity? identity)
    {
        services.AddSingleton(identity ?? new AppComIdentity(Guid.Empty, false));
        services.AddSingleton<AppAutomationState>();
        services.AddSingleton<IAutomationEnvironment>(provider => provider.GetRequiredService<AppAutomationState>());
        services.AddSingleton<IAutomationViewHost, AppAutomationPresentation>();
        services.AddSingleton<IAutomationPresentation, DispatchedAutomationPresentation>();
        services.AddSingleton<IAutomationTelephony, PendingAutomationAdapters>();
        services.AddWawiAutomation();
        services.AddSingleton<AppAutomationFactory>();
        services.AddSingleton<AppAutomationCatalog>();
        services.AddHostedService(provider => provider.GetRequiredService<AppAutomationCatalog>());
        if (!mode.ComActivated) return;

        services.Insert(0, ServiceDescriptor.Singleton<IHostedService, AppComInstallationCheck>());
        var hosted = services.Where(item => item.ServiceType == typeof(IHostedService)).ToArray();
        var foundation = Array.FindIndex(hosted, item => item.ImplementationType == typeof(LicenseStartupGate)) + 1;
        if (foundation == 0) throw new InvalidOperationException("The license gate must precede deferred COM services.");
        var keys = new object[hosted.Length];
        for (var index = 0; index < hosted.Length; index++)
        {
            var descriptor = hosted[index]; var key = keys[index] = new object();
            services.Remove(descriptor);
            if (descriptor.ImplementationType is { } type)
                services.Add(ServiceDescriptor.KeyedSingleton(typeof(IHostedService), key, type));
            else if (descriptor.ImplementationInstance is { } instance)
                services.Add(ServiceDescriptor.KeyedSingleton(typeof(IHostedService), key, instance));
            else
                services.Add(ServiceDescriptor.KeyedSingleton(typeof(IHostedService), key, (provider, _) => descriptor.ImplementationFactory!(provider)));
        }
        services.AddSingleton(provider => new AppStartupPipeline(keys.Select(key => (Func<IHostedService>)(() =>
            {
                var service = provider.GetRequiredKeyedService<IHostedService>(key);
                if (service is LicenseStartupGate license) license.AutomationState = provider.GetRequiredService<AppAutomationState>();
                return service;
            })).ToArray(),
            foundation, provider.GetRequiredService<AppAutomationState>())
        {
            RevalidateBeforeUserMode = token => ((LicenseStartupGate)provider.GetRequiredKeyedService<IHostedService>(keys[foundation - 1])).StartAsync(token),
            MetadataServices = [() => provider.GetRequiredService<WawiCallPro.Utilities.Plugins.ProductPluginRuntime>(),
                () => provider.GetRequiredService<AppAutomationCatalog>()]
        });
        services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<AppStartupPipeline>());
    }
}

internal sealed class AppComInstallationCheck(WawiCallPro.Utilities.Plugins.ILexwareInstallation installation) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        installation.Check();
        return Task.CompletedTask;
    }
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

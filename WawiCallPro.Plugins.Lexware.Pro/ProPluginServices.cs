using Microsoft.Extensions.DependencyInjection;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Plugins;

namespace WawiCallPro.Plugins.Lexware.Pro;

public sealed class ProPluginServices : IPluginServiceModule
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton(provider =>
        {
            var settings = provider.GetService<IPluginConnectionConfiguration>()?.ReadPro();
            return settings is null ? new ProConnectionOptions() : new ProConnectionOptions
            {
                ApiUrl = settings.ApiUrl, InstallationId = settings.InstallationId,
                Username = settings.Username, Password = settings.Password, ClientKey = settings.ClientKey,
                TimeoutSeconds = settings.TimeoutSeconds, TokenTimeoutSeconds = settings.TokenTimeoutSeconds
            };
        });
        services.AddSingleton<IProRegistry, ProRegistry>();
        services.AddSingleton<IProInstallationSource, ProInstallationSource>();
        services.AddSingleton<IProPropertyStore, ProPropertyStore>();
        services.AddSingleton<IProHttpHandlerFactory, ProHttpHandlerFactory>();
        services.AddSingleton<IProApiConnectionFactory, ProApiConnectionFactory>();
        services.AddSingleton<IProApiSession, ProApiSession>();
        services.AddSingleton<ProPartyDirectory>();
        services.AddSingleton<IBusinessDirectory, ProBusinessDirectory>();
    }
}

using Microsoft.Extensions.DependencyInjection;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Plugins;

namespace WawiCallPro.Plugins.Lexware.Pro;

public sealed class ProPluginServices : IPluginServiceModule
{
    public void ConfigureServices(IServiceCollection services)
    {
        // No demo credentials or implicit client identity. The later configuration point replaces these inputs.
        services.AddSingleton(new ProConnectionOptions());
        services.AddSingleton<IProRegistry, ProRegistry>();
        services.AddSingleton<IProInstallationSource, ProInstallationSource>();
        services.AddSingleton<IProPropertyStore, ProPropertyStore>();
        services.AddSingleton<IProHttpHandlerFactory, ProHttpHandlerFactory>();
        services.AddSingleton<IProApiConnectionFactory, ProApiConnectionFactory>();
        services.AddSingleton<IProApiSession, ProApiSession>();
        services.AddSingleton<IBusinessDirectory, ProBusinessDirectory>();
    }
}

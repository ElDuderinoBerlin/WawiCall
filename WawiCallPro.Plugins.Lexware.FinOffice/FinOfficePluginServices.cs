using Microsoft.Extensions.DependencyInjection;
using Quantarix.Pcfk.DataAccess.Infrastructure;
using Quantarix.Pcfk.DataAccess.Manager;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Plugins;

namespace WawiCallPro.Plugins.Lexware.FinOffice;

public sealed class FinOfficePluginServices : IPluginServiceModule
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton(provider => new FinOfficeConnectionOptions
        {
            InstallationId = provider.GetService<IPluginConnectionConfiguration>()?.ReadFinOfficeInstallationId()
        });
        services.AddSingleton<IPcfkInstallationManager, PcfkInstallationManager>();
        services.AddSingleton<IFinOfficeInstallationSource, FinOfficeInstallationSource>();
        services.AddSingleton<IFinOfficeCompanySession, FinOfficeCompanySession>();
        services.AddSingleton<FinOfficePartyDirectory>();
        services.AddSingleton<IBusinessDirectory, FinOfficeBusinessDirectory>();
    }
}

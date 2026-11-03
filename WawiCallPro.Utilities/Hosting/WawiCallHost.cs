using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WawiCallPro.Core.Companies;
using WawiCallPro.Interfaces.Hosting;

namespace WawiCallPro.Utilities.Hosting;

/// <summary>Shared registrations; each executable supplies its dispatcher and presentation services.</summary>
public static class WawiCallHost
{
    public static IHostBuilder CreateBuilder(Assembly entryAssembly)
    {
        ArgumentNullException.ThrowIfNull(entryAssembly);
        // Explicit configuration/logging providers follow in the settings/operations plan items.
        return new HostBuilder()
            .UseContentRoot(AppContext.BaseDirectory)
            .UseDefaultServiceProvider(options =>
            {
                options.ValidateScopes = true;
                options.ValidateOnBuild = true;
            })
            .ConfigureServices(services =>
            {
                services.Configure<HostOptions>(options =>
                {
                    options.StartupTimeout = TimeSpan.FromSeconds(30);
                    options.ShutdownTimeout = TimeSpan.FromSeconds(30);
                });
                services.AddSingleton(ApplicationDescriptor.FromAssembly(entryAssembly));
                services.AddSingleton<CompanyContext>();
                services.AddSingleton<ICompanyContext>(provider => provider.GetRequiredService<CompanyContext>());
                services.AddHostedService<CompanyContextLifetime>();
                services.AddSingleton<HostDiagnostics>();
            });
    }
}

using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WawiCallPro.Core.Companies;
using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Security;
using WawiCallPro.Utilities.Operations;
using Microsoft.Extensions.Logging;

namespace WawiCallPro.Utilities.Hosting;

/// <summary>Shared registrations; each executable supplies its dispatcher and presentation services.</summary>
public static class WawiCallHost
{
    public static IHostBuilder CreateBuilder(Assembly entryAssembly)
    {
        ArgumentNullException.ThrowIfNull(entryAssembly);
        // Only the safe provider is registered; arbitrary formatted state never becomes a log record.
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
                services.AddSingleton(provider => ConfigurationLocation.ForCurrentUser(provider.GetRequiredService<ApplicationDescriptor>().ProductLine));
                services.AddSingleton<ApplicationPaths>();
                services.AddSingleton<RuntimeFailureState>();
                services.AddSingleton<ILoggerProvider, SafeFileLoggerProvider>();
                services.AddSingleton<Quantarix.Plugin.Abstractions.ILogger, QuantarixLogAdapter>();
                services.AddHostedService<RuntimeFailureMonitor>();
                services.AddSingleton<ISecretProtector, WindowsUserSecretProtector>();
                services.AddSingleton<ApplicationOptionsValidator>();
                services.AddSingleton<ConfigurationStore>();
                services.AddSingleton<ApplicationConfigurationService>();
                services.AddHostedService(provider => provider.GetRequiredService<ApplicationConfigurationService>());
                services.AddSingleton<CompanyContext>();
                services.AddSingleton<ICompanyContext>(provider => provider.GetRequiredService<CompanyContext>());
                services.AddHostedService<CompanyContextLifetime>();
                services.AddSingleton<HostDiagnostics>();
            });
    }
}

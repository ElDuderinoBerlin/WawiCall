using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Quantarix.License.Client.Identity;
using Quantarix.License.Client.Keys;
using Quantarix.License.Client.Machine;
using Quantarix.License.Client.Protection;
using Quantarix.License.Client.Validation;
using WawiCallPro.Utilities.Diagnostics;
using WawiCallPro.Utilities.Licensing;
using WawiCallPro.Utilities.Operations;

namespace WawiCallPro.App.Hosting;

internal static class AppStartupServices
{
    internal static void AddAppStartupServices(this IServiceCollection services, StartupExecutionMode mode)
    {
        services.AddSingleton(mode);
        services.AddSingleton(LicenseProductIdentity.FromAssembly(Assembly.GetExecutingAssembly()));
        services.AddSingleton(provider => StartupSettings.Load(provider.GetRequiredService<ApplicationPaths>()));
        services.AddSingleton<IPublicKeyProvider, CompiledLicenseKeys>();
        services.AddSingleton<ILocalLicenseProtector, DpapiLocalLicenseProtector>();
        services.AddSingleton<IMachineIdProvider, WindowsMachineIdProvider>();
        services.AddSingleton<IApplicationIdentityProvider, AssemblyApplicationIdentityProvider>();
        services.AddSingleton<ILicenseClockStateStore, DpapiLicenseClockStateStore>();
        services.AddSingleton<LicenseFileValidator>();
        services.AddSingleton<LicenseGuard>();
        services.AddSingleton<IStartupHttpClientFactory, StartupHttpClientFactory>();
        services.AddSingleton<ILicenseServerSessionFactory, LicenseServerSessionFactory>();
        services.AddSingleton<IProductLicenseService, ProductLicenseService>();
        services.AddSingleton<IAppLicensePrompt, AppLicensePrompt>();
        services.AddHostedService<AppUiStartup>();
        services.AddHostedService<LicenseStartupGate>();
        services.AddHostedService<LicenseMonitor>();
        services.AddHostedService<CrashReportUploader>();
        services.AddSingleton<CrashReportQueue>();
    }
}

using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Diagnostics;
using WawiCallPro.Utilities.Hosting;
using WawiCallPro.Utilities.Operations;

namespace WawiCallPro.App.Hosting;

/// <summary>Small pre-host composition root; owns early diagnostics through final error presentation.</summary>
internal static class AppBootstrap
{
    internal static ServiceProvider Create()
    {
        var services = new ServiceCollection();
        var application = ApplicationDescriptor.FromAssembly(Assembly.GetExecutingAssembly());
        services.AddSingleton(application);
        services.AddSingleton(ConfigurationLocation.ForCurrentUser(application.ProductLine));
        services.AddSingleton<ApplicationPaths>();
        services.AddSingleton(TimeProvider.System);
        var assembly = Assembly.GetExecutingAssembly();
        services.AddSingleton(new CrashApplicationInfo(assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0",
            assembly.GetName().Version?.ToString() ?? "0.0"));
        services.AddSingleton<CrashReportQueue>();
        services.AddSingleton<ICrashReporter, CrashReporter>();
        services.AddSingleton<AppExceptionBoundary>();
        services.AddSingleton<IAppInstanceGuard, AppInstanceGuard>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }
}

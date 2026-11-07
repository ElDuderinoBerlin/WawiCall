using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WawiCallPro.Core.Threading;
using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Utilities.Hosting;
using WawiCallPro.Utilities.Plugins;
using WawiCallPro.Utilities.Diagnostics;

namespace WawiCallPro.App.Hosting;

internal static class AppCompositionRoot
{
    internal static IHost Build(WindowsFormsSynchronizationContext uiContext, AppExceptionBoundary exceptions,
        StartupExecutionMode mode, IAppInstanceGuard instance) =>
        WawiCallHost.CreateBuilder(Assembly.GetExecutingAssembly())
            .ConfigureServices(services =>
            {
                services.AddSingleton(exceptions);
                services.AddSingleton(instance);
                services.AddSingleton(new AppWindowOptions());
                services.AddSingleton<AppWorkspace>();
                services.AddSingleton<AppUiState>();
                services.AddAppStartupServices(mode);
                services.AddProductPluginRuntime();
                services.AddSingleton<WawiCallPro.Plugin.Windows.IPluginViewAccess, WawiCallPro.Plugin.Windows.PluginViewAccess>();
                services.AddSingleton(new WawiCallPro.Utilities.Plugins.SharedPluginContractAssembly(typeof(WawiCallPro.Plugin.Windows.IWawiCallPluginViews).Assembly));
                services.AddSingleton<IUiDispatcher>(_ => new SynchronizationContextUiDispatcher(uiContext));
                services.AddSingleton<IHostLifetime, AppHostLifetime>();
            })
            .Build();
}

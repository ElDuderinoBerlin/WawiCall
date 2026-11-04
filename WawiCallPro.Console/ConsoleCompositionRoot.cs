using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WawiCallPro.Core.Threading;
using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Utilities.Hosting;
using WawiCallPro.Utilities.Plugins;

namespace WawiCallPro.ConsoleApp;

internal static class ConsoleCompositionRoot
{
    internal static IHost Build(bool loadPlugins = true) => WawiCallHost.CreateBuilder(Assembly.GetExecutingAssembly())
        .ConfigureServices(services =>
        {
            if (loadPlugins) services.AddProductPluginRuntime();
            services.AddSingleton<IUiDispatcher, InlineUiDispatcher>();
            services.AddSingleton(new WawiCallPro.Utilities.Plugins.SharedPluginContractAssembly(typeof(WawiCallPro.Plugin.Windows.IWawiCallPluginViews).Assembly));
        })
        .Build();
}

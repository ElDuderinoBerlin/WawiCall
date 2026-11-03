using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WawiCallPro.Core.Threading;
using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Utilities.Hosting;

namespace WawiCallPro.App.Hosting;

internal static class AppCompositionRoot
{
    internal static IHost Build(WindowsFormsSynchronizationContext uiContext) =>
        WawiCallHost.CreateBuilder(Assembly.GetExecutingAssembly())
            .ConfigureServices(services =>
            {
                services.AddSingleton<IUiDispatcher>(_ => new SynchronizationContextUiDispatcher(uiContext));
                services.AddSingleton<IHostLifetime, AppHostLifetime>();
            })
            .Build();
}

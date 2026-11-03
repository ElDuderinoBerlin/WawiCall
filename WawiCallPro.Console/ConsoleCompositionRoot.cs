using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WawiCallPro.Core.Threading;
using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Utilities.Hosting;

namespace WawiCallPro.ConsoleApp;

internal static class ConsoleCompositionRoot
{
    internal static IHost Build() => WawiCallHost.CreateBuilder(Assembly.GetExecutingAssembly())
        .ConfigureServices(services => services.AddSingleton<IUiDispatcher, InlineUiDispatcher>())
        .Build();
}

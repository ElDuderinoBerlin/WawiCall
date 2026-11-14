using System.Reflection;
using WawiCallPro.Audio;
using WawiCallPro.Telephony.Sip;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WawiCallPro.Core.Threading;
using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Utilities.Hosting;
using WawiCallPro.Utilities.Plugins;
using WawiCallPro.Utilities.Diagnostics;
using WawiCallPro.Utilities.Persistence;

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
                services.AddSingleton<CallNotesSessionFactory>();
                services.AddSingleton<WawiCallPro.Interfaces.Office.IMailViewSource, UnconfiguredMailViewSource>();
                services.AddSingleton<IAutoStartRegistry, AutoStartRegistry>();
                services.AddSingleton(_ => AppStartupCommand.ForCurrentProcess());
                services.AddSingleton<IAppAutoStart, WindowsAppAutoStart>();
                services.AddSingleton<LogWindowService>();
                services.AddSingleton<ILogWindowService>(provider => provider.GetRequiredService<LogWindowService>());
                services.AddSingleton<ILoggerProvider, UiLogProvider>();
                services.AddSingleton<AppUiState>();
                services.AddSingleton<WawiCallPro.Plugin.Windows.IPartySearchPresentation, PartySearchPresentation>();
                services.AddSingleton<WawiCallPro.Interfaces.Plugins.IPartySearchWorkspaceFactory, WawiCallPro.Plugin.Windows.PartySearchWorkspaceFactory>();
                services.AddSingleton<WawiCallPro.Plugin.Windows.IPartySearchWorkspaceAccess, WawiCallPro.Plugin.Windows.PartySearchWorkspaceAccess>();
                services.AddSingleton<IAppHotkeyBackend, WindowsAppHotkeyBackend>();
                services.AddSingleton<IAppHotkeyFactory, AppHotkeyFactory>();
                services.AddAppStartupServices(mode);
                services.AddProductPluginRuntime();
                services.AddSingleton<ISipMediaSessionFactory, WindowsSipMediaSessionFactory>();
                services.AddSingleton<WawiCallPro.Interfaces.Audio.IAudioDeviceCatalog, WindowsAudioDeviceCatalog>();
                services.AddSipTelephony(!mode.SuppressNetwork);
                if (!mode.SuppressNetwork) services.AddPhoneSearchMaintenance();
                services.AddSingleton<WawiCallPro.Plugin.Windows.IPluginViewAccess, WawiCallPro.Plugin.Windows.PluginViewAccess>();
                services.AddSingleton(new WawiCallPro.Utilities.Plugins.SharedPluginContractAssembly(typeof(WawiCallPro.Plugin.Windows.IWawiCallPluginViews).Assembly));
                services.AddSingleton<IUiDispatcher>(_ => new SynchronizationContextUiDispatcher(uiContext));
                services.AddSingleton<IHostLifetime, AppHostLifetime>();
            })
            .Build();
}

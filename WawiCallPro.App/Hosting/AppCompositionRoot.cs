using System.IO;
using System.Reflection;
using WawiCallPro.Audio;
using WawiCallPro.Office;
using WawiCallPro.Telephony.Sip;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using WawiCallPro.Core.Threading;
using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Utilities.Hosting;
using WawiCallPro.Utilities.Plugins;
using WawiCallPro.Utilities.Diagnostics;
using WawiCallPro.Utilities.Persistence;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.App.Hosting;

internal static class AppCompositionRoot
{
    internal static IHost Build(WindowsFormsSynchronizationContext uiContext, AppExceptionBoundary exceptions,
        StartupExecutionMode mode, IAppInstanceGuard instance) => Build(uiContext, exceptions, mode, instance, null);

    internal static IHost Build(WindowsFormsSynchronizationContext uiContext, AppExceptionBoundary exceptions,
        StartupExecutionMode mode, IAppInstanceGuard instance, AppComIdentity? identity, CrashReportQueue? reportQueue = null) =>
        Build(uiContext, exceptions, mode, instance, identity, reportQueue, null);

    internal static IHost Build(WindowsFormsSynchronizationContext uiContext, AppExceptionBoundary exceptions,
        StartupExecutionMode mode, IAppInstanceGuard instance, AppComIdentity? identity, CrashReportQueue? reportQueue,
        Action<IServiceCollection>? configure) =>
        WawiCallHost.CreateBuilder(Assembly.GetExecutingAssembly())
            .ConfigureServices(services =>
            {
                services.AddSingleton(exceptions);
                services.AddSingleton(instance);
                services.AddSingleton(new AppWindowOptions());
                services.AddSingleton<AppWorkspace>();
                if (mode.DocumentationProfileDirectory is { } profile)
                    services.Replace(ServiceDescriptor.Singleton(new WawiCallPro.Utilities.Configuration.ConfigurationLocation(profile)));
                services.AddSingleton(_ => new WawiCallPro.Utilities.Configuration.ProductAppSettings(
                    Path.Combine(mode.DocumentationProfileDirectory ?? AppContext.BaseDirectory, "appsettings.json")));
                services.AddSingleton<IClientKeyExitPrompt, ClientKeyExitPrompt>();
                services.AddSingleton<ClientKeyExitProtection>();
                services.AddSingleton<CallNotesSessionFactory>();
                services.AddMailConnectors();
                services.AddSingleton<IAutoStartRegistry, AutoStartRegistry>();
                services.AddSingleton(_ => AppStartupCommand.ForCurrentProcess());
                services.AddSingleton<IAppAutoStart, WindowsAppAutoStart>();
                services.AddSingleton<LogWindowService>();
                services.AddSingleton<ILogWindowService>(provider => provider.GetRequiredService<LogWindowService>());
                services.AddSingleton<ILoggerProvider, UiLogProvider>();
                services.AddSingleton<IWindowDisplayEnvironment, WindowDisplayEnvironment>();
                services.AddSingleton(provider => new AppUiState(provider.GetRequiredService<ApplicationDescriptor>(), mode,
                    provider.GetRequiredService<IWindowDisplayEnvironment>()));
                services.AddSingleton<WawiCallPro.Plugin.Windows.IPartySearchPresentation, PartySearchPresentation>();
                services.AddSingleton<WawiCallPro.Interfaces.Plugins.IPartySearchWorkspaceFactory, WawiCallPro.Plugin.Windows.PartySearchWorkspaceFactory>();
                services.AddSingleton<WawiCallPro.Plugin.Windows.IPartySearchWorkspaceAccess, WawiCallPro.Plugin.Windows.PartySearchWorkspaceAccess>();
                services.AddSingleton<IAppHotkeyBackend, WindowsAppHotkeyBackend>();
                services.AddSingleton<IAppHotkeyFactory, AppHotkeyFactory>();
                services.AddAppStartupServices(mode);
                if (reportQueue is not null) services.AddSingleton(reportQueue);
                services.AddProductPluginRuntime();
                services.AddSingleton(new FunctionPluginLocation(Path.Combine(AppContext.BaseDirectory, "function-plugins")));
                services.AddSingleton<FunctionPluginRuntime>();
                services.AddSingleton<IFunctionPluginRuntime>(provider => provider.GetRequiredService<FunctionPluginRuntime>());
                services.AddHostedService(provider => provider.GetRequiredService<FunctionPluginRuntime>());
                services.AddSingleton<WindowsSipMediaSessionFactory>();
                services.AddSingleton<ISipMediaSessionFactory, LivePersistedSipMediaSessionFactory>();
                services.AddAudioConversion();
                if (!mode.SuppressNetwork) AddCloudAnnouncementSpeech(services);
                if (!mode.SuppressNetwork) services.AddSingleton<IIncomingRingtone>(provider => provider.GetRequiredService<WindowsIncomingRingtone>());
                services.AddPersistedRecordings();
                services.AddSingleton<WawiCallPro.Database.Repositories.RecordingPlaybackCatalog>();
                services.AddSingleton<IHistoryRecordingPlayback, HistoryRecordingPlayback>();
                services.AddSingleton<HistoryRecordingFiles>();
                services.AddSingleton<WawiCallPro.Interfaces.Audio.IAudioDeviceCatalog, WindowsAudioDeviceCatalog>();
                services.AddSipTelephony(!mode.SuppressNetwork);
                services.AddSingleton<AppTelephonyAccess>();
                services.AddSingleton(provider => new AppTelephonyService(provider.GetRequiredService<SipTelephonyService>(),
                    provider.GetRequiredService<SipTelephonyService>(), provider.GetRequiredService<AppTelephonyAccess>()));
                services.Replace(ServiceDescriptor.Singleton<WawiCallPro.Interfaces.Telephony.ITelephonyService>(provider => provider.GetRequiredService<AppTelephonyService>()));
                services.Replace(ServiceDescriptor.Singleton<WawiCallPro.Interfaces.Telephony.ITelephonyConnection>(provider => provider.GetRequiredService<AppTelephonyService>()));
                services.Replace(ServiceDescriptor.Singleton<WawiCallPro.Interfaces.Telephony.IDtmfReceiver>(provider => provider.GetRequiredService<AppTelephonyService>()));
                services.Replace(ServiceDescriptor.Singleton<WawiCallPro.Interfaces.Audio.ICallRecordingService>(provider => provider.GetRequiredService<AppTelephonyService>()));
                services.Replace(ServiceDescriptor.Singleton<WawiCallPro.Interfaces.Audio.ILiveCallRecordingService>(provider => provider.GetRequiredService<AppTelephonyService>()));
                if (!mode.SuppressNetwork) services.AddPhoneSearchMaintenance();
                if (!mode.SuppressNetwork) services.AddHostedService<MailStartupImport>();
                services.AddSingleton<WawiCallPro.Plugin.Windows.IPluginViewAccess, WawiCallPro.Plugin.Windows.PluginViewAccess>();
                services.AddSingleton(new WawiCallPro.Utilities.Plugins.SharedPluginContractAssembly(typeof(WawiCallPro.Plugin.Windows.IWawiCallPluginViews).Assembly));
                services.AddSingleton<IUiDispatcher>(_ => new SynchronizationContextUiDispatcher(uiContext));
                services.AddSingleton<IHostLifetime, AppHostLifetime>();
                services.AddAppAutomation(mode, identity);
                services.AddStartupProgress(mode);
                configure?.Invoke(services);
            })
            .Build();

    private static void AddCloudAnnouncementSpeech(IServiceCollection services)
    {
        var endpoint = Environment.GetEnvironmentVariable("WAWICALLPRO_AZURE_SPEECH_ENDPOINT");
        if (Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            try { services.AddAzureAnnouncementSpeech(uri); }
            catch (ArgumentException) { } // The dialog shows the setup hint when the endpoint is not usable.
        }
        var quotaProject = Environment.GetEnvironmentVariable("WAWICALLPRO_GOOGLE_TTS_PROJECT");
        if (!string.IsNullOrWhiteSpace(quotaProject))
            services.AddGoogleAnnouncementSpeech(quotaProject.Trim());

        services.AddSingleton<Func<AnnouncementSpeechProvider, IAnnouncementSpeech?>>(provider => selection => selection switch
        {
            AnnouncementSpeechProvider.AzureSpeech => provider.GetKeyedService<IAnnouncementSpeech>(CloudAnnouncementSpeechServices.AzureSpeech),
            AnnouncementSpeechProvider.GoogleCloud => provider.GetKeyedService<IAnnouncementSpeech>(CloudAnnouncementSpeechServices.GoogleCloud),
            _ => null
        });
    }
}

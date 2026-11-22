using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WawiCallPro.Interfaces.Plugins;
using WawiCallPro.Utilities.Diagnostics;

namespace WawiCallPro.App.Hosting;

/// <summary>Observed service boundaries, not an elapsed-time estimate. No source access or synthetic delays.</summary>
internal sealed class StartupProgress(StartupSplash splash, IProductPluginRuntime runtime)
{
    private int _percent;
    private string? _plugin;
    internal string LoadedPlugins { get; private set; } = "Geladene Plugins: noch keine";
    internal int Percent => _percent;
    internal async Task ReportAsync(Type serviceType, int index, int total, bool completed, CancellationToken token)
    {
        var title = ServiceTitle(serviceType);
        var percent = 2 + (int)(88L * (index * 2L + (completed ? 2 : 1)) / (Math.Max(1, total) * 2L));
        _percent = Math.Max(_percent, Math.Clamp(percent, 0, 90));
        if (runtime.IsLoaded && runtime is IProductPluginSelection { ActivePluginId: { } id } && _plugin != id)
        {
            (string Name, string Version) name;
            try { name = await runtime.UseAsync((plugin, _) => Task.FromResult((plugin.Name, plugin.Version.ToString())), token); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch { name = (id.EndsWith(".Pro", StringComparison.Ordinal) ? "Lexware Pro" : id.EndsWith(".FinOffice", StringComparison.Ordinal) ? "Lexware Financial Office" : "Warenwirtschaftsplugin", ""); }
            var safeName = string.IsNullOrWhiteSpace(name.Name) || name.Name.Length > 120 || name.Name.Any(char.IsControl)
                ? "Warenwirtschaftsplugin" : name.Name;
            LoadedPlugins = "Geladene Plugins: " + safeName + (name.Version.Length is > 0 and <= 40 ? " (v" + name.Version + ")" : "");
            _plugin = id;
        }
        var status = completed ? title + " gestartet." : title + " wird gestartet …";
        await splash.UpdateDetailedAsync(status + " (" + (index + 1) + "/" + total + ")", _percent, LoadedPlugins, token);
    }
    internal static string ServiceTitle(Type type) => type.Name switch
    {
        "ApplicationConfigurationService" => "Konfiguration",
        "AppUiStartup" => "Schrift, Skin und Darstellung",
        "StartupSplashHost" => "Startanzeige",
        "LicenseStartupGate" => "Lizenzprüfung",
        "LicenseMonitor" => "Lizenzüberwachung",
        "CrashReportUploader" => "Fehlerberichts-Warteschlange",
        "ProductPluginRuntime" => "Warenwirtschaftsplugins",
        "CompanyContextLifetime" => "Mandantenkontext",
        "SipHostService" => "SIP- und Gesprächsdienste",
        "AppAutomationCatalog" => "Mandanten- und Ansichtskatalogdienst",
        "AppAutomationTelephony" => "COM-Telefonieanbindung",
        "AppCallAssociation" => "Gesprächszuordnung",
        "AppAutomationShutdown" => "COM-Lebensdauerüberwachung",
        "PhoneSearchMaintenance" => "Rufnummernindex",
        "RuntimeFailureMonitor" => "Laufzeitüberwachung",
        "MailStartupImport" => "Mail-Importdienst",
        "RecordingConversionRecoveryStartup" => "Aufnahme-Wiederanlauf",
        "RecordingConversionQueue" => "MP3-Warteschlange",
        "RecordingArtifactAuditScheduler" => "Aufnahmedatei-Prüfdienst",
        _ => "Anwendungsdienst"
    };
}

/// <summary>Markers keep the actual hosted services intact: background-task monitoring, lifecycle and ownership do not change.</summary>
internal sealed class StartupProgressStep(StartupProgress progress, Func<Type> serviceType, int index, int total, bool completed) : IHostedService
{
    public Task StartAsync(CancellationToken token) => progress.ReportAsync(serviceType(), index, total, completed, token);
    public Task StopAsync(CancellationToken token) => Task.CompletedTask;
}

internal static class StartupProgressRegistration
{
    internal static void AddStartupProgress(this IServiceCollection services, StartupExecutionMode mode)
    {
        services.AddSingleton<StartupProgress>();
        if (mode.ComActivated) return; // The deferred COM pipeline reports its own actual service boundaries.
        var original = services.Where(item => item.ServiceType == typeof(IHostedService)).ToArray();
        for (var index = 0; index < original.Length; index++)
        {
            var slot = index;
            Type ResolveType(IServiceProvider provider) => provider.GetServices<IHostedService>()
                .Where(service => service is not StartupProgressStep).ElementAt(slot).GetType();
            var position = services.IndexOf(original[index]);
            services.Insert(position, ServiceDescriptor.Singleton<IHostedService>(provider => new StartupProgressStep(
                provider.GetRequiredService<StartupProgress>(), () => ResolveType(provider), slot, original.Length, false)));
            services.Insert(position + 2, ServiceDescriptor.Singleton<IHostedService>(provider => new StartupProgressStep(
                provider.GetRequiredService<StartupProgress>(), () => ResolveType(provider), slot, original.Length, true)));
        }
    }
}

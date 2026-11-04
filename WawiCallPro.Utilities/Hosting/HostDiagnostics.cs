using Microsoft.Extensions.Hosting;
using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Interfaces.Plugins;

namespace WawiCallPro.Utilities.Hosting;

/// <summary>Allowlisted startup status; never serialize the options, rejected inputs or secret payloads into diagnostics.</summary>
public sealed record HostDiagnosticSnapshot(
    string Application,
    string ProductLine,
    bool HostStarted,
    string Dispatcher,
    bool CompanySelected)
{
    public bool ConnectionConfigured { get; init; }
    public bool ConfigurationFound { get; init; }
    public int SettingsFormatVersion { get; init; }
    public bool SettingsUpgradeRequired { get; init; }
    public bool PluginLoaded { get; init; }
}

public sealed class HostDiagnostics(ApplicationDescriptor application, ICompanyContext companies,
    IUiDispatcher dispatcher, IHostApplicationLifetime lifetime, ApplicationConfigurationService configuration,
    IEnumerable<IProductPluginRuntime> plugins)
{
    public async Task<HostDiagnosticSnapshot> CaptureAsync(CancellationToken cancellationToken = default)
    {
        var selected = false;
        await dispatcher.InvokeAsync(() => selected = companies.Current.Company is not null, cancellationToken)
            .ConfigureAwait(false);
        return new HostDiagnosticSnapshot(application.Name, application.ProductLine,
            lifetime.ApplicationStarted.IsCancellationRequested, dispatcher.GetType().Name, selected)
        {
            ConnectionConfigured = configuration.Current.IsSipConfigured,
            ConfigurationFound = configuration.Current.FileFound,
            SettingsFormatVersion = configuration.Current.FormatVersion,
            SettingsUpgradeRequired = configuration.Current.RequiresUpgrade,
            PluginLoaded = plugins.Any(plugin => plugin.IsLoaded)
        };
    }
}

using System.Collections.Immutable;
using Microsoft.Extensions.Hosting;
using WawiCallPro.Automation;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Interfaces.Plugins;
using WawiCallPro.Plugin.Windows;

namespace WawiCallPro.App.Hosting;

/// <summary>Loads detached catalogs asynchronously. COM reads never perform source IO.</summary>
internal sealed class AppAutomationCatalog(IProductPluginRuntime runtime, IBusinessDirectory directory,
    IUiDispatcher dispatcher, AppAutomationState state, ILogWindowService logs) : IHostedService, IAsyncDisposable
{
    internal static ImmutableArray<AutomationView> BaseViews { get; } = [
        new("home", "Startseite"), new("history", "Gesprächshistorie"), new("mail", "E-Mails"),
        new("help", "Hilfe"), new("about", "Über WawiCallPro"), new("log", "Log-Fenster"),
        new("call", "Gesprächsdetails") { Context = "callId" }, new("database", "Datenbank einrichten"),
        new("settings", "Einstellungen"), new("settings-general", "Allgemein"), new("plugin", "Plugins"),
        new("settings-hotkeys", "Tastenkürzel"), new("settings-sip", "SIP-Konto"), new("settings-sip-advanced", "SIP – Erweitert"),
        new("settings-line-company", "Leitungsfirma"), new("settings-database", "Datenbank"),
        new("settings-audio", "Audiogeräte"), new("settings-audio-levels", "Lautstärken und MP3"),
        new("settings-phone-cache", "Telefon-Cache"), new("settings-cache-sources", "Cachequellen"),
        new("settings-phone-matching", "Stammnummernabgleich"), new("number-diagnostics", "Nummerndiagnostik"), new("settings-mail", "Office / Mail") ];
    private readonly SemaphoreSlim _gate = new(1);
    private readonly CancellationTokenSource _stopping = new();
    private Task? _watch;
    private string? _loadedPlugin;
    private int _disposed;
    private string? PluginId => (runtime as IProductPluginSelection)?.ActivePluginId;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        state.Runtime = runtime;
        await RefreshAsync(cancellationToken);
        _watch = WatchAsync();
    }
    private async Task WatchAsync()
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
            while (await timer.WaitForNextTickAsync(_stopping.Token))
                if (_loadedPlugin != PluginId) await RefreshAsync(_stopping.Token);
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested) { }
    }
    internal async Task RefreshAsync(CancellationToken token)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token, _stopping.Token);
        token = cancellation.Token;
        await _gate.WaitAsync(token);
        try
        {
            var pluginId = PluginId;
            state.PublishCatalog([], BaseViews, pluginId, false);
            try
            {
                if (!runtime.IsLoaded || runtime is IProductPluginAvailability availability && !availability.CanUseActivePlugin) return;
                var settingsTitle = await runtime.UseAsync((plugin, _) => Task.FromResult(
                    plugin is IWawiCallPluginSettings { HasConnectionSettings: true } settings ? settings.ConnectionSettingsTitle : null), token);
                var availableViews = BaseViews;
                if (settingsTitle is not null)
                {
                    if (string.IsNullOrWhiteSpace(settingsTitle) || settingsTitle.Length > 120 || settingsTitle.Any(char.IsControl))
                        throw new InvalidOperationException("Invalid plugin settings title.");
                    availableViews = availableViews.Add(new("settings-wawi", settingsTitle));
                }
                token.ThrowIfCancellationRequested();
                if (pluginId != PluginId) return;
                state.PublishCatalog([], availableViews, pluginId, false);
                var companies = await directory.GetCompaniesAsync(token);
                var line = state.GetStatus().ProductLine;
                if (companies.IsDefault || companies.Length > 1000 || companies.Any(company => company?.Key is null
                    || company.Key.ProductLine != line || string.IsNullOrWhiteSpace(company.DisplayName) || company.DisplayName.Length > 256)
                    || companies.Select(company => company.Key).Distinct().Count() != companies.Length) throw new InvalidOperationException("Invalid company catalog.");
                var views = await runtime.UseAsync(async (plugin, cancellation) =>
                {
                    if (plugin.ProductLine != line) throw new InvalidOperationException("Foreign product catalog.");
                    var items = ImmutableArray<PluginViewDescriptor>.Empty;
                    if (plugin is IWawiCallPluginViews windows)
                        await dispatcher.InvokeAsync(() => items = windows.GetViews().ToImmutableArray(), cancellation);
                    if (items.Length > 200 || items.Any(view => string.IsNullOrWhiteSpace(view.Id) || view.Id.Length > 120 || view.Id.Any(char.IsControl)
                        || string.IsNullOrWhiteSpace(view.DisplayName) || view.DisplayName.Length > 256) || items.Select(view => view.Id).Distinct().Count() != items.Length)
                        throw new InvalidOperationException("Invalid plugin view catalog.");
                    return (Workspaces: plugin is IWawiCallPluginWorkspaces, Views: items);
                }, token);
                token.ThrowIfCancellationRequested();
                if (pluginId != PluginId) return;
                var result = availableViews;
                if (views.Workspaces) result = result.Add(new("parties", "Kunden und Lieferanten"));
                result = result.Add(new("caller", "Anrufersuche") { Context = "callId optional für ein aktives Gespräch" }).Add(new("dial", "Wähldialog") { Context = "companyKey" });
                result = result.AddRange(views.Views.Select(view => new AutomationView("plugin:" + view.Id, view.DisplayName) { Context = "companyKey + customerNumber oder supplierNumber" }));
                state.PublishCatalog(companies, result, pluginId, true);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception) { logs.LogWarning("Der COM-Mandanten- und Viewkatalog konnte nicht geladen werden. Verbindung und Plugin prüfen."); }
            finally { _loadedPlugin = pluginId; }
        }
        finally { _gate.Release(); }
    }
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _disposed) != 0) return;
        await _stopping.CancelAsync();
        if (_watch is not null) await _watch;
        await _gate.WaitAsync(CancellationToken.None); _gate.Release();
    }
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        await _stopping.CancelAsync();
        if (_watch is not null) await _watch;
        await _gate.WaitAsync(CancellationToken.None); _gate.Release();
        _stopping.Dispose(); _gate.Dispose();
    }
}

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
        new("settings-database", "Datenbank"),
        new("settings-audio", "Audiogeräte"), new("settings-audio-levels", "Lautstärken und MP3"),
        new("settings-phone-cache", "Telefon-Cache"), new("settings-cache-sources", "Cachequellen"),
        new("settings-phone-matching", "Stammnummernabgleich"), new("number-diagnostics", "Nummerndiagnostik"), new("settings-mail", "Office / Mail") ];
    private readonly SemaphoreSlim _gate = new(1);
    private readonly CancellationTokenSource _stopping = new();
    private Task? _watch;
    private string? _loadedPlugin;
    private long _retryAtTicks;
    private long _connectionRevision = -1;
    private string? _lastNotice;
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
            {
                var revision = -1L;
                try { if (runtime.IsLoaded) revision = await runtime.UseAsync((plugin, _) => Task.FromResult((plugin as IBusinessDirectoryConnectionState)?.ConnectionRevision ?? 0), _stopping.Token); }
                catch (OperationCanceledException) when (_stopping.IsCancellationRequested) { throw; }
                catch { }

                if (_loadedPlugin != PluginId || revision != _connectionRevision || Volatile.Read(ref _retryAtTicks) is > 0 and var retry && DateTimeOffset.UtcNow.UtcTicks >= retry)
                    await RefreshAsync(_stopping.Token);
            }
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
            Volatile.Write(ref _retryAtTicks, 0);
            var stage = "Views";
            state.PublishCatalog([], BaseViews, pluginId, false);
            try
            {
                if (!runtime.IsLoaded || runtime is IProductPluginAvailability availability && !availability.CanUseActivePlugin) return;
                var setup = await runtime.UseAsync((plugin, _) => Task.FromResult((
                    Title: plugin is IWawiCallPluginSettings { HasConnectionSettings: true } settings ? settings.ConnectionSettingsTitle : null,
                    Configured: plugin is not IBusinessDirectoryConnectionState connection || connection.IsConnectionConfigured,
                    Revision: (plugin as IBusinessDirectoryConnectionState)?.ConnectionRevision ?? 0)), token);
                _connectionRevision = setup.Revision;
                var settingsTitle = setup.Title;
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
                if (!setup.Configured)
                {
                    if (_lastNotice != pluginId + ":Setup") logs.LogNormal("Der Warenwirtschaftszugang ist noch nicht eingerichtet. Bitte den Zugang im Einstellungsdialog konfigurieren.");
                    _lastNotice = pluginId + ":Setup";
                    return;
                }
                var line = state.GetStatus().ProductLine;
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
                stage = "Companies";
                var companies = await directory.GetCompaniesAsync(token);
                if (companies.IsDefault || companies.Length > 1000 || companies.Any(company => company?.Key is null
                    || company.Key.ProductLine != line || string.IsNullOrWhiteSpace(company.DisplayName) || company.DisplayName.Length > 256)
                    || companies.Select(company => company.Key).Distinct().Count() != companies.Length) throw new InvalidOperationException("Invalid company catalog.");
                token.ThrowIfCancellationRequested();
                if (pluginId != PluginId) return;
                var currentRevision = await runtime.UseAsync((plugin, _) => Task.FromResult((plugin as IBusinessDirectoryConnectionState)?.ConnectionRevision ?? 0), token);
                if (currentRevision != setup.Revision || pluginId != PluginId) return;
                state.PublishCatalog(companies, result, pluginId, true);
                _lastNotice = null;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception)
            {
                if (_lastNotice != pluginId + ":" + stage) logs.LogWarning(stage == "Companies"
                    ? "Mandanten konnten nicht geladen werden. Bitte Warenwirtschaftszugang und Verbindung prüfen; der Abruf wird erneut versucht."
                    : "Die Ansichten des Warenwirtschaftsplugins konnten nicht geladen werden. Bitte das aktive Plugin prüfen; der Abruf wird erneut versucht.");
                _lastNotice = pluginId + ":" + stage;
                Volatile.Write(ref _retryAtTicks, DateTimeOffset.UtcNow.AddSeconds(30).UtcTicks);
            }
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

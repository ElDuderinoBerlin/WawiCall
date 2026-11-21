using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using WawiCallPro.Automation;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Utilities.Hosting;
using WawiCallPro.Utilities.Licensing;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.App.Hosting;

/// <summary>Early, detached COM status. No form or business service is constructed by a status read.</summary>
internal sealed class AppAutomationState(ApplicationDescriptor application, TimeProvider clock) : IAutomationEnvironment
{
    private readonly object _gate = new();
    private readonly ConditionalWeakTable<AutomationApplication, Lease> _leases = new();
    private int _clients, _locks;
    private DateTimeOffset _changed = clock.GetUtcNow();
    private string _state = "Starting";
    private bool _visible, _owned, _stopping, _userMode;
    internal int ServerLocks { get { lock (_gate) return _locks; } }
    internal bool UserMode { get { lock (_gate) return _userMode; } }
    internal Func<CancellationToken, Task>? ShowRequested { get; set; }
    internal Func<PresentationRequest, CancellationToken, Task>? PresentRequested { get; set; }
    internal WawiCallPro.Interfaces.Plugins.IProductPluginRuntime? Runtime { get; set; }
    private ImmutableArray<Company> _companies = [];
    private ImmutableArray<AutomationView> _views = AppAutomationCatalog.BaseViews;
    private string? _catalogPlugin;
    private bool _businessReady;
    internal void PublishCatalog(ImmutableArray<Company> companies, ImmutableArray<AutomationView> views, string? plugin, bool businessReady)
    { lock (_gate) { _companies = companies; _views = views; _catalogPlugin = plugin; _businessReady = businessReady; } }
    internal WawiCallPro.Interfaces.Telephony.ITelephonyService? Telephony { get; set; }
    public AutomationStatus GetStatus()
    {
        lock (_gate) return new(application.ProductLine == "Pro" ? LexwareProductLine.Pro : LexwareProductLine.FinOffice,
            _state, _visible, Telephony?.Registration.State.ToString() ?? "Stopped", _state is "Failed" or "ConfigurationRequired" ? "ProviderUnavailable" : "");
    }
    internal void Ready() { lock (_gate) { if (_stopping) return; _state = "Ready"; _changed = clock.GetUtcNow(); } }
    internal void Failed(Exception failure)
    {
        lock (_gate) { if (_stopping) return; _state = failure is LicenseAccessException ? "LicenseRequired" : failure is ConfigurationException ? "ConfigurationRequired" : "Failed"; _changed = clock.GetUtcNow(); }
    }
    internal void EnterUserMode() { lock (_gate) { if (_stopping) throw new AutomationFault(AutomationFailure.Stopping); _userMode = true; } }
    internal void Visible() { lock (_gate) { _visible = true; _owned = true; } }
    internal void UpdateVisibility(bool visible) { lock (_gate) _visible = visible; }
    internal void Stopping() { lock (_gate) { _stopping = true; _state = "Stopping"; } }
    internal void Track(AutomationApplication instance)
    {
        lock (_gate)
        {
            if (_stopping) throw new AutomationFault(AutomationFailure.Stopping);
            _clients++; _changed = clock.GetUtcNow(); _leases.Add(instance, new Lease(this));
        }
    }
    internal void ServerLock(bool acquire)
    {
        lock (_gate)
        {
            if (acquire && _stopping) throw new AutomationFault(AutomationFailure.Stopping);
            if (acquire && _locks >= 4096) throw new AutomationFault(AutomationFailure.Busy);
            if (!acquire && _locks == 0) throw new AutomationFault(AutomationFailure.Conflict);
            _locks += acquire ? 1 : -1; _changed = clock.GetUtcNow();
        }
    }
    internal bool TryIdleShutdown(bool hasWork)
    {
        lock (_gate)
        {
            if (_stopping) return false;
            if (Telephony?.GetCalls().Any(AppTelephonyService.IsActive) == true) { _changed = clock.GetUtcNow(); return false; }
            if (_state == "Starting" || _owned || _clients != 0 || _locks != 0 || hasWork
                || clock.GetUtcNow() - _changed < TimeSpan.FromSeconds(30)) return false;
            _stopping = true; _state = "Stopping"; return true;
        }
    }
    private sealed class Lease(AppAutomationState owner)
    {
        ~Lease() { lock (owner._gate) { owner._clients--; owner._changed = owner.clockNow(); } }
    }
    private DateTimeOffset clockNow() => clock.GetUtcNow();
    public ImmutableArray<Company> GetCompanies() { lock (_gate) return _companies; }
    public ImmutableArray<AutomationView> GetViews()
    {
        lock (_gate)
        {
            if (BusinessAvailable()) return _views;
            // Connection setup remains reachable before a source catalog can be authenticated.
            if ((Runtime as WawiCallPro.Interfaces.Plugins.IProductPluginSelection)?.ActivePluginId == _catalogPlugin
                && Runtime?.IsLoaded == true && (Runtime is not WawiCallPro.Interfaces.Plugins.IProductPluginAvailability availability || availability.CanUseActivePlugin))
                return AppAutomationCatalog.BaseViews.AddRange(_views.Where(view => view.Id == "settings-wawi"));
            return AppAutomationCatalog.BaseViews;
        }
    }
    private bool BusinessAvailable() => _businessReady && (Runtime as WawiCallPro.Interfaces.Plugins.IProductPluginSelection)?.ActivePluginId == _catalogPlugin
        && Runtime?.IsLoaded == true && (Runtime is not WawiCallPro.Interfaces.Plugins.IProductPluginAvailability available || available.CanUseActivePlugin);
    public void RequireAccess(AutomationAccess access, CompanyKey? company = null)
    {
        lock (_gate)
        {
            if (_stopping) throw new AutomationFault(AutomationFailure.Stopping);
            if (access == AutomationAccess.Telephony && Telephony is null) throw new AutomationFault(AutomationFailure.NotReady);
            if (access == AutomationAccess.Business)
            {
                if (_state != "Ready") throw new AutomationFault(AutomationFailure.NotReady);
                if (!BusinessAvailable() || company is not null && !_companies.Any(item => item.Key == company)) throw new AutomationFault(AutomationFailure.AccessDenied);
            }
            if (_state == "Starting") throw new AutomationFault(AutomationFailure.NotReady);
        }
    }
}

internal sealed class AppAutomationFactory(IAutomationApplicationFactory inner, AppAutomationState state) : IAutomationApplicationFactory
{
    public AutomationApplication Create() { var instance = inner.Create(); state.Track(instance); return instance; }
}

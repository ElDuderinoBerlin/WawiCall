using System.Text.Json;
using WawiCallPro.Automation;

namespace WawiCallPro.ComPrototype;

internal sealed class ContractFixtureContext : ApplicationContext
{
    private readonly PrototypeOptions _options;
    private readonly ServerLifetime _lifetime;
    private readonly AutomationClassRegistration _registration;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 100 };
    private bool _disposed;
    private Func<ValueTask> _stopServices = () => ValueTask.CompletedTask;
    internal void ConfigureShutdown(Func<ValueTask> stopServices) => _stopServices = stopServices;
    public ContractFixtureContext(PrototypeOptions options, ServerLifetime lifetime, IAutomationApplicationFactory factory)
    {
        _options = options; _lifetime = lifetime;
        // Phase 2 checks contracts, not product CCW lifetime. Explicit fixture ownership holds the host.
        lifetime.SetUserOwned(true); lifetime.CompleteStartup();
        _registration = new(options.ClassId, factory, lifetime.LockServer);
        File.WriteAllText(Path.Combine(options.Profile, "factory.json"), JsonSerializer.Serialize(new { Pid = Environment.ProcessId, UiThread = Environment.CurrentManagedThreadId }));
        _timer.Tick += Tick; _timer.Start();
    }
    private async void Tick(object? sender, EventArgs e)
    {
        var release = Path.Combine(_options.Profile, "user-off.request");
        if (File.Exists(release)) { File.Delete(release); _lifetime.SetUserOwned(false); }
        if (!_lifetime.TryBeginShutdown()) return;
        _registration.Dispose(); _timer.Stop();
        await _stopServices();
        File.WriteAllText(Path.Combine(_options.Profile, "stopped.json"), JsonSerializer.Serialize(new { Pid = Environment.ProcessId, Revoked = true }));
        ExitThread();
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed) { _disposed = true; _timer.Dispose(); _registration.Dispose(); }
        base.Dispose(disposing);
    }
}

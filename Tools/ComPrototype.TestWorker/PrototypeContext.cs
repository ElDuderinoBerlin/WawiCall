using System.Runtime.InteropServices;
using System.Text.Json;

namespace WawiCallPro.ComPrototype;

/// <summary>A single WinForms pump without a Form, tray icon, network, database or product configuration.</summary>
internal sealed class PrototypeContext : ApplicationContext
{
    private readonly PrototypeOptions _options;
    private readonly ServerLifetime _lifetime;
    private readonly ProbeDependency _dependency;
    private readonly ComClassRegistration _registration;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 100 };
    private readonly CancellationTokenSource _stopping = new();
    private readonly Task _startup;
    private bool _disposed;

    internal PrototypeContext(PrototypeOptions options, ServerLifetime lifetime, ProbeDependency dependency, Func<PrototypeAutomation> create)
    {
        _options = options; _lifetime = lifetime; _dependency = dependency;
        // Install a UI context before starting async initialization; Application.Run owns the only pump.
        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
        _registration = new(options.ClassId, create, lifetime);
        File.WriteAllText(Path.Combine(options.Profile, "factory.json"), JsonSerializer.Serialize(new
        { Pid = Environment.ProcessId, dependency.Id, dependency.UiThread, Apartment = Thread.CurrentThread.GetApartmentState().ToString() }));
        _timer.Tick += Tick;
        _timer.Start();
        _startup = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            await Task.Delay(_options.StartupDelayMs, _stopping.Token);
            _dependency.Ready = true;
            _lifetime.CompleteStartup();
            File.WriteAllText(Path.Combine(_options.Profile, "ready.json"), JsonSerializer.Serialize(new { Pid = Environment.ProcessId }));
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested) { }
    }

    private void Tick(object? sender, EventArgs e)
    {
        // Explicit fixture commands for checking ownership after the client process has exited.
        if (Consume("user-on.request")) _lifetime.SetUserOwned(true);
        if (Consume("user-off.request")) _lifetime.SetUserOwned(false);
        if (Consume("call-off.request")) _lifetime.SetCallActive(false);
        if (Consume("collect.request"))
        {
            // Test synchronization only: production ownership must never depend on periodic forced GC.
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            File.WriteAllText(Path.Combine(_options.Profile, "collected.json"), JsonSerializer.Serialize(new { Clients = _lifetime.Clients }));
        }
        if (!_lifetime.TryBeginShutdown()) return;
        Marshal.ThrowExceptionForHR(NativeProbe.CoSuspendClassObjects());
        _registration.Dispose();
        _timer.Stop();
        File.WriteAllText(Path.Combine(_options.Profile, "stopped.json"), JsonSerializer.Serialize(new
        { Pid = Environment.ProcessId, Revoked = true, VisibleWindows = NativeProbe.CountVisibleWindows(), Clients = _lifetime.Clients }));
        ExitThread();
    }

    private bool Consume(string name)
    {
        var path = Path.Combine(_options.Profile, name);
        if (!File.Exists(path)) return false;
        File.Delete(path);
        return true;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            _timer.Dispose();
            _stopping.Cancel();
            _registration.Dispose();
            if (_startup.IsFaulted) _startup.GetAwaiter().GetResult();
            _stopping.Dispose();
        }
        base.Dispose(disposing);
    }
}

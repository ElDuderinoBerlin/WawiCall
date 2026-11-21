using System.IO;
using System.IO.MemoryMappedFiles;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using Quantarix.Windows.Win32;
using WawiCallPro.Utilities.Hosting;
using WawiCallPro.Utilities.Operations;

namespace WawiCallPro.App.Hosting;

internal interface IAppInstanceGuard : IDisposable
{
    bool TryAcquire();
    void RequestExistingInstanceActivation();
    bool TryConsumeActivation();
    event EventHandler? ActivationRequested;
}

/// <summary>One interactive process per user/product/profile. Named signals also survive startup before a window exists.</summary>
internal sealed class AppInstanceGuard(ApplicationDescriptor application, ApplicationPaths paths) : IAppInstanceGuard, IComInstancePublication
{
    private Mutex? _mutex;
    private EventWaitHandle? _signal;
    private EventWaitHandle? _automationReady;
    private MemoryMappedFile? _owner;
    private RegisteredWaitHandle? _listener;
    private bool _owned;
    private volatile bool _disposed;
    private int _activationPending;
    public event EventHandler? ActivationRequested;

    public bool TryAcquire()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_mutex is not null) return _owned;
        using var user = WindowsIdentity.GetCurrent();
        var profile = $"{user.User?.Value}|{Path.GetFullPath(paths.RootDirectory).TrimEnd(Path.DirectorySeparatorChar).ToUpperInvariant()}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(profile)))[..32];
        var name = $@"Local\WawiCallPro.{application.ProductLine}.{hash}";
        _mutex = new Mutex(false, name);
        try { _owned = _mutex.WaitOne(0); }
        catch (AbandonedMutexException) { _owned = true; }
        _signal = new EventWaitHandle(false, EventResetMode.AutoReset, name + ".Activate");
        _automationReady = new EventWaitHandle(false, EventResetMode.ManualReset, name + ".AutomationReady");
        _owner = MemoryMappedFile.CreateOrOpen(name + ".Owner", sizeof(int));
        if (_owned)
        {
            _automationReady.Reset();
            using var owner = _owner.CreateViewAccessor();
            owner.Write(0, Environment.ProcessId);
            owner.Flush();
            _listener = ThreadPool.RegisterWaitForSingleObject(_signal, OnSignal, null, Timeout.Infinite, false);
        }
        return _owned;
    }

    private void OnSignal(object? state, bool timedOut)
    {
        if (_disposed) return;
        Interlocked.Exchange(ref _activationPending, 1);
        ActivationRequested?.Invoke(this, EventArgs.Empty);
    }

    public bool TryConsumeActivation() => Interlocked.Exchange(ref _activationPending, 0) != 0;
    public void PublishAutomation() { if (!_owned) throw new InvalidOperationException("Only the owner may publish automation readiness."); _automationReady!.Set(); }
    public void WithdrawAutomation() { if (_owned) _automationReady?.Reset(); }
    public bool WaitForAutomation(TimeSpan timeout)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_owned || _automationReady is null || _mutex is null) return false;
        int signal;
        try { signal = WaitHandle.WaitAny([_mutex, _automationReady], timeout); }
        catch (AbandonedMutexException error) { signal = error.MutexIndex; }
        if (signal == 1) return true;
        if (signal != 0) return false;
        // The old host withdrew its class and finished shutdown. This thread now owns the same mutex;
        // Program continues startup instead of reporting a stale ready event to COM and exiting.
        _owned = true; _automationReady.Reset();
        using var owner = _owner!.CreateViewAccessor();
        owner.Write(0, Environment.ProcessId); owner.Flush();
        _listener = ThreadPool.RegisterWaitForSingleObject(_signal!, OnSignal, null, Timeout.Infinite, false);
        return false;
    }

    public void RequestExistingInstanceActivation()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_owned || _signal is null) return;
        // The owner belongs to this exact mutex/profile, independent of apphost, dotnet or edition EXE names.
        using var owner = _owner!.CreateViewAccessor(0, sizeof(int), MemoryMappedFileAccess.Read);
        var processId = 0;
        SpinWait.SpinUntil(() => (processId = owner.ReadInt32(0)) > 0, TimeSpan.FromMilliseconds(250));
        if (processId > 0 && processId != Environment.ProcessId) User32.AllowSetForegroundWindow((uint)processId);
        _signal.Set();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _listener?.Unregister(null);
        _signal?.Dispose();
        if (_owned) _automationReady?.Reset();
        _automationReady?.Dispose();
        _owner?.Dispose();
        if (_owned) _mutex!.ReleaseMutex();
        _mutex?.Dispose();
    }
}

internal interface IComInstancePublication
{
    void PublishAutomation();
    void WithdrawAutomation();
    bool WaitForAutomation(TimeSpan timeout);
}

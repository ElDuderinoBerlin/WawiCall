using Microsoft.Extensions.Logging;
using Quantarix.Windows.Win32;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Diagnostics;
using WawiCallPro.Utilities.Operations;

namespace WawiCallPro.App.Hosting;

internal enum AppHotkeyAction { ShowWindow, OpenDial }

internal interface IAppHotkeyBackend
{
    bool Register(IntPtr window, int id, int modifiers, int key);
    bool Unregister(IntPtr window, int id);
}

internal sealed class WindowsAppHotkeyBackend : IAppHotkeyBackend
{
    public bool Register(IntPtr window, int id, int modifiers, int key) => User32.RegisterHotKey(window, id, modifiers, key);
    public bool Unregister(IntPtr window, int id) => User32.UnregisterHotKey(window, id);
}

internal interface IAppHotkeyFactory
{
    AppHotkeySession Create(IntPtr window, UserSettings settings, Action<AppHotkeyAction> execute);
}

/// <summary>Creates a session owned by one main-window handle. Diagnostics never reserve system shortcuts.</summary>
internal sealed class AppHotkeyFactory(IAppHotkeyBackend backend, StartupExecutionMode mode, ILogger<AppHotkeyFactory> logger) : IAppHotkeyFactory
{
    private int _nextId;
    public AppHotkeySession Create(IntPtr window, UserSettings settings, Action<AppHotkeyAction> execute)
    {
        var session = new AppHotkeySession(window, backend, execute, logger);
        if (mode.SuppressNetwork) return session;
        session.Register(NextId(), settings.ShowWindowHotkey, AppHotkeyAction.ShowWindow);
        session.Register(NextId(), settings.DialHotkey, AppHotkeyAction.OpenDial);
        return session;
    }
    private int NextId() => Interlocked.Increment(ref _nextId);
}

internal sealed class AppHotkeySession(IntPtr window, IAppHotkeyBackend backend, Action<AppHotkeyAction> execute, ILogger logger) : IDisposable
{
    internal const int MessageId = 0x0312;
    internal const int Modifiers = 0x0001 | 0x0002;
    private readonly Dictionary<int, (int Key, AppHotkeyAction Action)> _registered = [];
    private bool _disposed;

    internal void Register(int id, string? combination, AppHotkeyAction action)
    {
        if (combination is null) return;
        var functionKey = GlobalHotkeySettings.GetFunctionKey(combination);
        if (functionKey == 0 || window == IntPtr.Zero || id > 0xBFFF)
        { OperationEvents.Write(logger, OperationEvent.GlobalHotkeyUnavailable); return; }
        var key = 0x70 + functionKey - 1;
        if (!backend.Register(window, id, Modifiers | 0x4000, key))
        {
            OperationEvents.Write(logger, OperationEvent.GlobalHotkeyUnavailable);
            return;
        }
        _registered.Add(id, (key, action));
    }

    internal bool TryDispatch(IntPtr identifier, IntPtr data)
    {
        var id = identifier.ToInt64();
        if (_disposed || id is < 1 or > 0xBFFF || !_registered.TryGetValue((int)id, out var binding)) return false;
        if (data.ToInt64() != ((long)binding.Key << 16 | Modifiers)) return false;
        execute(binding.Action);
        return true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var id in _registered.Keys)
            if (!backend.Unregister(window, id)) OperationEvents.Write(logger, OperationEvent.GlobalHotkeyReleaseFailed);
        _registered.Clear();
    }
}

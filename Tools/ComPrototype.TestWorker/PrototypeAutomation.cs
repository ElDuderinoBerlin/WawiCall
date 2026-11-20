using System.Runtime.InteropServices;
using Quantarix.Interop.COM;
using Quantarix.Interop.Interfaces;

namespace WawiCallPro.ComPrototype;

// Test-only IDispatch contract. No product CLSID or type-library registration is used.
[ComVisible(true), Guid("FC231586-49D8-4638-AC94-5B480AC9547E")]
[InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
public interface IPrototypeAutomation
{
    [DispId(1)] int ProcessId { get; }
    [DispId(2)] string DependencyId { get; }
    [DispId(3)] string State { get; }
    [DispId(4)] int ThreadId { get; }
    [DispId(5)] int VisibleWindows { get; }
    [DispId(6)] void SetSimulatedUserOwnership(bool value);
    [DispId(7)] void SetSimulatedCall(bool value);
    [DispId(8)] void Close();
    [DispId(9)] void Fail();
}

internal sealed class ProbeDependency
{
    internal string Id { get; } = Guid.NewGuid().ToString("D");
    internal int UiThread { get; } = Environment.CurrentManagedThreadId;
    internal bool Ready { get; set; }
}

[ComVisible(true), ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IPrototypeAutomation))]
public sealed class PrototypeAutomation : IPrototypeAutomation, ISupportErrorInfo
{
    private readonly ProbeDependency _dependency;
    private readonly ServerLifetime _lifetime;
    private readonly IUiDispatcher _dispatcher;
    private IDisposable? _lease;

    internal PrototypeAutomation(ProbeDependency dependency, ServerLifetime lifetime, IUiDispatcher dispatcher)
    { _dependency = dependency; _lifetime = lifetime; _dispatcher = dispatcher; _lease = lifetime.AcquireClient(); }

    public int ProcessId => Environment.ProcessId;
    public string DependencyId => _dependency.Id;
    public string State => OnUi(() => _dependency.Ready ? "Ready" : "Starting");
    public int ThreadId => OnUi(() => Environment.CurrentManagedThreadId);
    public int VisibleWindows => OnUi(NativeProbe.CountVisibleWindows);
    public void SetSimulatedUserOwnership(bool value) => _dispatcher.Invoke(() => { EnsureOpen(); _lifetime.SetUserOwned(value); });
    public void SetSimulatedCall(bool value) => _dispatcher.Invoke(() => { EnsureOpen(); _lifetime.SetCallActive(value); });
    public void Close() => Interlocked.Exchange(ref _lease, null)?.Dispose();
    public void Fail() => throw new COMException("Synthetic prototype failure.", unchecked((int)0x80070057));
    public int InterfaceSupportsErrorInfo(ref Guid riid) => riid == typeof(IPrototypeAutomation).GUID ? 0 : 1;
    private void EnsureOpen()
    { if (_lease is null) throw new COMException("Prototype session closed.", unchecked((int)0x80010108)); }
    private T OnUi<T>(Func<T> read)
    {
        T result = default!;
        _dispatcher.Invoke(() => result = read());
        return result;
    }
}

// Unmodified Quantarix path used by LxTranslate: parameterless construction and ComBase lifetime.
[ComVisible(true), ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IPrototypeAutomation))]
public sealed class LegacyAutomation : ComBase, IPrototypeAutomation
{
    public int ProcessId => Environment.ProcessId;
    public string DependencyId => "legacy";
    public string State => "Ready";
    public int ThreadId => Environment.CurrentManagedThreadId;
    public int VisibleWindows => NativeProbe.CountVisibleWindows();
    public void SetSimulatedUserOwnership(bool value) => throw new NotSupportedException();
    public void SetSimulatedCall(bool value) => throw new NotSupportedException();
    public void Close() { }
    public void Fail() => throw new COMException("Synthetic prototype failure.", unchecked((int)0x80070057));
}

using System.Runtime.InteropServices;
using Quantarix.Interop.COM;
using Quantarix.Interop.Win32;
using WawiCallPro.Automation.Contracts;

namespace WawiCallPro.Automation;

/// <summary>Publishes one injected class factory in the caller's existing COM apartment. It writes no registry entries.</summary>
public sealed class AutomationClassRegistration : IDisposable
{
    private readonly int _thread = Environment.CurrentManagedThreadId;
    private readonly AutomationClassFactory _factory;
    private readonly int _cookie;
    private bool _disposed;
    public AutomationClassRegistration(Guid clsid, IAutomationApplicationFactory factory, Action<bool> serverLock)
    {
        _factory = new(factory, serverLock);
        Marshal.ThrowExceptionForHR(Ole32.CoRegisterClassObject(ref clsid, _factory, Ole32.CLSCTX_LOCAL_SERVER,
            Ole32.REGCLS_MULTIPLEUSE | Ole32.REGCLS_SUSPENDED, out _cookie));
        try { Marshal.ThrowExceptionForHR(Ole32.CoResumeClassObjects()); }
        catch { Ole32.CoRevokeClassObject(_cookie); throw; }
    }
    public void Dispose()
    {
        if (_disposed) return;
        if (Environment.CurrentManagedThreadId != _thread) throw new InvalidOperationException("Revoke the class on its registering thread.");
        Marshal.ThrowExceptionForHR(Ole32.CoRevokeClassObject(_cookie));
        _disposed = true;
        GC.KeepAlive(_factory);
    }
}

[ComVisible(true), ClassInterface(ClassInterfaceType.None)]
internal sealed class AutomationClassFactory(IAutomationApplicationFactory factory, Action<bool> serverLock) : IClassFactory
{
    public void CreateInstance(object pUnkOuter, ref Guid riid, out IntPtr ppvObject)
    {
        ppvObject = IntPtr.Zero;
        if (pUnkOuter is not null) throw new COMException("Aggregation unsupported.", unchecked((int)0x80040110));
        if (riid != new Guid("00000000-0000-0000-C000-000000000046") && riid != new Guid("00020400-0000-0000-C000-000000000046")
            && riid != typeof(IApplicationV1).GUID) throw new COMException("Interface unsupported.", unchecked((int)0x80004002));
        var instance = factory.Create();
        var unknown = Marshal.GetIUnknownForObject(instance);
        try { Marshal.ThrowExceptionForHR(Marshal.QueryInterface(unknown, in riid, out ppvObject)); }
        finally { Marshal.Release(unknown); }
    }
    public void LockServer(bool fLock) => serverLock(fLock);
}

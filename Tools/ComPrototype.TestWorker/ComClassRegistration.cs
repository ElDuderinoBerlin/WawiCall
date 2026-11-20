using System.Runtime.InteropServices;
using Quantarix.Interop.COM;
using Quantarix.Interop.Win32;

namespace WawiCallPro.ComPrototype;

/// <summary>DI class activation using the shared COM interface and native wrappers, with an external message loop.</summary>
internal sealed class ComClassRegistration : IDisposable
{
    private readonly InjectedClassFactory _factory;
    private readonly int _cookie;
    private bool _disposed;
    internal ComClassRegistration(Guid clsid, Func<PrototypeAutomation> create, ServerLifetime lifetime)
    {
        _factory = new(create, lifetime);
        Marshal.ThrowExceptionForHR(Ole32.CoRegisterClassObject(ref clsid, _factory, Ole32.CLSCTX_LOCAL_SERVER,
            Ole32.REGCLS_MULTIPLEUSE | Ole32.REGCLS_SUSPENDED, out _cookie));
        try { Marshal.ThrowExceptionForHR(Ole32.CoResumeClassObjects()); }
        catch { Ole32.CoRevokeClassObject(_cookie); throw; }
    }
    public void Dispose()
    {
        if (_disposed) return;
        Marshal.ThrowExceptionForHR(Ole32.CoRevokeClassObject(_cookie));
        _disposed = true;
        GC.KeepAlive(_factory);
    }
}

[ComVisible(true), ClassInterface(ClassInterfaceType.None)]
internal sealed class InjectedClassFactory(Func<PrototypeAutomation> create, ServerLifetime lifetime) : IClassFactory
{
    public void CreateInstance(object pUnkOuter, ref Guid riid, out IntPtr ppvObject)
    {
        ppvObject = IntPtr.Zero;
        if (pUnkOuter is not null) throw new COMException("Aggregation unsupported.", unchecked((int)0x80040110));
        if (riid != new Guid("00000000-0000-0000-C000-000000000046")
            && riid != new Guid("00020400-0000-0000-C000-000000000046") && riid != typeof(IPrototypeAutomation).GUID)
            throw new COMException("Interface unsupported.", unchecked((int)0x80004002));
        var instance = create();
        var unknown = Marshal.GetIUnknownForObject(instance);
        try
        {
            var result = Marshal.QueryInterface(unknown, in riid, out ppvObject);
            if (result < 0) { instance.Close(); Marshal.ThrowExceptionForHR(result); }
        }
        finally { Marshal.Release(unknown); }
    }
    public void LockServer(bool fLock) => lifetime.LockServer(fLock);
}

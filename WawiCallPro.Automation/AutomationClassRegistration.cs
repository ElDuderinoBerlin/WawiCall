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
    /// <summary>Call at process entry before UI/RCW creation or any other interface marshalling.</summary>
    public static void PrepareProcess() => AutomationComOptions.EnableFastRundown();
    public AutomationClassRegistration(Guid clsid, IAutomationApplicationFactory factory, Action<bool> serverLock)
    {
        AutomationComOptions.EnableFastRundown();
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
    private readonly object _gate = new();
    private int _explicitLocks;
    public void CreateInstance(object pUnkOuter, ref Guid riid, out IntPtr ppvObject)
    {
        ppvObject = IntPtr.Zero;
        if (pUnkOuter is not null) throw new COMException("Aggregation unsupported.", unchecked((int)0x80040110));
        if (riid != new Guid("00000000-0000-0000-C000-000000000046") && riid != new Guid("00020400-0000-0000-C000-000000000046")
            && riid != typeof(IApplicationV1).GUID) throw new COMException("Interface unsupported.", unchecked((int)0x80004002));
        try
        {
            var instance = factory.Create();
            var unknown = Marshal.GetIUnknownForObject(instance);
            nint dispatch;
            var dispatchId = new Guid("00020400-0000-0000-C000-000000000046");
            try { Marshal.ThrowExceptionForHR(Marshal.QueryInterface(unknown, in dispatchId, out dispatch)); }
            finally { Marshal.Release(unknown); }
            ppvObject = AutomationDispatch.Export(dispatch, typeof(IApplicationV1).GUID, serverLock);
        }
        catch (AutomationFault fault) { throw new COMException(fault.Message, unchecked((int)0x8004A100) + (int)fault.Failure); }
        catch (Exception) { throw new COMException(AutomationFault.MessageFor(AutomationFailure.ProviderUnavailable), unchecked((int)0x8004A108)); }
    }
    public void LockServer(bool fLock)
    {
        lock (_gate)
        {
            if (!fLock && _explicitLocks == 0)
                throw new COMException(AutomationFault.MessageFor(AutomationFailure.Conflict), unchecked((int)0x8004A106));
            try { serverLock(fLock); }
            catch (AutomationFault fault) { throw new COMException(fault.Message, unchecked((int)0x8004A100) + (int)fault.Failure); }
            catch (Exception) { throw new COMException(AutomationFault.MessageFor(AutomationFailure.ProviderUnavailable), unchecked((int)0x8004A108)); }
            _explicitLocks += fLock ? 1 : -1;
        }
    }
}

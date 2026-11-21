using System.Runtime.InteropServices;

namespace WawiCallPro.Automation;

internal static unsafe class AutomationComOptions
{
    internal static void EnableFastRundown()
    {
        var initialized = CoInitializeEx(0, 2); // STA; keep an existing apartment's mode when already initialized.
        if (initialized < 0 && initialized != unchecked((int)0x80010106)) Marshal.ThrowExceptionForHR(initialized);
        try { Configure(); }
        finally { if (initialized >= 0) CoUninitialize(); }
    }
    private static void Configure()
    {
        // Preserve all other process options. Dead local clients must relinquish their stubs without
        // waiting for the normal COM ping timeout; object release still goes through native Release.
        // https://learn.microsoft.com/windows/win32/api/objidl/nn-objidl-iglobaloptions
        var clsid = new Guid("0000034B-0000-0000-C000-000000000046");
        var iid = new Guid("0000015B-0000-0000-C000-000000000046");
        // Avoid creating an RCW here: its own marshalling can initialize COM security before Set.
        Marshal.ThrowExceptionForHR(CoCreateInstance(in clsid, 0, 1, in iid, out var instance));
        try
        {
            nuint flags = 0;
            var table = *(nint**)instance;
            Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, uint, nuint*, int>)table[4])(instance, 4, &flags)); // COMGLB_RO_SETTINGS
            if ((flags & 0x8) == 0) // COMGLB_FAST_RUNDOWN (Windows SDK ObjIdlBase.h)
                Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, uint, nuint, int>)table[3])(instance, 4, flags | 0x8));
        }
        finally { Marshal.Release(instance); }
    }

    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern int CoCreateInstance(in Guid clsid, nint outer, uint context, in Guid iid, out nint instance);
    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern int CoInitializeEx(nint reserved, uint mode);
    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern void CoUninitialize();
}

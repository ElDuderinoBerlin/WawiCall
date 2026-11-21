using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using WawiCallPro.Automation.Contracts;

namespace WawiCallPro.Automation;

/// <summary>
/// Reference-counted boundary around the CLR's dispatch implementation. Each exported facade or snapshot
/// holds the host until its last native Release, independently of managed garbage collection.
/// Only the versioned dispatch contract and error information are exposed; no CLR or safety interfaces escape.
/// </summary>
internal static unsafe class AutomationDispatch
{
    private static readonly Guid Unknown = new("00000000-0000-0000-C000-000000000046");
    private static readonly Guid Dispatch = new("00020400-0000-0000-C000-000000000046");
    private static readonly Guid ErrorInfo = new("DF0B3D60-548F-101B-8E65-08002B2BD119");
    private static readonly nint DispatchTable = BuildDispatchTable();
    private static readonly nint ErrorTable = BuildErrorTable();
    private const int NoInterface = unchecked((int)0x80004002);
    private const int PointerError = unchecked((int)0x80004003);
    private const int ProviderError = unchecked((int)0x8004A108);

    // Takes ownership of dispatch even when construction fails.
    internal static nint Export(nint dispatch, Guid contract, Action<bool> serverLock)
    {
        nint memory = 0;
        GCHandle handle = default;
        var held = false;
        try
        {
            serverLock(true); held = true;
            memory = (nint)NativeMemory.AllocZeroed((nuint)(4 * sizeof(nint)));
            if (memory == 0) throw new OutOfMemoryException();
            var entry = new Entry(dispatch, contract, serverLock, memory);
            handle = GCHandle.Alloc(entry);
            var slots = (nint*)memory;
            slots[0] = DispatchTable; slots[1] = GCHandle.ToIntPtr(handle);
            slots[2] = ErrorTable; slots[3] = slots[1];
            return memory;
        }
        catch
        {
            if (handle.IsAllocated) handle.Free();
            NativeMemory.Free((void*)memory);
            Marshal.Release(dispatch);
            if (held) serverLock(false);
            throw;
        }
    }

    private sealed class Entry(nint inner, Guid contract, Action<bool> serverLock, nint memory)
    {
        internal readonly nint Inner = inner, Memory = memory;
        internal readonly Guid Contract = contract;
        internal readonly Action<bool> ServerLock = serverLock;
        internal int References = 1;
    }
    private static Entry Get(nint self) => (Entry)GCHandle.FromIntPtr(((nint*)self)[1]).Target!;
    private static nint* VTable(nint value) => *(nint**)value;
    private static nint AllocateTable(int count) => RuntimeHelpers.AllocateTypeAssociatedMemory(typeof(AutomationDispatch), count * sizeof(nint));
    private static nint BuildDispatchTable()
    {
        var table = (nint*)AllocateTable(7);
        table[0] = (nint)(delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int>)&QueryInterface;
        table[1] = (nint)(delegate* unmanaged[Stdcall]<nint, uint>)&AddRef;
        table[2] = (nint)(delegate* unmanaged[Stdcall]<nint, uint>)&Release;
        table[3] = (nint)(delegate* unmanaged[Stdcall]<nint, uint*, int>)&GetTypeInfoCount;
        table[4] = (nint)(delegate* unmanaged[Stdcall]<nint, uint, uint, nint*, int>)&GetTypeInfo;
        table[5] = (nint)(delegate* unmanaged[Stdcall]<nint, Guid*, nint, uint, uint, int*, int>)&GetNames;
        table[6] = (nint)(delegate* unmanaged[Stdcall]<nint, int, Guid*, uint, ushort, nint, nint, nint, uint*, int>)&Invoke;
        return (nint)table;
    }
    private static nint BuildErrorTable()
    {
        var table = (nint*)AllocateTable(4);
        table[0] = (nint)(delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int>)&QueryInterface;
        table[1] = (nint)(delegate* unmanaged[Stdcall]<nint, uint>)&AddRef;
        table[2] = (nint)(delegate* unmanaged[Stdcall]<nint, uint>)&Release;
        table[3] = (nint)(delegate* unmanaged[Stdcall]<nint, Guid*, int>)&SupportsErrorInfo;
        return (nint)table;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int QueryInterface(nint self, Guid* iid, nint* result)
    {
        if (result == null) return PointerError;
        *result = 0;
        if (iid == null) return PointerError;
        try
        {
            var entry = Get(self);
            if (*iid == Unknown || *iid == Dispatch || *iid == entry.Contract) *result = entry.Memory;
            else if (*iid == ErrorInfo) *result = entry.Memory + 2 * sizeof(nint);
            else return NoInterface;
            Interlocked.Increment(ref entry.References);
            return 0;
        }
        catch { return ProviderError; }
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static uint AddRef(nint self)
    {
        try { return (uint)Interlocked.Increment(ref Get(self).References); }
        catch { return 0; }
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static uint Release(nint self)
    {
        try
        {
            var handle = GCHandle.FromIntPtr(((nint*)self)[1]);
            var entry = (Entry)handle.Target!;
            var remaining = Interlocked.Decrement(ref entry.References);
            if (remaining != 0) return (uint)remaining;
            try { Marshal.Release(entry.Inner); }
            finally
            {
                try { entry.ServerLock(false); }
                finally { handle.Free(); NativeMemory.Free((void*)entry.Memory); }
            }
        }
        catch { /* Never unwind a managed exception into native COM cleanup. */ }
        return 0;
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int SupportsErrorInfo(nint self, Guid* iid)
    {
        if (iid == null) return PointerError;
        try { return *iid == Get(self).Contract ? 0 : 1; }
        catch { return ProviderError; }
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int GetTypeInfoCount(nint self, uint* count)
    {
        try { var inner = Get(self).Inner; return ((delegate* unmanaged[Stdcall]<nint, uint*, int>)VTable(inner)[3])(inner, count); }
        catch { return ProviderError; }
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int GetTypeInfo(nint self, uint index, uint locale, nint* info)
    {
        try { var inner = Get(self).Inner; return ((delegate* unmanaged[Stdcall]<nint, uint, uint, nint*, int>)VTable(inner)[4])(inner, index, locale, info); }
        catch { return ProviderError; }
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int GetNames(nint self, Guid* iid, nint names, uint count, uint locale, int* ids)
    {
        try { var inner = Get(self).Inner; return ((delegate* unmanaged[Stdcall]<nint, Guid*, nint, uint, uint, int*, int>)VTable(inner)[5])(inner, iid, names, count, locale, ids); }
        catch { return ProviderError; }
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int Invoke(nint self, int id, Guid* iid, uint locale, ushort flags, nint args, nint result, nint error, uint* argumentError)
    {
        try
        {
            var entry = Get(self);
            var hr = ((delegate* unmanaged[Stdcall]<nint, int, Guid*, uint, ushort, nint, nint, nint, uint*, int>)VTable(entry.Inner)[6])
                (entry.Inner, id, iid, locale, flags, args, result, error, argumentError);
            if (hr < 0 || result == 0 || *(ushort*)result != 9) return hr; // VT_DISPATCH
            var value = (nint*)((byte*)result + 8);
            if (*value == 0) return hr;
            var child = *value;
            *value = 0; *(ushort*)result = 0;
            // The CCW returns detached public snapshots. Never export their original IUnknown identity.
            Guid contract;
            try { contract = SnapshotContract(child); }
            catch { Marshal.Release(child); throw; }
            *value = Export(child, contract, entry.ServerLock);
            *(ushort*)result = 9;
            return hr;
        }
        catch (AutomationFault fault) { return unchecked((int)0x8004A100) + (int)fault.Failure; }
        catch { return ProviderError; }
    }
    private static Guid SnapshotContract(nint child)
    {
        foreach (var contract in new[] { typeof(IStatusV1).GUID, typeof(IOperationV1).GUID, typeof(ICallV1).GUID })
        {
            if (Marshal.QueryInterface(child, in contract, out var pointer) < 0) continue;
            Marshal.Release(pointer); return contract;
        }
        throw new AutomationFault(AutomationFailure.ProviderUnavailable);
    }
}

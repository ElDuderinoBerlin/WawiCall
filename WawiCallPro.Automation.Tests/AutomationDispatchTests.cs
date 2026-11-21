using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using WawiCallPro.Automation.Contracts;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Parties;

namespace WawiCallPro.Automation.Tests;

[TestClass]
public sealed class AutomationDispatchTests
{
    [TestMethod, DataRow(1), DataRow(2)]
    public async Task EverySnapshotOwnsANativeLeaseAndOnlyExposesItsVersionedDispatchContract(int product)
    {
        var source = new Source((LexwareProductLine)product);
        var services = new ServiceCollection();
        services.AddSingleton<IAutomationEnvironment>(source); services.AddSingleton<IAutomationTelephony>(source);
        services.AddSingleton<IAutomationPresentation>(source); services.AddSingleton<IPartySearch>(source);
        services.AddWawiAutomation();
        await using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IAutomationApplicationFactory>();
        var app = factory.Create();
        var operationId = app.ConnectTelephony();
        var leases = 0;
        var pointers = new List<nint>();
        var boundary = new AutomationClassFactory(new FixedFactory(app), acquire => Interlocked.Add(ref leases, acquire ? 1 : -1));
        var iid = typeof(IApplicationV1).GUID;
        boundary.CreateInstance(null!, ref iid, out var root); pointers.Add(root);
        try
        {
            Assert.AreEqual(1, leases);
            Assert.AreEqual(unchecked((int)0x8004A106), Assert.Throws<COMException>(() => boundary.LockServer(false)).HResult);
            Assert.AreEqual(1, leases, "An unbalanced factory unlock must never remove a facade's native lease.");
            boundary.LockServer(true); Assert.AreEqual(2, leases);
            boundary.LockServer(false); Assert.AreEqual(1, leases);
            var status = InvokeObject(root, 1); pointers.Add(status);
            var operation = InvokeObject(root, 9, operationId); pointers.Add(operation);
            var call = InvokeObject(root, 10, Guid.NewGuid().ToString("D")); pointers.Add(call);
            Assert.AreEqual(4, leases);
            foreach (var (pointer, contract) in new[] { (root, typeof(IApplicationV1)), (status, typeof(IStatusV1)), (operation, typeof(IOperationV1)), (call, typeof(ICallV1)) })
            {
                var expected = contract.GUID;
                Assert.AreEqual(0, Marshal.QueryInterface(pointer, in expected, out var same));
                Assert.AreEqual(pointer, same); Marshal.Release(same);
                foreach (var unsupported in new[] { new Guid("CB5BDC81-93C1-11CF-8F20-00805F2CD064"), new Guid("65074F7F-63C0-304E-AF0A-D51741CB4A8D"), Guid.NewGuid() })
                {
                    Assert.AreEqual(unchecked((int)0x80004002), Marshal.QueryInterface(pointer, in unsupported, out var rejected));
                    Assert.AreEqual(nint.Zero, rejected);
                }
                var errorInfo = new Guid("DF0B3D60-548F-101B-8E65-08002B2BD119");
                Assert.AreEqual(0, Marshal.QueryInterface(pointer, in errorInfo, out var support));
                var unknown = new Guid("00000000-0000-0000-C000-000000000046");
                Assert.AreEqual(0, Marshal.QueryInterface(support, in unknown, out var identity));
                Assert.AreEqual(pointer, identity); Marshal.Release(identity); Marshal.Release(support);
            }
            Marshal.Release(root); pointers.Remove(root);
            Assert.AreEqual(3, leases, "Parent release must leave independent child leases without a server GC.");
            Assert.AreEqual("Ready", InvokeString(status, 3));
            Assert.IsTrue(new[] { "Pending", "Running", "Succeeded" }.Contains(InvokeString(operation, 2)));
            Assert.AreEqual("Dialing", InvokeString(call, 2));
        }
        finally { foreach (var pointer in pointers) Marshal.Release(pointer); }
        Assert.AreEqual(0, leases, "The last native Release must synchronously relinquish every host lease.");
    }

    [TestMethod]
    public void FactoryRejectsUnsupportedInterfacesAndRedactsConstructionFailures()
    {
        var factory = new AutomationClassFactory(new BrokenFactory(), _ => { });
        foreach (var requested in new[] { typeof(IApplicationV1).GUID, new Guid("00020400-0000-0000-C000-000000000046") })
        {
            var iid = requested;
            var error = Assert.Throws<COMException>(() => factory.CreateInstance(null!, ref iid, out _));
            Assert.AreEqual(unchecked((int)0x8004A108), error.HResult);
            Assert.DoesNotContain("private", error.Message);
        }
        var unknown = Guid.NewGuid();
        Assert.AreEqual(unchecked((int)0x80004002), Assert.Throws<COMException>(() => factory.CreateInstance(null!, ref unknown, out _)).HResult);
    }

    private static unsafe nint InvokeObject(nint pointer, int member, string? argument = null)
    {
        var result = stackalloc byte[24]; new Span<byte>(result, 24).Clear();
        Invoke(pointer, member, argument, 1, (nint)result);
        Assert.AreEqual((ushort)9, *(ushort*)result);
        return *(nint*)(result + 8);
    }
    private static unsafe string InvokeString(nint pointer, int member)
    {
        var result = stackalloc byte[24]; new Span<byte>(result, 24).Clear();
        Invoke(pointer, member, null, 2, (nint)result);
        Assert.AreEqual((ushort)8, *(ushort*)result);
        var bstr = *(nint*)(result + 8);
        try { return Marshal.PtrToStringBSTR(bstr); }
        finally { Marshal.FreeBSTR(bstr); }
    }
    private static unsafe void Invoke(nint pointer, int member, string? argument, ushort flags, nint result)
    {
        var value = stackalloc byte[24]; new Span<byte>(value, 24).Clear();
        var bstr = argument is null ? 0 : Marshal.StringToBSTR(argument);
        if (argument is not null) { *(ushort*)value = 8; *(nint*)(value + 8) = bstr; }
        var args = new Parameters { Values = argument is null ? 0 : (nint)value, Count = argument is null ? 0u : 1u };
        var iid = Guid.Empty;
        try
        {
            var hr = ((delegate* unmanaged[Stdcall]<nint, int, Guid*, uint, ushort, nint, nint, nint, uint*, int>)(*(nint**)pointer)[6])
                (pointer, member, &iid, 0, flags, (nint)(&args), result, 0, null);
            Marshal.ThrowExceptionForHR(hr);
        }
        finally { if (bstr != 0) Marshal.FreeBSTR(bstr); }
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct Parameters { internal nint Values, Named; internal uint Count, NamedCount; }
    private sealed class FixedFactory(AutomationApplication app) : IAutomationApplicationFactory { public AutomationApplication Create() => app; }
    private sealed class BrokenFactory : IAutomationApplicationFactory { public AutomationApplication Create() => throw new InvalidOperationException("private-path-secret"); }
    private sealed class Source(LexwareProductLine product) : IAutomationEnvironment, IAutomationTelephony, IAutomationPresentation, IPartySearch
    {
        public AutomationStatus GetStatus() => new(product, "Ready", false, "Registered");
        public ImmutableArray<Company> GetCompanies() => [];
        public ImmutableArray<AutomationView> GetViews() => [];
        public void RequireAccess(AutomationAccess access, CompanyKey? company = null) { }
        public Task ConnectAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<Guid> DialAsync(CompanyKey company, string destination, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task HangUpAsync(Guid callId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public AutomationCall GetCall(Guid callId) => new(callId, "Dialing");
        public Task ShowAsync(PresentationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PartySearchResult> SearchAsync(PartySearchQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}

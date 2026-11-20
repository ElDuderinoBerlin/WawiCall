using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using WawiCallPro.Automation.Contracts;

namespace WawiCallPro.Automation.Tests;

[TestClass]
public sealed class TypeLibraryTests
{
    [DllImport("oleaut32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void LoadTypeLibEx(string file, int registrationKind, out ITypeLib library);

    [TestMethod, DataRow("x86"), DataRow("x64")]
    public void CompiledTypeLibrariesMatchThePublishedDispatchContractWithoutRegistration(string architecture)
    {
        var path = Path.Combine(AppContext.BaseDirectory, architecture, "WawiCallPro.Automation.v1.tlb");
        Assert.IsTrue(File.Exists(path), path);
        LoadTypeLibEx(path, 2, out var library); // REGKIND_NONE: never install a type library, even per user.
        try
        {
            library.GetLibAttr(out var pointer);
            try
            {
                var attribute = Marshal.PtrToStructure<TYPELIBATTR>(pointer);
                Assert.AreEqual(new Guid("7155BD3A-6580-4A46-A034-1E4F55CFA047"), attribute.guid);
                Assert.AreEqual((short)1, attribute.wMajorVerNum); Assert.AreEqual((short)0, attribute.wMinorVerNum);
                Assert.AreEqual(architecture == "x64" ? SYSKIND.SYS_WIN64 : SYSKIND.SYS_WIN32, attribute.syskind);
            }
            finally { library.ReleaseTLibAttr(pointer); }
            foreach (var contract in new[] { typeof(IApplicationV1), typeof(IStatusV1), typeof(IOperationV1), typeof(ICallV1) })
                AssertInterface(library, contract);
            foreach (var type in new[] { typeof(ProApplication), typeof(FinOfficeApplication) })
            {
                var id = type.GUID; library.GetTypeInfoOfGuid(ref id, out var info);
                try
                {
                    info.GetTypeAttr(out var attr);
                    try { Assert.AreEqual(TYPEKIND.TKIND_COCLASS, Marshal.PtrToStructure<TYPEATTR>(attr).typekind); }
                    finally { info.ReleaseTypeAttr(attr); }
                    info.GetRefTypeOfImplType(0, out var reference); info.GetRefTypeInfo(reference, out var implemented);
                    try
                    {
                        implemented.GetTypeAttr(out var implementedPointer);
                        try { Assert.AreEqual(typeof(IApplicationV1).GUID, Marshal.PtrToStructure<TYPEATTR>(implementedPointer).guid); }
                        finally { implemented.ReleaseTypeAttr(implementedPointer); }
                    }
                    finally { Marshal.FinalReleaseComObject(implemented); }
                }
                finally { Marshal.FinalReleaseComObject(info); }
            }
        }
        finally { Marshal.FinalReleaseComObject(library); }
    }

    [TestMethod]
    public void VersionOneNamesDispatchIdsAndComSurfaceAreFrozen()
    {
        Assert.AreEqual("710E28F8-6286-4F6E-8898-C88A3F872FC1", typeof(IApplicationV1).GUID.ToString().ToUpperInvariant());
        var expected = new[] { "GetStatus", "Show", "ShowView", "ShowDial", "ShowCustomer", "ShowSupplier", "ConnectTelephony", "Dial", "GetOperation", "GetCall", "HangUp", "GetCompaniesJson", "GetViewsJson" };
        Assert.AreEqual(expected.Length, typeof(IApplicationV1).GetMethods().Length);
        for (var i = 0; i < expected.Length; i++) Assert.AreEqual(i + 1, typeof(IApplicationV1).GetMethod(expected[i])!.GetCustomAttribute<DispIdAttribute>()!.Value);
        Assert.AreEqual(AutomationIds.ProProgId, typeof(ProApplication).GetCustomAttribute<ProgIdAttribute>()!.Value);
        Assert.AreEqual(AutomationIds.FinOfficeProgId, typeof(FinOfficeApplication).GetCustomAttribute<ProgIdAttribute>()!.Value);
        Assert.AreNotEqual(typeof(ProApplication).GUID, typeof(FinOfficeApplication).GUID);
        foreach (var type in new[] { typeof(ProApplication), typeof(FinOfficeApplication), typeof(StatusSnapshot), typeof(OperationSnapshot), typeof(CallSnapshot) })
            Assert.AreEqual(ClassInterfaceType.None, type.GetCustomAttribute<ClassInterfaceAttribute>()!.Value);
        foreach (var type in new[] { typeof(IStatusV1), typeof(IOperationV1), typeof(ICallV1) })
            Assert.IsTrue(type.GetProperties().All(property => !property.CanWrite));
    }

    private static void AssertInterface(ITypeLib library, Type contract)
    {
        var guid = contract.GUID; library.GetTypeInfoOfGuid(ref guid, out var info);
        try
        {
            info.GetTypeAttr(out var pointer);
            int count;
            try
            {
                var attr = Marshal.PtrToStructure<TYPEATTR>(pointer); count = attr.cFuncs;
                Assert.AreEqual(TYPEKIND.TKIND_DISPATCH, attr.typekind);
            }
            finally { info.ReleaseTypeAttr(pointer); }
            var members = contract.GetMethods().Where(method => !method.IsSpecialName).Select(method => (Method: method, Id: method.GetCustomAttribute<DispIdAttribute>()!.Value))
                .Concat(contract.GetProperties().Select(property => (Method: property.GetMethod!, Id: property.GetCustomAttribute<DispIdAttribute>()!.Value))).ToArray();
            Assert.AreEqual(members.Length, count);
            for (var index = 0; index < count; index++)
            {
                info.GetFuncDesc(index, out var descPointer);
                try
                {
                    var desc = Marshal.PtrToStructure<FUNCDESC>(descPointer);
                    var member = members.Single(item => item.Id == desc.memid).Method;
                    Assert.AreEqual(member.GetParameters().Length, (int)desc.cParams);
                    var expected = member.ReturnType == typeof(string) ? VarEnum.VT_BSTR : member.ReturnType == typeof(bool) ? VarEnum.VT_BOOL : VarEnum.VT_DISPATCH;
                    Assert.AreEqual((short)expected, desc.elemdescFunc.tdesc.vt, member.Name);
                    var names = new string[10]; info.GetNames(desc.memid, names, names.Length, out _);
                    Assert.AreEqual(member.Name.Replace("get_", "", StringComparison.Ordinal), names[0]);
                    for (var parameter = 0; parameter < desc.cParams; parameter++)
                    {
                        var argument = Marshal.PtrToStructure<ELEMDESC>(IntPtr.Add(desc.lprgelemdescParam, parameter * Marshal.SizeOf<ELEMDESC>()));
                        Assert.AreEqual((short)VarEnum.VT_BSTR, argument.tdesc.vt);
                    }
                }
                finally { info.ReleaseFuncDesc(descPointer); }
            }
        }
        finally { Marshal.FinalReleaseComObject(info); }
    }
}

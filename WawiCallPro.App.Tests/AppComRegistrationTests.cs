using Microsoft.Win32;
using System.IO;
using WawiCallPro.App.Hosting;

namespace WawiCallPro.App.Tests;

[TestClass]
public sealed class AppComRegistrationTests
{
    [TestMethod]
    public void RegisterAndRemoveAreIdempotentAndTouchOnlyOwnClassAndProgIdInBothViews()
    {
        var store = new RegistryDouble(); var service = new UserComRegistration(store); var identity = Identity("1.0.0");
        service.Register(identity); var before = store.Snapshot(); service.Register(identity);
        Assert.AreEqual(before, store.Snapshot()); Assert.AreEqual(10, store.Nodes.Count);
        foreach (var ((view, key), values) in store.Nodes)
        {
            Assert.IsTrue(view is RegistryView.Registry32 or RegistryView.Registry64);
            Assert.IsTrue(key.StartsWith(identity.ClassKey, StringComparison.Ordinal) || key.StartsWith(identity.ProgId, StringComparison.Ordinal));
            Assert.IsFalse(key.Contains("TypeLib", StringComparison.Ordinal) || key.Contains("Interface", StringComparison.Ordinal));
            if (key.EndsWith("LocalServer32", StringComparison.Ordinal))
            { Assert.AreEqual("\"" + identity.Executable + "\"", values[""]); Assert.AreEqual(identity.Executable, values["ServerExecutable"]); }
        }
        service.Unregister(identity); service.Unregister(identity); Assert.AreEqual(0, store.Nodes.Count);
    }
    [TestMethod]
    public void OlderRegistrationAndUninstallCannotReplaceOrDeleteTheNewOwner()
    {
        var store = new RegistryDouble(); var service = new UserComRegistration(store); var old = Identity("1.0.0");
        service.Register(old);
        var next = old with { Executable = @"D:\Isolated new install\WawiCallPro.App.Pro.exe", InstallationRoot = @"D:\Isolated new install", Version = "2.0.0" };
        service.Register(next); var newer = store.Snapshot();
        Assert.Throws<InvalidOperationException>(() => service.Register(old)); service.Unregister(old);
        Assert.AreEqual(newer, store.Snapshot());
        Assert.Throws<InvalidOperationException>(() => service.Register(old with { Version = "2.0.0" }));
        Assert.AreEqual(newer, store.Snapshot());
        service.Unregister(next); Assert.AreEqual(0, store.Nodes.Count);
    }
    [TestMethod]
    public void InstalledRollbackRequiresTheSameInstallationAndDoesNotAuthorizeAnOldForeignInstall()
    {
        var store = new RegistryDouble(); var service = new UserComRegistration(store); var v1 = Identity("1.0.0");
        var v2 = v1 with { Version = "2.0.0" }; service.Register(v2);
        Assert.Throws<InvalidOperationException>(() => service.Register(v1));
        Assert.Throws<InvalidOperationException>(() => service.Register(v1 with { InstallationRoot = @"D:\other" }, true));
        service.Register(v1, true); service.Unregister(v2);
        Assert.AreEqual("1.0.0", store.Nodes[(RegistryView.Registry64, v1.ClassKey)]["WawiCallPro.Version"]);
        service.Unregister(v1); Assert.AreEqual(0, store.Nodes.Count);
    }
    [TestMethod]
    public void ForeignRegistrationAndChangedLeafValuesSurviveUninstall()
    {
        var store = new RegistryDouble(); var service = new UserComRegistration(store); var identity = Identity("1.0.0");
        store.Write(RegistryView.Registry32, identity.ClassKey, new Dictionary<string, string> { [""] = "Foreign owner" });
        var before = store.Snapshot(); Assert.Throws<InvalidOperationException>(() => service.Register(identity));
        service.Unregister(identity); Assert.AreEqual(before, store.Snapshot());
        store.Nodes.Clear(); service.Register(identity);
        store.Nodes[(RegistryView.Registry64, identity.ClassKey + @"\LocalServer32")][""] = "Foreign command";
        store.Nodes[(RegistryView.Registry64, identity.ClassKey)]["ExternalValue"] = "Preserve";
        service.Unregister(identity);
        Assert.AreEqual("Foreign command", store.Nodes[(RegistryView.Registry64, identity.ClassKey + @"\LocalServer32")][""]);
        Assert.AreEqual("Preserve", store.Nodes[(RegistryView.Registry64, identity.ClassKey)]["ExternalValue"]);
    }
    [TestMethod, DataRow(1), DataRow(4), DataRow(8), DataRow(10)]
    public void PartialWriteFailureRestoresEveryChangedNode(int failureAt)
    {
        var store = new RegistryDouble { FailAt = failureAt }; var service = new UserComRegistration(store); var identity = Identity("1.0.0");
        Assert.Throws<IOException>(() => service.Register(identity)); Assert.AreEqual(0, store.Nodes.Count);
        service.Register(identity); var before = store.Snapshot();
        store.FailAt = store.Writes + failureAt;
        Assert.Throws<IOException>(() => service.Register(identity with { Version = "2.0.0" }));
        Assert.AreEqual(before, store.Snapshot());
    }
    [TestMethod, DataRow("1.0.0-alpha.2", "1.0.0-alpha.10", -1), DataRow("1.0.0-rc.1", "1.0.0", -1),
        DataRow("1.0.0+build1", "1.0.0+build2", 0), DataRow("2.0.0", "1.99.99", 1)]
    public void OwnershipUsesSemanticVersions(string a, string b, int sign) => Assert.AreEqual(sign, Math.Sign(ComRegistrationVersion.Compare(a, b)));

    private static ComRegistrationIdentity Identity(string version)
    { var id = Guid.NewGuid(); return new(id, "WCP.p" + id.ToString("N"), "Pro", @"D:\Isolated install\WawiCallPro.App.Pro.exe", version, @"D:\Isolated install"); }
    private sealed class RegistryDouble : IUserComRegistry
    {
        internal readonly Dictionary<(RegistryView View, string Key), Dictionary<string, string>> Nodes = [];
        internal int Writes, FailAt;
        public Dictionary<string, string>? Read(RegistryView view, string key) => Nodes.TryGetValue((view, key), out var value) ? new(value) : null;
        public void Write(RegistryView view, string key, IReadOnlyDictionary<string, string> values)
        { Nodes[(view, key)] = new(values); if (++Writes == FailAt) throw new IOException("Synthetic registry failure."); }
        public void Restore(RegistryView view, string key, IReadOnlyDictionary<string, string>? values)
        { if (values is null || values.Count == 0) Nodes.Remove((view, key)); else Nodes[(view, key)] = new(values); }
        internal string Snapshot() => string.Join("\n", Nodes.OrderBy(item => item.Key.View).ThenBy(item => item.Key.Key)
            .SelectMany(item => item.Value.OrderBy(value => value.Key).Select(value => item.Key + "/" + value.Key + "=" + value.Value)));
    }
}

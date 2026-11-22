using System.Text.Json;
using WawiCallPro.App.Hosting;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.App.Tests;

[TestClass]
public sealed class ClientKeyExitTests
{
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void ExitOnlyEncryptsAfterConsentAndNeverChangesOtherSettings(bool consent)
    {
        var directory = Path.Combine(Path.GetTempPath(), "Wcp-KeyExit-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        try
        {
            var key = Guid.NewGuid().ToString("B"); var file = Path.Combine(directory, "appsettings.json");
            File.WriteAllText(file, JsonSerializer.Serialize(new { LexwareApi = new { ClientKey = key }, Other = 42 }));
            var settings = new ProductAppSettings(file); var prompt = new Prompt { Consent = consent };
            var protection = new ClientKeyExitProtection(settings, prompt);
            Assert.IsTrue(protection.TryComplete(new Owner())); Assert.AreEqual(1, prompt.Calls);
            Assert.AreEqual(!consent, settings.HasPlaintextClientKey()); Assert.AreEqual(key, settings.ReadClientKey());
            Assert.AreEqual(42, settings.Read()["Other"]!.GetValue<int>());
            if (consent) { Assert.IsTrue(protection.TryComplete(new Owner())); Assert.AreEqual(1, prompt.Calls); }
            Assert.AreEqual(0, prompt.Failures);
        }
        finally { Directory.Delete(directory, true); }
    }
    [TestMethod]
    public void FailedEncryptionKeepsApplicationOpenAndPreservesThePlaintextFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Wcp-KeyLocked-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        try
        {
            var file = Path.Combine(directory, "appsettings.json");
            File.WriteAllText(file, JsonSerializer.Serialize(new { LexwareApi = new { ClientKey = Guid.NewGuid().ToString("B") } }));
            var before = File.ReadAllBytes(file); var prompt = new Prompt { Consent = true };
            using var fileLock = new FileStream(file + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            Assert.IsFalse(new ClientKeyExitProtection(new(file), prompt).TryComplete(new Owner()));
            Assert.AreEqual(1, prompt.Failures); CollectionAssert.AreEqual(before, File.ReadAllBytes(file));
        }
        finally { Directory.Delete(directory, true); }
    }
    [TestMethod]
    public void SecureSettingsCommandRunsWithoutWindowsAndHandlesErrorsWithFixedMessages()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Wcp-KeyCli-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        try
        {
            var key = Guid.NewGuid().ToString("B"); var file = Path.Combine(directory, "appsettings.json");
            File.WriteAllText(file, JsonSerializer.Serialize(new { LexwareApi = new { ClientKey = key } }));
            var windows = Application.OpenForms.Count;
            Assert.AreEqual(0, AppSettingsCommand.TryRun(["--secure-settings", file]));
            Assert.AreEqual(0, AppSettingsCommand.TryRun(["--secure-settings", file]));
            Assert.IsFalse(new ProductAppSettings(file).HasPlaintextClientKey());
            Assert.AreEqual(2, AppSettingsCommand.TryRun(["--secure-settings", "relative.json"]));
            Assert.AreEqual(1, AppSettingsCommand.TryRun(["--secure-settings", Path.Combine(directory, "missing.json")]));
            Assert.IsNull(AppSettingsCommand.TryRun([]));
            Assert.AreEqual(windows, Application.OpenForms.Count);
        }
        finally { Directory.Delete(directory, true); }
    }
    private sealed class Owner : IWin32Window { public IntPtr Handle => IntPtr.Zero; }
    private sealed class Prompt : IClientKeyExitPrompt
    {
        internal bool Consent; internal int Calls, Failures;
        public bool Confirm(IWin32Window owner) { Calls++; return Consent; }
        public void ReportFailure(IWin32Window owner) => Failures++;
    }
}

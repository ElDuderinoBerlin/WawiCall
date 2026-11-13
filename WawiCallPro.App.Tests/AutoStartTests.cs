using Microsoft.Win32;
using WawiCallPro.App.Hosting;
using WawiCallPro.Utilities.Diagnostics;

namespace WawiCallPro.App.Tests;

[TestClass]
public sealed class AutoStartTests
{
    [TestMethod]
    public void CommandsQuoteTrustedPathsAndSelectTheInstalledVelopackLauncher()
    {
        var assembly = @"D:\Fixture App\WawiCallPro.App.FinOffice.dll";
        var native = AppStartupCommand.Create(@"D:\Fixture App\WawiCallPro.App.FinOffice.exe", assembly);
        Assert.AreEqual("\"D:\\Fixture App\\WawiCallPro.App.FinOffice.exe\"", native.Command);
        var hosted = AppStartupCommand.Create(@"C:\Program Files\dotnet\dotnet.exe", assembly);
        Assert.AreEqual("\"C:\\Program Files\\dotnet\\dotnet.exe\" \"D:\\Fixture App\\WawiCallPro.App.FinOffice.dll\"", hosted.Command);
        var installed = AppStartupCommand.Create(@"D:\App\current\WawiCallPro.App.FinOffice.exe", assembly, @"D:\App\Update.exe");
        Assert.AreEqual("\"D:\\App\\Update.exe\" start \"WawiCallPro.App.FinOffice.exe\"", installed.Command);
        var demo = AppStartupCommand.Create(@"D:\App\WawiCallPro.App.Pro.Demo.exe", @"D:\App\WawiCallPro.App.Pro.Demo.dll");
        Assert.AreNotEqual(native.ValueName, demo.ValueName);
        Assert.ThrowsExactly<AutoStartException>(() => AppStartupCommand.Create("relative.exe", assembly));
        Assert.ThrowsExactly<AutoStartException>(() => AppStartupCommand.Create("D:\\bad\"path\\WawiCallPro.App.FinOffice.exe", assembly));
        Assert.ThrowsExactly<AutoStartException>(() => AppStartupCommand.Create(@"D:\Other.exe", assembly));
        Assert.ThrowsExactly<AutoStartException>(() => AppStartupCommand.Create(@"D:\dotnet.exe", "D:\\" + new string('a', 270) + "\\WawiCallPro.App.Pro.dll"));
    }

    [TestMethod]
    public void RealRegistryRoundtripIsEditionBoundAndRejectsExternalChangesWithoutTouchingForeignValues()
    {
        var keyPath = @"Software\WawiCallPro\Tests\AutoStart-" + Guid.NewGuid().ToString("N");
        try
        {
            var registry = new AutoStartRegistry(keyPath);
            var proCommand = AppStartupCommand.Create(@"D:\Fixture\WawiCallPro.App.Pro.exe", @"D:\Fixture\WawiCallPro.App.Pro.dll");
            var finCommand = AppStartupCommand.Create(@"D:\Fixture\WawiCallPro.App.FinOffice.exe", @"D:\Fixture\WawiCallPro.App.FinOffice.dll");
            var pro = new WindowsAppAutoStart(registry, proCommand, new()); var fin = new WindowsAppAutoStart(registry, finCommand, new());
            var initial = pro.Read(); Assert.IsFalse(initial.Enabled); Assert.IsTrue(initial.CanChange);
            Assert.IsTrue(pro.Set(true, initial).Enabled); Assert.IsFalse(fin.Read().Enabled);
            fin.Set(true, fin.Read()); var beforeExternalChange = pro.Read();
            registry.Write(proCommand.ValueName, "foreign-command-sentinel");
            Assert.ThrowsExactly<AutoStartException>(() => pro.Set(false, beforeExternalChange));
            Assert.AreEqual("foreign-command-sentinel", registry.Read(proCommand.ValueName)); Assert.IsTrue(fin.Read().Enabled);
            Assert.IsFalse(pro.Read().CanChange); Assert.DoesNotContain("sentinel", pro.Read().Status);
            registry.Write(proCommand.ValueName, proCommand.Command);
            Assert.IsFalse(pro.Set(false, pro.Read()).Enabled); Assert.IsTrue(fin.Read().Enabled);
            fin.Set(false, fin.Read()); Assert.IsNull(registry.Read(finCommand.ValueName));
            using var key = Registry.CurrentUser.OpenSubKey(keyPath, true); key!.SetValue(proCommand.ValueName, 42, RegistryValueKind.DWord);
            Assert.IsFalse(pro.Read().CanChange);
        }
        finally
        {
            Assert.IsTrue(keyPath.StartsWith(@"Software\WawiCallPro\Tests\AutoStart-", StringComparison.Ordinal));
            Registry.CurrentUser.DeleteSubKeyTree(keyPath, false);
        }
    }

    [TestMethod]
    public void DiagnosticsAndAuditsNeverReadOrWriteTheRealStartupRegistry()
    {
        var service = new WindowsAppAutoStart(new ForbiddenRegistry(), new("WawiCallPro.App.Pro", "trusted-command"), new(true));
        Assert.IsFalse(service.Read().CanChange);
        Assert.ThrowsExactly<AutoStartException>(() => service.Set(true, new(false, true, "")));
        var unavailable = new WindowsAppAutoStart(new ForbiddenRegistry(), new("WawiCallPro.App.Pro", "") { UnavailableReason = "Startpfad nicht verfügbar." }, new());
        Assert.IsFalse(unavailable.Read().CanChange);
        Assert.ThrowsExactly<AutoStartException>(() => unavailable.Set(true, new(false, true, "")));
    }
    private sealed class ForbiddenRegistry : IAutoStartRegistry
    {
        public string? Read(string name) => throw new AssertFailedException("Registry access is forbidden in diagnostics.");
        public void Write(string name, string command) => throw new AssertFailedException("Registry access is forbidden in diagnostics.");
        public void Delete(string name) => throw new AssertFailedException("Registry access is forbidden in diagnostics.");
    }
}

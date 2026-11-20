using System.Text.Json;
using WawiCallPro.App.Hosting;
using WawiCallPro.Automation.Contracts;
using WawiCallPro.Utilities.Hosting;

namespace WawiCallPro.App.Tests;

[TestClass, DoNotParallelize]
public sealed class AppComIdentityTests
{
    [TestMethod]
    public void OverriddenProfileCannotPublishTheProductClass()
    {
        var saved = Environment.GetEnvironmentVariable("WAWICALLPRO_CONFIG_DIR");
        try
        {
            Environment.SetEnvironmentVariable("WAWICALLPRO_CONFIG_DIR", Path.GetTempPath());
            var application = ApplicationDescriptor.FromAssembly(typeof(Program).Assembly);
            Assert.IsFalse(AppComIdentity.Resolve(new(AppStartKind.Interactive), application).Enabled);
            Assert.Throws<ArgumentException>(() => AppComIdentity.Resolve(new(AppStartKind.Com), application));
        }
        finally { Environment.SetEnvironmentVariable("WAWICALLPRO_CONFIG_DIR", saved); }
    }

    [TestMethod]
    public void IsolatedFixtureRequiresARandomMatchingClassAndProgId()
    {
        var saved = Environment.GetEnvironmentVariable("WAWICALLPRO_CONFIG_DIR");
        var directory = Path.Combine(Path.GetTempPath(), "WawiCallPro-com-identity-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, "com-fixture.json");
        try
        {
            var start = new AppStartArguments(AppStartKind.Com) { FixtureDirectory = directory };
            var application = ApplicationDescriptor.FromAssembly(typeof(Program).Assembly);
            foreach (var id in new[] { Guid.Empty, new Guid(AutomationIds.ProClass), new Guid(AutomationIds.FinOfficeClass) })
            {
                File.WriteAllText(file, JsonSerializer.Serialize(new { ClassId = id, ProgId = "WCP.p" + id.ToString("N") }));
                Assert.Throws<ArgumentException>(() => AppComIdentity.Resolve(start, application));
            }
            var random = Guid.NewGuid();
            File.WriteAllText(file, JsonSerializer.Serialize(new { ClassId = random, ProgId = "wrong" }));
            Assert.Throws<ArgumentException>(() => AppComIdentity.Resolve(start, application));
            File.WriteAllText(file, JsonSerializer.Serialize(new { ClassId = random, ProgId = "WCP.p" + random.ToString("N") }));
            var identity = AppComIdentity.Resolve(start, application);
            Assert.IsTrue(identity.Enabled); Assert.AreEqual(random, identity.ClassId);
            Assert.AreEqual(directory, Environment.GetEnvironmentVariable("WAWICALLPRO_CONFIG_DIR"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("WAWICALLPRO_CONFIG_DIR", saved);
            File.Delete(file); Directory.Delete(directory);
        }
    }
}

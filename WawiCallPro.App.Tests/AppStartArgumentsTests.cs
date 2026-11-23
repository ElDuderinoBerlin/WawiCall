using WawiCallPro.App.Hosting;

namespace WawiCallPro.App.Tests;

[TestClass]
public sealed class AppStartArgumentsTests
{
    [TestMethod]
    public void IsolatedComIdentityUsesAnExplicitAbsoluteFixturePath()
    {
        var com = AppStartArguments.Parse(["--COM-FIXTURE", @"D:\test profile", "-EMBEDDING"]);
        Assert.AreEqual(AppStartKind.Com, com.Kind); Assert.AreEqual(@"D:\test profile", com.FixtureDirectory);
        Assert.AreEqual(AppStartKind.Interactive, AppStartArguments.Parse(["--com-fixture", @"D:\test profile"]).Kind);
        Assert.Throws<ArgumentException>(() => AppStartArguments.Parse(["--com-fixture", "relative", "-Embedding"]));
        Assert.Throws<ArgumentException>(() => AppStartArguments.Parse(["--com-fixture", @"D:\test", "--diagnostics"]));
    }
    [TestMethod]
    public void InteractiveAndDiagnosticArgumentsRetainTheirMeaning()
    {
        Assert.AreEqual(AppStartKind.Interactive, AppStartArguments.Parse([]).Kind);
        Assert.AreEqual(AppStartKind.ResetLicense, AppStartArguments.Parse(["--reset-license"]).Kind);
        Assert.AreEqual(new AppStartArguments(AppStartKind.ImportLicense, @"D:\test license.xml"),
            AppStartArguments.Parse(["--import-license", @"D:\test license.xml"]));
        Assert.Throws<ArgumentException>(() => AppStartArguments.Parse(["--import-license", "relative.xml"]));
        CollectionAssert.AreEqual(new[] { "--diagnostics" }, AppStartArguments.Parse(["--DIAGNOSTICS"]).ToInteractiveArguments());
        CollectionAssert.AreEqual(new[] { "--ui-audit", "audit directory" }, AppStartArguments.Parse(["--UI-AUDIT", "audit directory"]).ToInteractiveArguments());
        CollectionAssert.AreEqual(new[] { "--activate-existing", @"D:\isolated profile" }, AppStartArguments.Parse(["--ACTIVATE-EXISTING", @"D:\isolated profile"]).ToInteractiveArguments());
    }

    [TestMethod]
    public void ComActivationIsExplicitAndCannotFallThroughToInteractiveStartup()
    {
        var start = AppStartArguments.Parse(["-eMbEdDiNg"]);
        Assert.AreEqual(AppStartKind.Com, start.Kind);
        Assert.Throws<InvalidOperationException>(() => start.ToInteractiveArguments());
    }

    [TestMethod]
    public void InvalidAndCombinedArgumentsAreRejected()
    {
        string[][] invalid = [["-Embedding", "--diagnostics"], ["--diagnostics", "extra"], ["--ui-audit"],
            ["--ui-audit", " "], ["--activate-existing", "relative"], ["--activate-existing"],
            ["--unknown"], ["/regserver", "/machine"], ["-Embedding", "-Embedding"]];
        foreach (var arguments in invalid) Assert.Throws<ArgumentException>(() => AppStartArguments.Parse(arguments));
    }
}

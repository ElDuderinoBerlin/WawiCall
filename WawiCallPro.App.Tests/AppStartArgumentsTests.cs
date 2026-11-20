using WawiCallPro.App.Hosting;

namespace WawiCallPro.App.Tests;

[TestClass]
public sealed class AppStartArgumentsTests
{
    [TestMethod]
    public void InteractiveAndDiagnosticArgumentsRetainTheirMeaning()
    {
        Assert.AreEqual(AppStartKind.Interactive, AppStartArguments.Parse([]).Kind);
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

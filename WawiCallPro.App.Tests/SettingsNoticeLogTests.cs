using Microsoft.Extensions.Logging;
using WawiCallPro.App.Hosting;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Operations;

namespace WawiCallPro.App.Tests;

[TestClass]
public sealed class SettingsNoticeLogTests
{
    [TestMethod]
    public void SettingsNoticesReachBothLogWindowAndSafeFileWithoutForeignDetails()
    {
        var root = Path.Combine(Path.GetTempPath(), "Wcp-SettingsLog-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new ApplicationPaths(new ConfigurationLocation(root)); var queue = new LogWindowService();
            using var file = new SafeFileLoggerProvider(paths, new RuntimeFailureState());
            using var factory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Trace).AddProvider(file).AddProvider(new UiLogProvider(queue)));
            var logger = factory.CreateLogger("Settings");
            OperationEvents.Write(logger, OperationEvent.SettingsLoaded);
            OperationEvents.Write(logger, OperationEvent.SettingsCompaniesFailed, new InvalidOperationException("synthetic-private-credentials"));
            var notices = queue.Drain(); Assert.HasCount(2, notices);
            Assert.AreEqual(SettingsMessages.Get(OperationEvent.SettingsLoaded), notices[0].Message);
            Assert.AreEqual(LogLevel.Warning, notices[1].Level);
            var text = File.ReadAllText(Directory.GetFiles(paths.LogsDirectory).Single());
            Assert.Contains("SettingsLoaded", text); Assert.Contains("SettingsCompaniesFailed", text);
            Assert.DoesNotContain("synthetic-private-credentials", text);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}

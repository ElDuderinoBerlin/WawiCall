using Microsoft.Extensions.Logging;
using WawiCallPro.App.Hosting;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Operations;

namespace WawiCallPro.App.Tests;

[TestClass]
public sealed class SettingsNoticeLogTests
{
    [TestMethod]
    public async Task SpecificCompanyFailuresReachUiTextAndStructuredLogsWithoutExceptionContents()
    {
        var root = Path.Combine(Path.GetTempPath(), "Wcp-CompanyFailureLog-" + Guid.NewGuid().ToString("N"));
        var product = Path.Combine(root, "product"); Directory.CreateDirectory(product);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(product, "appsettings.json"), """
                {"Serilog":{"WriteTo":[{"Name":"File","Args":{"path":"logs/WawiCallPro-.log","rollingInterval":"Day","retainedFileCountLimit":14}}]}}
                """);
            var location = new ConfigurationLocation(root, product); var paths = new ApplicationPaths(location);
            var runtime = new RuntimeLoggingSettings(paths);
            var configuration = new ApplicationConfigurationService(new ConfigurationStore(location, new(new WawiCallPro.Utilities.Security.WindowsUserSecretProtector())), runtime);
            await configuration.StartAsync(default);
            var queue = new LogWindowService();
            using var file = new SafeFileLoggerProvider(paths, new RuntimeFailureState(), runtime);
            using var factory = LoggerFactory.Create(builder => builder.AddProvider(file).AddProvider(new UiLogProvider(queue)));
            var logger = factory.CreateLogger("Settings");
            var events = Enum.GetValues<OperationEvent>().Where(value => value.ToString().StartsWith("SettingsCompanies", StringComparison.Ordinal)
                && value >= OperationEvent.SettingsCompaniesConfigurationRequired).ToArray();
            foreach (var value in events) OperationEvents.Write(logger, value, new InvalidOperationException("synthetic-private-password-token-key-body",
                new Exception("synthetic-private-inner-response")));
            var notices = queue.Drain(); Assert.HasCount(events.Length, notices);
            for (var i = 0; i < events.Length; i++)
            {
                Assert.AreEqual(LogLevel.Warning, notices[i].Level);
                Assert.AreEqual(SettingsMessages.Get(events[i]), notices[i].Message);
                Assert.DoesNotContain("synthetic-private", notices[i].Message);
            }
            var text = await File.ReadAllTextAsync(Directory.GetFiles(runtime.Directory, "*.log").Single());
            var json = await File.ReadAllTextAsync(Directory.GetFiles(runtime.Directory, "*.jsonl").Single());
            Assert.Contains("unbekannter Client-Key (2002)", text);
            foreach (var value in events) Assert.Contains(value.ToString(), json);
            Assert.DoesNotContain("synthetic-private", text); Assert.DoesNotContain("synthetic-private", json);
        }
        finally { Directory.Delete(root, true); }
    }

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

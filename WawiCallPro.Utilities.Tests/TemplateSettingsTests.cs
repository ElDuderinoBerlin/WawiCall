using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Licensing;
using WawiCallPro.Utilities.Operations;
using WawiCallPro.Utilities.Security;

namespace WawiCallPro.Utilities.Tests;

[TestClass]
public sealed class TemplateSettingsTests
{
    [TestMethod]
    public async Task FullTemplateBindsUiLicenseCrashAndSerilogWhileExcludingForeignLogData()
    {
        var root = Path.Combine(Path.GetTempPath(), "Wcp-Template-" + Guid.NewGuid().ToString("N"));
        var product = Path.Combine(root, "product"); Directory.CreateDirectory(product);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(product, "appsettings.json"), """
                {"Help":{"Language":"auto"},"PrintForms":{"PretranslateOnLanguageChange":true},
                 "AppUi":{"FontFamily":"Segoe UI","FontSize":12,"ShowStartPage":false},
                 "LicenseServer":{"Url":"https://license.example.invalid"},
                 "CrashReporting":{"Enabled":true,"ServiceBaseUri":"https://crash.example.invalid","QueueFlushIntervalSeconds":15,"RequestTimeoutSeconds":17},
                 "Serilog":{"MinimumLevel":{"Default":"Debug","Override":{"Microsoft":"Warning"}},
                   "WriteTo":[{"Name":"File","Args":{"path":"logs/WawiCallPro-.log","rollingInterval":"Day","retainedFileCountLimit":2,"outputTemplate":"{Level:u3} {Message:lj}{NewLine}"}}]}}
                """);
            var location = new ConfigurationLocation(Path.Combine(root, "profile"), product); var paths = new ApplicationPaths(location);
            var store = new ConfigurationStore(location, new(new WindowsUserSecretProtector()));
            var runtime = new RuntimeLoggingSettings(paths); var failures = new RuntimeFailureState();
            using var provider = new SafeFileLoggerProvider(paths, failures, runtime);
            var configuration = new ApplicationConfigurationService(store, runtime); await configuration.StartAsync(default);
            Assert.AreEqual(12F, configuration.Current.Options.User.FontSize); Assert.IsFalse(configuration.Current.Options.User.ShowStartPage);
            var startup = StartupSettings.LoadApplicationSettings(paths, location);
            Assert.AreEqual("https://license.example.invalid/", startup.LicenseServerUrl!.AbsoluteUri);
            Assert.AreEqual("https://crash.example.invalid/", startup.CrashServiceBaseUri!.AbsoluteUri);
            Assert.AreEqual(15, startup.CrashFlushIntervalSeconds); Assert.IsTrue(startup.UploadCrashReports);
            using var crashClient = new StartupHttpClientFactory(startup).CreateCrashClient();
            Assert.AreEqual(startup.CrashServiceBaseUri, crashClient.BaseAddress); Assert.AreEqual(TimeSpan.FromSeconds(17), crashClient.Timeout);
            var microsoft = provider.CreateLogger("Microsoft.Example"); Assert.IsFalse(microsoft.IsEnabled(LogLevel.Information));
            var application = provider.CreateLogger("WawiCallPro.Settings"); Assert.IsTrue(application.IsEnabled(LogLevel.Debug));
            application.LogInformation(new InvalidOperationException("synthetic-private-exception"), "synthetic-private-message");
            var file = Directory.GetFiles(runtime.Directory, "*.log").Single(); var text = await File.ReadAllTextAsync(file);
            Assert.Contains("FrameworkMessage", text); Assert.DoesNotContain("synthetic-private", text);
            var foreign = Path.Combine(runtime.Directory, "WawiCallPro-foreign.log"); await File.WriteAllTextAsync(foreign, "retain");
            for (var i = 0; i < 3; i++)
                await File.WriteAllTextAsync(Path.Combine(runtime.Directory, "WawiCallPro-20000101-1-" + Guid.NewGuid().ToString("N") + ".log"), "old");
            application.LogInformation("synthetic-private-message");
            Assert.AreEqual(3, Directory.GetFiles(runtime.Directory, "*.log").Length);
            Assert.IsTrue(File.Exists(foreign));
            var before = await File.ReadAllTextAsync(Path.Combine(product, "appsettings.json"));
            await store.SaveAsync(configuration.Current.Options, configuration.Current);
            Assert.AreEqual(before, await File.ReadAllTextAsync(Path.Combine(product, "appsettings.json")));
            Assert.IsNotNull(JsonNode.Parse(before)!["PrintForms"]);
        }
        finally { Directory.Delete(root, true); }
    }
}

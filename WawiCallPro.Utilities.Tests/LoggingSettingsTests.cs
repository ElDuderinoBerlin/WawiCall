using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Operations;
using WawiCallPro.Utilities.Security;

namespace WawiCallPro.Utilities.Tests;

[TestClass]
public sealed class LoggingSettingsTests
{
    [TestMethod]
    [DataRow(LogLevel.Trace)]
    [DataRow(LogLevel.Debug)]
    [DataRow(LogLevel.Information)]
    [DataRow(LogLevel.Warning)]
    [DataRow(LogLevel.Error)]
    [DataRow(LogLevel.Critical)]
    [DataRow(LogLevel.None)]
    public async Task SerilogUsesLoadedThresholdAndDirectoryWithoutLosingBackgroundFailures(LogLevel minimum)
    {
        var root = Path.Combine(Path.GetTempPath(), "WawiCallPro-logging-" + Guid.NewGuid().ToString("N"));
        try
        {
            var location = new ConfigurationLocation(root);
            var paths = new ApplicationPaths(location);
            var store = new ConfigurationStore(location, new(new WindowsUserSecretProtector()));
            var snapshot = await store.LoadAsync();
            var directory = Path.Combine(root, "selected-logs");
            await store.SaveAsync(snapshot.Options with { User = snapshot.Options.User with { LogLevel = minimum, LogDirectory = directory } }, snapshot);
            var settings = new RuntimeLoggingSettings(paths);
            var failures = new RuntimeFailureState();
            // The provider must be resolvable before configuration has been loaded.
            using var provider = new SafeFileLoggerProvider(paths, failures, settings);
            var configuration = new ApplicationConfigurationService(store, settings);
            await configuration.StartAsync(default);
            Assert.AreEqual(directory, settings.Directory);
            Assert.IsFalse(Directory.Exists(directory));
            var logger = provider.CreateLogger("untrusted-library");
            for (var value = LogLevel.Trace; value < LogLevel.None; value++)
                logger.Log(value, new EventId(42, "private-name"), "private-state", new IOException("private-error"),
                    (_, _) => throw new AssertFailedException("A foreign formatter must never run."));
            var expected = Math.Max(0, (int)LogLevel.None - (int)minimum);
            if (expected > 0)
            {
                var lines = File.ReadAllLines(Directory.GetFiles(directory).Single());
                Assert.HasCount(expected, lines);
                foreach (var line in lines)
                {
                    Assert.DoesNotContain("private", line);
                    using var json = JsonDocument.Parse(line);
                    Assert.IsTrue(Enum.Parse<LogLevel>(json.RootElement.GetProperty("Level").GetString()!) >= minimum);
                }
            }
            else Assert.IsFalse(Directory.Exists(directory));
            Assert.IsFalse(Directory.Exists(paths.LogsDirectory));
            var original = new InvalidOperationException("private-background-error");
            provider.CreateLogger("Microsoft.Extensions.Hosting.Internal.Host").LogError(original, "private-message");
            Assert.AreSame(original, failures.Failure);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task VersionSevenGetsLoggingDefaultsAndInvalidSettingsNeverReplaceSavedFile()
    {
        var root = Path.Combine(Path.GetTempPath(), "WawiCallPro-logging-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var location = new ConfigurationLocation(root);
            const string legacy = "{\"formatVersion\":7,\"settings\":{\"user\":{\"startMinimized\":true}}}";
            await File.WriteAllTextAsync(location.FilePath, legacy);
            var store = new ConfigurationStore(location, new(new WindowsUserSecretProtector()));
            var old = await store.LoadAsync();
            Assert.AreEqual(LogLevel.Information, old.Options.User.LogLevel); Assert.IsNull(old.Options.User.LogDirectory);
            Assert.AreEqual(legacy, await File.ReadAllTextAsync(location.FilePath));
            await store.SaveAsync(old.Options, old);
            var current = await store.LoadAsync();
            Assert.AreEqual(8, current.FormatVersion); Assert.IsTrue(current.Options.User.StartMinimized);
            var before = await File.ReadAllBytesAsync(location.FilePath);
            foreach (var path in new[] { "relative", "", " ", "C:\\logs\nprivate", @"\\server\share", @"C:\logs:stream", @"C:\" })
                await Assert.ThrowsExactlyAsync<ConfigurationException>(() => store.SaveAsync(current.Options with { User = current.Options.User with { LogDirectory = path } }, current));
            await Assert.ThrowsExactlyAsync<ConfigurationException>(() => store.SaveAsync(current.Options with { User = current.Options.User with { LogLevel = (LogLevel)99 } }, current));
            CollectionAssert.AreEqual(before, await File.ReadAllBytesAsync(location.FilePath));
        }
        finally { Directory.Delete(root, true); }
    }
}

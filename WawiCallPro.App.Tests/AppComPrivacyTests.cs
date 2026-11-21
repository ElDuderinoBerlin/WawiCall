using System.Text.Json;
using Microsoft.Extensions.Logging;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Operations;

namespace WawiCallPro.App.Tests;

[TestClass]
public sealed class AppComPrivacyTests
{
    [TestMethod, DataRow(1024), DataRow(4096)]
    public async Task ComProviderFailuresAndRealSerilogFilesContainOnlyBoundedSafeMetadata(int maximumBytes)
    {
        const string privateData = "synthetic-secret-company-00123-phone-0049123456";
        await using var h = await AppComTelephonyTests.Harness.CreateAsync();
        await AppComTelephonyTests.Done(h.App, h.App.ConnectTelephony());
        h.Fixture.Phone.Failure = new IOException(privateData);
        var result = await AppComTelephonyTests.Done(h.App, h.App.Dial("00123", h.Company, Guid.NewGuid().ToString("D")));
        Assert.AreEqual("ProviderUnavailable", result.ErrorCode);
        Assert.IsTrue(result.Message.Length < 256); Assert.AreEqual(36, result.Id.Length);
        Assert.DoesNotContain(privateData, JsonSerializer.Serialize(result));
        Assert.AreEqual(0, h.Presentation.Shown);
        var root = Path.Combine(Path.GetTempPath(), "WawiCallPro-com-privacy-" + Guid.NewGuid().ToString("N"));
        var paths = new ApplicationPaths(new ConfigurationLocation(root));
        try
        {
            var failures = new RuntimeFailureState();
            using (var provider = new SafeFileLoggerProvider(paths, failures, maximumBytes))
            {
                var logger = provider.CreateLogger(privateData);
                using var scope = logger.BeginScope(new Unformattable());
                logger.Log(LogLevel.Error, new EventId(42, privateData), new Unformattable(), new IOException(privateData),
                    (_, _) => throw new AssertFailedException("Foreign log formatter must never run."));
                OperationEvents.Write(logger, OperationEvent.HostFailed, new InvalidOperationException(privateData));
                for (var i = 0; i < 100; i++)
                    logger.LogError(new IOException(privateData), "Credentials {Value}", privateData);
            }
            var file = Directory.GetFiles(paths.LogsDirectory, "*.jsonl").Single();
            Assert.IsTrue(new FileInfo(file).Length <= maximumBytes);
            var lines = File.ReadAllLines(file); Assert.IsTrue(lines.Length >= 2);
            foreach (var line in lines)
            {
                Assert.DoesNotContain(privateData, line); Assert.DoesNotContain("Credentials", line);
                using var json = JsonDocument.Parse(line);
                CollectionAssert.AreEquivalent(new[] { "TimestampUtc", "RunId", "ProcessId", "Level", "Event", "EventId", "ExceptionType" },
                    json.RootElement.EnumerateObject().Select(property => property.Name).ToArray());
            }
            Assert.IsInstanceOfType<OperationException>(failures.Failure);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    private sealed class Unformattable
    { public override string ToString() => throw new AssertFailedException("Private logger state or scope must never be formatted."); }
}

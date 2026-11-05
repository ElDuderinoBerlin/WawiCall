using System.Text.Json;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Diagnostics;
using WawiCallPro.Utilities.Hosting;
using WawiCallPro.Utilities.Operations;

namespace WawiCallPro.Utilities.Tests;

[TestClass]
public sealed class CrashReportingTests
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WawiCallPro-crash-tests-" + Guid.NewGuid().ToString("N"));
    [TestCleanup]
    public void Cleanup() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
    private CrashReportQueue CreateQueue() => new(new ApplicationPaths(new ConfigurationLocation(_root)));
    private CrashReporter CreateReporter(CrashReportQueue queue) => new(queue, new ApplicationDescriptor("WawiCallPro", "Pro"), new("test", "test"), TimeProvider.System);

    [TestMethod]
    public void DurableReportsContainMethodsAndIdentityButNoExceptionMessagesDataOrSourcePaths()
    {
        const string secret = "sip-password-and-private-connection-string";
        Exception exception;
        try { throw new InvalidOperationException(secret, new IOException(secret)); } catch (Exception error) { exception = error; }
        exception.Data[secret] = secret;
        var queue = CreateQueue();
        var receipt = CreateReporter(queue).Capture(exception, CrashOrigin.Startup);
        Assert.IsTrue(receipt.Stored);
        var json = File.ReadAllText(queue.GetPendingFiles().Single());
        Assert.DoesNotContain(secret, json);
        Assert.DoesNotContain("Application-Development", json);
        var payload = JsonSerializer.Deserialize<CrashReport>(json, CrashReportQueue.JsonOptions)!;
        Assert.AreEqual(receipt.ReportId, payload.ReportId);
        Assert.AreEqual("WawiCallPro", payload.Application);
        Assert.Contains(nameof(DurableReportsContainMethodsAndIdentityButNoExceptionMessagesDataOrSourcePaths), payload.StackTrace!);
        Assert.AreEqual(0, Directory.GetFiles(queue.DirectoryPath, "*.partial").Length);
    }

    [TestMethod]
    public async Task ConcurrentCapturesRemainAtomicAndQueueIsBounded()
    {
        var queue = CreateQueue(); var reporter = CreateReporter(queue);
        var receipts = await Task.WhenAll(Enumerable.Range(0, 100).Select(_ => Task.Run(() => reporter.Capture(new Exception("secret"), CrashOrigin.BackgroundTask))));
        Assert.IsTrue(receipts.All(receipt => receipt.Stored));
        Assert.AreEqual(CrashReportQueue.MaximumReports, queue.GetPendingFiles().Length);
        foreach (var path in queue.GetPendingFiles()) Assert.IsNotNull(JsonSerializer.Deserialize<CrashReport>(File.ReadAllBytes(path), CrashReportQueue.JsonOptions));
    }

    [TestMethod]
    public void StorageFailureDoesNotThrowAnotherFatalException()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "crash-reports"), "obstructed");
        Assert.IsFalse(CreateReporter(CreateQueue()).Capture(new Exception("secret"), CrashOrigin.Process).Stored);
    }
}

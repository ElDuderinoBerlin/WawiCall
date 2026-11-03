using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WawiCallPro.ConsoleApp.Diagnostics;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Operations;

namespace WawiCallPro.Console.Tests;

[TestClass]
public sealed class DiagnosticLogTests
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WawiCallPro-log-reader-tests-" + Guid.NewGuid().ToString("N"));

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    [TestMethod]
    public async Task ReaderSharesFilesWithActiveWritersAndReturnsAllCompletedRecords()
    {
        var paths = new ApplicationPaths(new ConfigurationLocation(_root));
        var failures = new RuntimeFailureState();
        using var provider = new SafeFileLoggerProvider(paths, failures);
        var logger = provider.CreateLogger("test");
        OperationEvents.Write(logger, OperationEvent.HostStarted);
        var writer = Task.Run(() =>
        {
            for (var index = 0; index < 100; index++) OperationEvents.Write(logger, OperationEvent.FrameworkMessage);
        });
        do { _ = DiagnosticLogReader.Read(paths); } while (!writer.IsCompleted);
        await writer;
        var snapshot = DiagnosticLogReader.Read(paths);
        Assert.HasCount(101, snapshot.Entries);
        Assert.AreEqual(0, snapshot.IncompleteTails);
        Assert.IsNull(failures.Failure);
        Assert.AreEqual(OperationEvent.HostStarted, snapshot.Entries[0].Event);
        Assert.IsTrue(snapshot.Entries.All(entry => entry.ProcessId == Environment.ProcessId && entry.TimestampUtc.Offset == TimeSpan.Zero));
    }

    [TestMethod]
    public void SnapshotReportsPartialTailAndPreservesEveryMetadataField()
    {
        var paths = new ApplicationPaths(new ConfigurationLocation(_root));
        Directory.CreateDirectory(paths.LogsDirectory);
        var run = Guid.NewGuid();
        var entry = new LogEntry(DateTimeOffset.UtcNow, run, 123, LogLevel.Error, OperationEvent.HostFailed, 45, nameof(IOException));
        File.WriteAllText(Path.Combine(paths.LogsDirectory, "operations-fixture.jsonl"), JsonSerializer.Serialize(entry,
            new JsonSerializerOptions { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } }) + "\n{\"unfinished\":", new UTF8Encoding(false));
        var snapshot = DiagnosticLogReader.Read(paths);
        Assert.HasCount(1, snapshot.Entries);
        Assert.AreEqual(entry, snapshot.Entries[0]);
        Assert.AreEqual(1, snapshot.IncompleteTails);
        var table = DiagnosticTables.Logs(snapshot);
        Assert.Contains(run.ToString("D"), table.Rows[0].Value);
        Assert.Contains("PID 123", table.Rows[0].Value);
        Assert.Contains("ID 45", table.Rows[0].Value);
        Assert.Contains(nameof(IOException), table.Rows[0].Value);
    }

    [TestMethod]
    [DataRow("{not-json}")]
    [DataRow("{\"message\":\"private-sentinel\"}")]
    [DataRow("{\"TimestampUtc\":null,\"TimestampUtc\":null}")]
    public void InvalidRecordsFailWithAFixedMessageWithoutEchoingRawData(string invalid)
    {
        var paths = new ApplicationPaths(new ConfigurationLocation(_root));
        Directory.CreateDirectory(paths.LogsDirectory);
        File.WriteAllText(Path.Combine(paths.LogsDirectory, "operations-private-sentinel.jsonl"), invalid + "\n");
        var failure = Assert.ThrowsExactly<DiagnosticException>(() => DiagnosticLogReader.Read(paths));
        Assert.DoesNotContain("private-sentinel", failure.Message);
        Assert.DoesNotContain(_root, failure.Message);
    }

    [TestMethod]
    public void MissingLogsAreEmptyAndOversizedFilesAreRejected()
    {
        var paths = new ApplicationPaths(new ConfigurationLocation(_root));
        Assert.HasCount(0, DiagnosticLogReader.Read(paths).Entries);
        Assert.IsFalse(Directory.Exists(_root));
        Directory.CreateDirectory(paths.LogsDirectory);
        using (var file = File.Create(Path.Combine(paths.LogsDirectory, "operations-large.jsonl"))) file.SetLength(4194305);
        Assert.ThrowsExactly<DiagnosticException>(() => DiagnosticLogReader.Read(paths));
    }
}

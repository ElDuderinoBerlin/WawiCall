using WawiCallPro.ConsoleApp.Diagnostics;
using WawiCallPro.Utilities.Persistence;

namespace WawiCallPro.Console.Tests;

[TestClass]
public sealed class RecordingExportCommandTests
{
    [TestMethod]
    public void ExportRequiresAnArtifactAndExplicitAbsoluteDestination()
    {
        var id = Guid.NewGuid(); var path = Path.Combine(Path.GetTempPath(), "private-export.wav");
        var command = ConsoleCommand.Parse(["--recording-export", id.ToString("D"), path]);
        Assert.AreEqual(id, command.RecordingArtifactId); Assert.AreEqual(path, command.File);
        Assert.IsTrue(command.UsesRecordingExport); Assert.IsFalse(command.UsesDatabase); Assert.IsFalse(command.UsesRecordingInventory);
        foreach (var args in new[] { new[] { "--recording-export" }, ["--recording-export", "private-invalid", path],
            ["--recording-export", Guid.Empty.ToString("D"), path], ["--recording-export", id.ToString("D"), "relative.wav"],
            ["--recording-export", id.ToString("D"), path, "--overwrite"] })
        {
            var error = Assert.ThrowsExactly<DiagnosticException>(() => ConsoleCommand.Parse(args));
            Assert.DoesNotContain(path, error.ToString()); Assert.DoesNotContain("private-invalid", error.ToString());
        }
        Assert.IsTrue(DiagnosticTables.Help().Rows.Any(row => row.Name.StartsWith("--recording-export", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task SuccessfulPublicationRetainsSuccessEvenWhenCancellationArrivesAfterward()
    {
        using var cancel = new CancellationTokenSource();
        var service = new Exporter { Published = cancel.Cancel }; var id = Guid.NewGuid(); var path = Path.Combine(Path.GetTempPath(), "private-export.wav");
        var table = await RecordingExportCommands.ExecuteAsync(ConsoleCommand.Parse(["--recording-export", id.ToString("D"), path]), service, cancel.Token);
        Assert.AreEqual(id, service.Id); Assert.AreEqual(path, service.Path); Assert.AreEqual(1, service.Calls);
        Assert.IsTrue(table.Rows.Any(row => row.Value.Contains("veröffentlicht", StringComparison.Ordinal)));
        Assert.IsFalse(table.Rows.Any(row => row.Value.Contains(path, StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task FailuresAreSanitizedAndRequestedCancellationIsPreserved()
    {
        var command = ConsoleCommand.Parse(["--recording-export", Guid.NewGuid().ToString("D"), Path.Combine(Path.GetTempPath(), "private-export.wav")]);
        var service = new Exporter { Fail = true };
        var error = await Assert.ThrowsExactlyAsync<DiagnosticException>(() => RecordingExportCommands.ExecuteAsync(command, service, default));
        Assert.IsNull(error.InnerException); Assert.DoesNotContain("private-export", error.ToString());
        await Assert.ThrowsAsync<OperationCanceledException>(() => RecordingExportCommands.ExecuteAsync(command, service, new CancellationToken(true)));
    }

    [TestMethod]
    public async Task UnrelatedCommandsNeverInvokeExport()
    {
        var service = new Exporter();
        await Assert.ThrowsExactlyAsync<DiagnosticException>(() => RecordingExportCommands.ExecuteAsync(ConsoleCommand.Parse(["--status"]), service, default));
        Assert.AreEqual(0, service.Calls);
    }

    private sealed class Exporter : IRecordingExport
    {
        internal Guid Id; internal string? Path; internal int Calls; internal bool Fail; internal Action? Published;
        public Task ExportAsync(Guid id, string destination, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested(); Id = id; Path = destination; Calls++;
            if (Fail) throw new IOException("private-export-path");
            Published?.Invoke(); return Task.CompletedTask;
        }
    }
}

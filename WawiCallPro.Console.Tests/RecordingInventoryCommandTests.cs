using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using WawiCallPro.ConsoleApp.Diagnostics;
using WawiCallPro.Utilities.Persistence;

namespace WawiCallPro.Console.Tests;

[TestClass]
public sealed class RecordingInventoryCommandTests
{
    [TestMethod]
    public void PathsRequireExplicitDetailsAndUnexpectedArgumentsAreRejected()
    {
        var summary = ConsoleCommand.Parse(["--recordings-inventory"]);
        Assert.IsTrue(summary.UsesRecordingInventory); Assert.IsFalse(summary.ShowRecordingPaths); Assert.IsFalse(summary.UsesDatabase);
        Assert.IsTrue(ConsoleCommand.Parse(["--recordings-inventory", "--details"]).ShowRecordingPaths);
        foreach (var arguments in new[] { new[] { "--details" }, ["--recordings-inventory", "private-path"], ["--recordings-inventory", "--details", "--delete"] })
            Assert.ThrowsExactly<DiagnosticException>(() => ConsoleCommand.Parse(arguments));
        Assert.IsTrue(DiagnosticTables.Help().Rows.Any(row => row.Name.StartsWith("--recordings-inventory", StringComparison.Ordinal)));
    }

    [TestMethod]
    [DataRow(false)][DataRow(true)]
    public async Task SummaryHidesPathsAndDetailsExplainEachReviewCategory(bool details)
    {
        var service = new Inventory(); var command = ConsoleCommand.Parse(details ? ["--recordings-inventory", "--details"] : ["--recordings-inventory"]);
        var table = await RecordingInventoryCommands.ExecuteAsync(command, service, default);
        Assert.AreEqual(1, service.Reads); Assert.IsTrue(table.Rows.Any(row => row.Name == "Registrierte Dateien" && row.Value == "4"));
        Assert.IsTrue(table.Rows.Any(row => row.Value.Contains("kein Verwaisungsnachweis", StringComparison.Ordinal)));
        foreach (var path in new[] { "private.wav", "partial.tmp", "linked" })
            Assert.AreEqual(details, table.Rows.Any(row => row.Value == path));
        if (details) Assert.IsTrue(table.Rows.Any(row => row.Name == "Link übersprungen" && row.Value == "linked"));
    }

    [TestMethod]
    public async Task EmptyDetailsAreExplicitAndReadFailuresNeverExposePathsOrExceptions()
    {
        var service = new Inventory { Empty = true };
        var command = ConsoleCommand.Parse(["--recordings-inventory", "--details"]);
        var table = await RecordingInventoryCommands.ExecuteAsync(command, service, default);
        Assert.IsTrue(table.Rows.Any(row => row.Value == "Keine Einträge im beobachteten Bestand."));
        service.Fail = true;
        var error = await Assert.ThrowsExactlyAsync<DiagnosticException>(() => RecordingInventoryCommands.ExecuteAsync(command, service, default));
        Assert.DoesNotContain("private-path", error.ToString()); Assert.IsNull(error.InnerException);
        await Assert.ThrowsAsync<OperationCanceledException>(() => RecordingInventoryCommands.ExecuteAsync(command, service, new CancellationToken(true)));
    }

    [TestMethod]
    public async Task RealConsoleUsesIsolatedReadOnlyInventoryWithoutPluginOrInteractiveInput()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "wcp-inventory-command-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(root);
        try
        {
            var unavailable = await Run(root, "--recordings-inventory");
            Assert.AreEqual(1, unavailable.Exit); Assert.Contains("Arbeitsplatzkennung", unavailable.Error);
            Assert.IsFalse(File.Exists(Path.Combine(root, "workstation.local.json"))); Assert.IsFalse(Directory.EnumerateFiles(root, "*.db").Any());
            Assert.AreEqual(0, (await Run(root, "--db-init")).Exit);
            var identity = File.ReadAllBytes(Path.Combine(root, "workstation.local.json"));
            var database = Directory.EnumerateFiles(root, "*.db").Single(); var before = File.ReadAllBytes(database);
            var recordings = Path.Combine(root, "recordings"); Directory.CreateDirectory(recordings);
            for (var index = 0; index < 105; index++) File.WriteAllBytes(Path.Combine(recordings, $"synthetic-{index:000}.wav"), [1, 2, 3]);
            var summary = await Run(root, "--recordings-inventory"); Assert.AreEqual(0, summary.Exit, summary.Error); Assert.DoesNotContain("synthetic-", summary.Output);
            var details = await Run(root, "--recordings-inventory", "--details"); Assert.AreEqual(0, details.Exit, details.Error);
            for (var index = 0; index < 105; index++) Assert.Contains($"synthetic-{index:000}.wav", details.Output);
            Assert.DoesNotContain(root, details.Output); Assert.DoesNotContain("N/P/G/Q", details.Output);
            CollectionAssert.AreEqual(before, File.ReadAllBytes(database)); CollectionAssert.AreEqual(identity, File.ReadAllBytes(Path.Combine(root, "workstation.local.json")));
            foreach (var path in Directory.EnumerateFiles(recordings)) CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, File.ReadAllBytes(path));
        }
        finally
        {
            var resolved = Path.GetFullPath(root);
            if (!resolved.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(resolved).StartsWith("wcp-inventory-command-", StringComparison.Ordinal))
                throw new AssertFailedException("Unexpected fixture cleanup target.");
            Directory.Delete(resolved, true);
        }
    }

    private static async Task<(int Exit, string Output, string Error)> Run(string root, params string[] arguments)
    {
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "WawiCallProSolution.sln"))) repository = repository.Parent;
        Assert.IsNotNull(repository);
        var descriptor = WawiCallPro.Utilities.Hosting.ApplicationDescriptor.FromAssembly(typeof(ConsoleCommand).Assembly);
        var configuration = typeof(ConsoleCommand).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(Path.Combine(repository.FullName, "WawiCallPro.Console", "bin", configuration, "net11.0-windows10.0.19041.0", "WawiCallPro.Console." + descriptor.ProductLine + ".dll"));
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        start.Environment["WAWICALLPRO_CONFIG_DIR"] = root;
        using var process = Process.Start(start)!; var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(timeout.Token); }
        finally { if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); } }
        return (process.ExitCode, await output, await error);
    }

    private sealed class Inventory : IRecordingDirectoryInventory
    {
        internal int Reads; internal bool Empty, Fail;
        public Task<RecordingDirectoryInventoryResult> ReadAsync(CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested(); Reads++;
            if (Fail) throw new IOException("private-path");
            ImmutableArray<RecordingDirectoryEntry> review = Empty ? [] : [new("private.wav", false), new("partial.tmp", false), new("linked", true)];
            return Task.FromResult(new RecordingDirectoryInventoryResult(4, Empty ? 0 : 1, Empty ? 0 : 1, Empty ? 0 : 1, review));
        }
    }
}

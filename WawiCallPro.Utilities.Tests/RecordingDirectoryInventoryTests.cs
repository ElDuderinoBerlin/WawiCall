using System.Collections.Immutable;
using WawiCallPro.Database;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Database.Repositories;
using WawiCallPro.Interfaces.Common;
using WawiCallPro.Utilities.Operations;
using WawiCallPro.Utilities.Persistence;

namespace WawiCallPro.Utilities.Tests;

[TestClass]
public sealed class RecordingDirectoryInventoryTests
{
    [TestMethod]
    public async Task AllRegisteredStatesAndUnassignedFilesAreInventoriedWithoutReadingOrChangingContent()
    { using var database = await WaveArtifactFixture.Create(); await RecordingDirectoryInventoryAcceptance.RunAsync(database.Target); }

    [TestMethod]
    [DataRow("identity")][DataRow("database")][DataRow("incomplete")][DataRow("traversal")]
    public async Task UnprovenCatalogsNeverProduceAnUnassignedFileReport(string guard)
    {
        using var file = await WaveArtifactFixture.Create(); var reads = new Entries([new("private.wav", false)]);
        var catalog = new Catalog { Fail = guard == "database", Partial = guard == "incomplete", Paths = guard == "traversal" ? ["../private.wav"] : ["captured.wav"] };
        var service = new RecordingDirectoryInventory(catalog, new Identity(guard == "identity" ? null : Guid.NewGuid()), reads, file.Paths);
        if (guard == "database") await Assert.ThrowsExactlyAsync<IOException>(() => service.ReadAsync());
        else if (guard == "traversal") await Assert.ThrowsExactlyAsync<OperationException>(() => service.ReadAsync());
        else await Assert.ThrowsExactlyAsync<RecordingOutboxException>(() => service.ReadAsync());
        Assert.AreEqual(0, reads.Reads);
    }

    [TestMethod]
    public async Task CaseAndDirectorySeparatorsMatchAndLinksAreAlwaysReportedAsSkipped()
    {
        using var file = await WaveArtifactFixture.Create(); var catalog = new Catalog { Paths = ["nested/known.wav"] };
        var entries = new Entries([new("NESTED\\KNOWN.WAV", false), new("nested/link.wav", true), new("unknown.mp3", false), new("partial.TMP", false)]);
        var result = await new RecordingDirectoryInventory(catalog, new Identity(Guid.NewGuid()), entries, file.Paths).ReadAsync();
        Assert.AreEqual(1, result.Registered); Assert.AreEqual(1, result.WithoutLocalReference); Assert.AreEqual(1, result.TemporaryWithoutLocalReference);
        Assert.AreEqual(1, result.LinksSkipped); Assert.AreEqual(3, result.Review.Length);
    }

    [TestMethod]
    public async Task MissingDirectoryIsEmptyAndCancellationIsObserved()
    {
        using var file = await WaveArtifactFixture.Create(); Directory.Delete(file.Paths.RecordingsDirectory, true);
        var directory = new LocalRecordingDirectoryEntries(file.Paths); Assert.IsFalse(directory.Read().Any());
        await Assert.ThrowsAsync<OperationCanceledException>(() => new RecordingDirectoryInventory(new Catalog(), new Identity(Guid.NewGuid()), directory, file.Paths).ReadAsync(new CancellationToken(true)));
    }

    [TestMethod]
    public async Task DirectoryJunctionsAreNotFollowedAndRootJunctionIsRejected()
    {
        using var file = await WaveArtifactFixture.Create(); var outside = Path.Combine(file.Paths.RootDirectory, "outside"); Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, "outside.wav"), "private-synthetic");
        var link = Path.Combine(file.Paths.RecordingsDirectory, "link");
        var start = new System.Diagnostics.ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("/c"); start.ArgumentList.Add("mklink"); start.ArgumentList.Add("/J"); start.ArgumentList.Add(link); start.ArgumentList.Add(outside);
        using var process = System.Diagnostics.Process.Start(start)!; await process.WaitForExitAsync(); Assert.AreEqual(0, process.ExitCode);
        try
        {
            var rows = new LocalRecordingDirectoryEntries(file.Paths).Read().ToArray();
            Assert.IsTrue(rows.Any(row => row.RelativePath == "link" && row.IsLink)); Assert.IsFalse(rows.Any(row => row.RelativePath.Contains("outside.wav", StringComparison.Ordinal)));
        }
        finally { Directory.Delete(link); }
        var second = new ApplicationPaths(new WawiCallPro.Utilities.Configuration.ConfigurationLocation(Path.Combine(file.Paths.RootDirectory, "second")));
        Directory.CreateDirectory(second.RootDirectory);
        start.ArgumentList.Clear(); start.ArgumentList.Add("/c"); start.ArgumentList.Add("mklink"); start.ArgumentList.Add("/J"); start.ArgumentList.Add(second.RecordingsDirectory); start.ArgumentList.Add(outside);
        using var rootLink = System.Diagnostics.Process.Start(start)!; await rootLink.WaitForExitAsync(); Assert.AreEqual(0, rootLink.ExitCode);
        try { Assert.ThrowsExactly<RecordingOutboxException>(() => new LocalRecordingDirectoryEntries(second).Read().ToArray()); }
        finally { Directory.Delete(second.RecordingsDirectory); }
        Assert.AreEqual("private-synthetic", File.ReadAllText(Path.Combine(outside, "outside.wav")));
    }

    private sealed class Catalog : IRecordingFileCatalogQuery
    {
        internal bool Fail, Partial; internal ImmutableArray<string> Paths = ["known.wav"];
        public Task<Page<RecordingFileReference>> GetPageAsync(Guid workstation, Guid? afterRecordingId = null, int limit = 100, CancellationToken token = default)
        { token.ThrowIfCancellationRequested(); if (Fail) throw new IOException("private-database-error"); return Task.FromResult(new Page<RecordingFileReference>(Partial ? [] : [new(Guid.NewGuid(), Paths)], Partial)); }
    }
    private sealed class Entries(RecordingDirectoryEntry[] rows) : IRecordingDirectoryEntries
    { internal int Reads; public IEnumerable<RecordingDirectoryEntry> Read(CancellationToken token = default) { Reads++; return rows; } }
    private sealed class Identity(Guid? id) : IWorkstationIdentity
    { public Task<Guid?> GetAsync(CancellationToken token = default) { token.ThrowIfCancellationRequested(); return Task.FromResult(id); } public Task<Guid> InitializeAsync(CancellationToken token = default) => throw new AssertFailedException(); }
}

public static class RecordingDirectoryInventoryAcceptance
{
    public static async Task RunAsync(DatabaseTarget target)
    {
        using var file = await WaveArtifactFixture.Create(target); var call = (await new DapperCallRepository(new(target, new())).GetAsync(file.CompletedRecording.Recording.CallId))!;
        var catalog = new DapperRecordingFileCatalogQuery(new(target, new())); var local = call.History.Call.Origin!.WorkstationId;
        Assert.IsTrue((await catalog.GetPageAsync(Guid.NewGuid())).Items.IsEmpty);
        var first = (await catalog.GetPageAsync(local, limit: 1)).Items.Single(); Assert.AreEqual(file.Id, first.RecordingId); Assert.Contains("captured.wav", first.RelativePaths);
        var recordings = new DapperRecordingRepository(new(target, new()));
        for (var index = 0; index < 2; index++) await recordings.WriteAsync(new(new(Guid.NewGuid(), call.History.Call.Id, "legacy-" + index + ".wav", "wav", null,
            WawiCallPro.Interfaces.History.RecordingAvailability.Pending), null, DateTimeOffset.UtcNow));
        Guid? after = null; var visited = new HashSet<Guid>();
        while (true)
        {
            var page = await catalog.GetPageAsync(local, after, 1); Assert.IsTrue(visited.Add(page.Items.Single().RecordingId)); after = page.Items[0].RecordingId;
            if (!page.HasMore) break;
        }
        Assert.AreEqual(3, visited.Count);
        var mp3 = new RecordingArtifact(Guid.NewGuid(), file.Id, call.History.Call.Id, RecordingArtifactFormat.Mp3, "pending.mp3", WawiCallPro.Interfaces.History.RecordingAvailability.Pending);
        await file.Artifacts.WriteAsync(new(mp3, null, DateTimeOffset.UtcNow)); File.WriteAllBytes(file.Paths.ResolveRecordingPath("pending.mp3"), [1, 2]);
        File.WriteAllBytes(file.Paths.ResolveRecordingPath("unassigned.wav"), [3, 4]); File.WriteAllBytes(file.Paths.ResolveRecordingPath("partial.tmp"), [5, 6]);
        Directory.CreateDirectory(file.Paths.ResolveRecordingPath("nested")); File.WriteAllBytes(file.Paths.ResolveRecordingPath("nested/unknown.bin"), [7, 8]);
        var before = await file.Parent.GetAsync(file.Id); var capture = await file.Captures.GetAsync(file.Id); var artifacts = await file.Artifacts.GetForRecordingAsync(file.Id);
        var inventory = new RecordingDirectoryInventory(catalog, new Identity(local), new LocalRecordingDirectoryEntries(file.Paths), file.Paths);
        using (File.Open(file.Paths.ResolveRecordingPath("unassigned.wav"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var result = await inventory.ReadAsync(); Assert.AreEqual(2, result.Registered); Assert.AreEqual(2, result.WithoutLocalReference); Assert.AreEqual(1, result.TemporaryWithoutLocalReference); Assert.AreEqual(0, result.LinksSkipped);
        }
        var repeated = await inventory.ReadAsync(); Assert.AreEqual(3, repeated.Review.Length);
        Assert.AreEqual(before, await file.Parent.GetAsync(file.Id)); Assert.AreEqual(capture, await file.Captures.GetAsync(file.Id));
        CollectionAssert.AreEqual(artifacts.ToArray(), (await file.Artifacts.GetForRecordingAsync(file.Id)).ToArray());
        CollectionAssert.AreEqual(new byte[] { 3, 4 }, File.ReadAllBytes(file.Paths.ResolveRecordingPath("unassigned.wav")));
        Assert.IsTrue((await catalog.GetPageAsync(local, after)).Items.IsEmpty);
        await Assert.ThrowsAsync<OperationCanceledException>(() => catalog.GetPageAsync(local, cancellationToken: new CancellationToken(true)));
        Assert.Throws<ArgumentException>(() => catalog.GetPageAsync(Guid.Empty)); Assert.Throws<ArgumentException>(() => catalog.GetPageAsync(local, limit: 0));
    }
    private sealed class Identity(Guid id) : IWorkstationIdentity
    { public Task<Guid?> GetAsync(CancellationToken token = default) => Task.FromResult<Guid?>(id); public Task<Guid> InitializeAsync(CancellationToken token = default) => throw new AssertFailedException(); }
}

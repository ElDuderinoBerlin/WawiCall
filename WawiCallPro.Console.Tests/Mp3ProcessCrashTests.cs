using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using CSCore.MediaFoundation;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Database;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Utilities.Persistence;
using WawiCallPro.Utilities.Security;

namespace WawiCallPro.Console.Tests;

[TestClass]
public sealed class Mp3ProcessCrashTests
{
    private const string ChildPoint = "WCP_MP3_CRASH_POINT", ChildRoot = "WCP_MP3_CRASH_ROOT";

    [TestMethod]
    [DataRow("encoding")][DataRow("before-preparation")][DataRow("prepared")][DataRow("published")][DataRow("db-confirmed")]
    public async Task ActualProcessTerminationPreservesTheOriginalAndRecoversExactlyOnce(string point)
    {
        var childPoint = Environment.GetEnvironmentVariable(ChildPoint);
        if (childPoint is not null)
        {
            if (childPoint != point) return; // The child runs only its requested checkpoint, without launching descendants.
            var childRoot = Environment.GetEnvironmentVariable(ChildRoot) ?? throw new AssertFailedException();
            Mp3CrashFixture.ValidateRoot(childRoot);
            await Mp3CrashFixture.RunChildAsync(childRoot, point);
            Assert.Fail("The child must remain at the checkpoint until its parent terminates it.");
        }

        await RunIsolatedAsync(point, null, "SQLite");
    }

    public static async Task RunServerAsync(DatabaseTarget target, DatabaseCreation creation, string engine)
    {
        if (!creation.WasCreated || creation.Type != target.Type || engine is not ("SQL Server" or "MySQL" or "MariaDB" or "PostgreSQL"))
            throw new AssertFailedException("A newly created disposable database and explicit engine are required.");
        Mp3CrashFixture.ValidateServerTarget(target);
        foreach (var point in new[] { "encoding", "before-preparation", "prepared", "published", "db-confirmed" })
            await RunIsolatedAsync(point, target, engine);
    }

    private static async Task RunIsolatedAsync(string point, DatabaseTarget? target, string engine)
    {
        var root = Path.Combine(Path.GetTempPath(), "wcp-mp3-process-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        Process? process = null;
        try
        {
            if (target is not null) await Mp3CrashFixture.ConfigureServerAsync(root, target, engine);
            var assembly = ResolveTestAssembly();
            var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(assembly); start.ArgumentList.Add("--filter");
            start.ArgumentList.Add("FullyQualifiedName~Mp3ProcessCrashTests.ActualProcessTerminationPreservesTheOriginalAndRecoversExactlyOnce");
            start.Environment[ChildPoint] = point; start.Environment[ChildRoot] = root;
            process = Process.Start(start) ?? throw new AssertFailedException("Could not start the isolated fixture process.");
            var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var readyFile = Path.Combine(root, "ready.json");
            while (!File.Exists(readyFile))
            {
                if (process.HasExited) Assert.Fail("Fixture exited before its checkpoint: " + await stdout + await stderr);
                await Task.Delay(50, deadline.Token);
            }
            var signal = JsonSerializer.Deserialize<Mp3CrashSignal>(await File.ReadAllTextAsync(readyFile, deadline.Token))!;
            Assert.AreEqual(point, signal.Point); Assert.AreEqual(process.Id, signal.ProcessId); Assert.AreNotEqual(Environment.ProcessId, signal.ProcessId);
            Assert.AreEqual(engine, signal.Engine);
            Assert.IsFalse(process.HasExited);
            Assert.AreEqual(Path.GetFullPath(assembly), Path.GetFullPath(signal.AssemblyPath));
            Assert.AreEqual(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(assembly)!, "WawiCallPro.Audio.dll")), Path.GetFullPath(signal.AudioAssemblyPath));
            var before = await Mp3CrashFixture.OpenAsync(root);
            Assert.IsTrue(signal.Length > 0);
            Assert.AreEqual(point != "encoding", File.Exists(Path.Combine(root, "encoder-returned")));
            Assert.AreEqual(point is "published" or "db-confirmed", File.Exists(before.Output));
            var jobBefore = (await before.Jobs.GetAsync(signal.Seed.JobId))!;
            var artifactBefore = (await before.Artifacts.GetAsync(signal.Seed.TargetId))!;
            var sourceBefore = (await before.Artifacts.GetAsync(signal.Seed.SourceId))!;
            var recordingBefore = await before.Recordings.GetAsync(signal.Seed.RecordingId);
            var captureBefore = await before.Captures.GetAsync(signal.Seed.RecordingId);
            var protectedCompletion = point is "prepared" or "published" or "db-confirmed";
            Assert.AreEqual(protectedCompletion, before.Conversions.HasPending(signal.Seed.JobId));
            Assert.AreEqual(point == "db-confirmed" ? RecordingConversionState.Completed : RecordingConversionState.Running, jobBefore.Job.State);
            Assert.AreEqual(point == "db-confirmed" ? 3L : 2L, jobBefore.Version);
            Assert.AreEqual(point == "db-confirmed" ? 2L : 1L, artifactBefore.Version);
            Assert.ThrowsExactly<IOException>(() => File.Open(before.Wave, FileMode.Open, FileAccess.Write, FileShare.ReadWrite).Dispose());
            Assert.AreEqual(new InterruptedMp3RecoveryResult(0, point == "db-confirmed" ? 0 : 1, 0), await before.Interrupted(signal.Seed.WorkstationId).RecoverAsync());
            if (protectedCompletion)
                Assert.AreEqual(new RecordingConversionRecoveryResult(0, 1, 0), await before.Recovery(signal.Seed.WorkstationId).RecoverAsync());
            DateTimeOffset? frozenChanged = protectedCompletion ? ReadFrozenTime(before, signal.Seed.JobId) : null;

            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(deadline.Token);
            Assert.AreNotEqual(0, process.ExitCode); await stdout; await stderr;
            var after = await Mp3CrashFixture.OpenAsync(root); // New services, only persisted state crosses the process boundary.
            if (protectedCompletion)
            {
                Assert.AreEqual(new RecordingConversionRecoveryResult(1, 0, 0), await after.Recovery(signal.Seed.WorkstationId).RecoverAsync());
                Assert.AreEqual(new InterruptedMp3RecoveryResult(0, 0, 0), await after.Interrupted(signal.Seed.WorkstationId).RecoverAsync());
            }
            else
            {
                Assert.AreEqual(new RecordingConversionRecoveryResult(0, 0, 0), await after.Recovery(signal.Seed.WorkstationId).RecoverAsync());
                Assert.AreEqual(new InterruptedMp3RecoveryResult(1, 0, 0), await after.Interrupted(signal.Seed.WorkstationId).RecoverAsync());
            }
            var job = (await after.Jobs.GetAsync(signal.Seed.JobId))!; var artifact = (await after.Artifacts.GetAsync(signal.Seed.TargetId))!;
            Assert.AreEqual(3L, job.Version); Assert.AreEqual(2L, artifact.Version);
            Assert.AreEqual(jobBefore.Job.AttemptId, job.Job.AttemptId); Assert.AreEqual(1, job.Job.Attempts);
            Assert.AreEqual(protectedCompletion ? RecordingConversionState.Completed : RecordingConversionState.Failed, job.Job.State);
            Assert.AreEqual(protectedCompletion ? RecordingAvailability.Available : RecordingAvailability.Failed, artifact.Artifact.Availability);
            if (protectedCompletion)
            {
                Assert.AreEqual(frozenChanged, artifact.UpdatedAtUtc); Assert.AreEqual(frozenChanged, job.UpdatedAtUtc);
                Assert.AreEqual(signal.Sha256, artifact.Artifact.Sha256); Assert.AreEqual(signal.Length, artifact.Artifact.ByteLength);
                Assert.AreEqual(signal.Sha256, Digest(after.Output));
                Assert.IsFalse(Directory.EnumerateFiles(after.Paths.RecordingsDirectory, ".wawi-mp3-*.tmp").Any());
                VerifyDecodedAudio(after.Output);
            }
            else
            {
                Assert.AreEqual(RecordingConversionFailure.Interrupted, job.Job.Failure); Assert.IsNull(artifact.Artifact.Sha256);
                Assert.IsFalse(File.Exists(after.Output));
                var retained = after.Paths.ResolveRecordingPath(signal.RelativePath);
                Assert.AreEqual(signal.Length, new FileInfo(retained).Length); Assert.AreEqual(signal.Sha256, Digest(retained));
            }
            Assert.AreEqual(signal.Seed.WaveSha256, Digest(after.Wave));
            Assert.AreEqual(sourceBefore, await after.Artifacts.GetAsync(signal.Seed.SourceId));
            Assert.AreEqual(recordingBefore, await after.Recordings.GetAsync(signal.Seed.RecordingId));
            Assert.AreEqual(captureBefore, await after.Captures.GetAsync(signal.Seed.RecordingId));
            CollectionAssert.AreEqual(new byte[] { 17, 29, 41 }, File.ReadAllBytes(Path.Combine(after.Paths.RecordingsDirectory, "unrelated.tmp")));
            Assert.AreEqual(1, File.ReadAllLines(Path.Combine(root, "encoder-started")).Length);
            Assert.AreEqual(0, after.Conversions.GetPending().Count);
            Assert.AreEqual(new RecordingConversionRecoveryResult(0, 0, 0), await after.Recovery(signal.Seed.WorkstationId).RecoverAsync());
            Assert.AreEqual(new InterruptedMp3RecoveryResult(0, 0, 0), await after.Interrupted(signal.Seed.WorkstationId).RecoverAsync());
            Assert.AreEqual(job, await after.Jobs.GetAsync(signal.Seed.JobId)); Assert.AreEqual(artifact, await after.Artifacts.GetAsync(signal.Seed.TargetId));
            using (File.Open(after.Wave, FileMode.Open, FileAccess.Write, FileShare.None)) { }
            WriteEvidence(signal, process.ExitCode, job, artifact, frozenChanged);
        }
        finally
        {
            if (process is not null)
            {
                if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); }
                process.Dispose();
            }
            Mp3CrashFixture.ValidateRoot(root); Directory.Delete(root, true);
        }
    }

    private static DateTimeOffset ReadFrozenTime(Mp3CrashFixture fixture, Guid job)
    {
        var receipt = Path.Combine(fixture.Paths.RootDirectory, "mp3-pending", job.ToString("N") + ".pending");
        var encrypted = JsonSerializer.Deserialize<ProtectedSecret>(File.ReadAllText(receipt))!;
        using var plain = new WindowsUserSecretProtector().Unprotect(encrypted, "recording-conversion-v1");
        var payload = JsonNode.Parse(plain.Reveal())!; Assert.AreEqual(2, payload["Version"]!.GetValue<int>());
        return payload["Target"]!["ChangedAtUtc"]!.GetValue<DateTimeOffset>();
    }
    private static string ResolveTestAssembly()
    {
        var loaded = typeof(Mp3ProcessCrashTests).Assembly;
        if (File.Exists(Path.ChangeExtension(loaded.Location, ".runtimeconfig.json"))) return loaded.Location;
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "WawiCallProSolution.sln"))) repository = repository.Parent;
        Assert.IsNotNull(repository);
        var configuration = loaded.GetCustomAttributes(typeof(System.Reflection.AssemblyConfigurationAttribute), false)
            .Cast<System.Reflection.AssemblyConfigurationAttribute>().Single().Configuration;
        var original = Path.Combine(repository.FullName, "WawiCallPro.Console.Tests", "bin", configuration, Mp3CrashFixture.ProductLine,
            "net11.0-windows10.0.19041.0", "WawiCallPro.Console.Tests.dll");
        Assert.IsTrue(File.Exists(original)); Assert.AreEqual(Digest(loaded.Location), Digest(original));
        Assert.IsTrue(File.Exists(Path.ChangeExtension(original, ".runtimeconfig.json")));
        return original;
    }

    [TestMethod]
    [DataRow(DatabaseType.MySql, "Server=127.0.0.1;Database=existing;User ID=test")]
    [DataRow(DatabaseType.PostgreSql, "Host=remote.example.test;Database=wcp_schema_0123456789abcdef0123456789abcdef;Username=test")]
    [DataRow(DatabaseType.SqlServer, "Server=(localdb)\\MSSQLLocalDB;Database=wcp_schema_0123456789abcdef0123456789abcdef;Integrated Security=True")]
    public void ExistingOrRemoteTargetsAreRejectedBeforeOpeningAConnection(DatabaseType type, string connection) =>
        Assert.ThrowsExactly<AssertFailedException>(() => Mp3CrashFixture.ValidateServerTarget(DatabaseTarget.Server(type, connection)));
    private static string Digest(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    private static void VerifyDecodedAudio(string path)
    {
        using var decoder = new MediaFoundationDecoder(path);
        Assert.AreEqual(44100, decoder.WaveFormat.SampleRate); Assert.AreEqual(2, decoder.WaveFormat.Channels);
        var buffer = new byte[decoder.WaveFormat.BytesPerSecond / 10]; long bytes = 0; var signal = false; int count;
        while ((count = decoder.Read(buffer, 0, buffer.Length)) != 0) { bytes += count; signal |= buffer.AsSpan(0, count).ContainsAnyExcept((byte)0); }
        Assert.IsTrue(bytes / (double)decoder.WaveFormat.BytesPerSecond is > 0.9 and < 1.2); Assert.IsTrue(signal);
    }
    private static void WriteEvidence(Mp3CrashSignal signal, int exitCode, StoredRecordingConversion job, StoredRecordingArtifact artifact, DateTimeOffset? frozenChanged)
    {
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "WawiCallProSolution.sln"))) repository = repository.Parent;
        Assert.IsNotNull(repository);
        var directory = Path.Combine(repository.FullName, ".artifacts", "mp3-process-crash", Mp3CrashFixture.ProductLine, signal.Engine.Replace(' ', '-')); Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + "-" + signal.Point + ".json"), JsonSerializer.Serialize(new
        {
            Utc = DateTimeOffset.UtcNow, signal.Point, signal.Engine, signal.ProcessId, ParentProcessId = Environment.ProcessId, ExitCode = exitCode,
            signal.AssemblyPath, signal.AudioAssemblyPath, Framework = RuntimeInformation.FrameworkDescription, ProductLine = Mp3CrashFixture.ProductLine,
            signal.Sha256, signal.Length, WaveSha256 = signal.Seed.WaveSha256, FrozenChangedAtUtc = frozenChanged,
            JobState = job.Job.State.ToString(), Failure = job.Job.Failure?.ToString(), job.Job.AttemptId, job.Job.Attempts,
            JobVersion = job.Version, ArtifactVersion = artifact.Version, ArtifactSha256 = artifact.Artifact.Sha256
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}

using System.Text.Json;
using System.Diagnostics;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Telephony.Sip;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Operations;
using WawiCallPro.Utilities.Security;

namespace WawiCallPro.Telephony.Tests;

[TestClass]
public sealed class ProtectedSipCallOutboxTests
{
    [TestMethod]
    public async Task AbruptProcessExitReleasesLockAndPreservesFlushedRecord()
    {
        const string childVariable = "WAWI_OUTBOX_CRASH_TEST_ROOT";
        var childRoot = Environment.GetEnvironmentVariable(childVariable);
        if (childRoot is not null)
        {
            using var journal = new ProtectedSipCallOutbox(new ApplicationPaths(new ConfigurationLocation(childRoot)),
                new WindowsUserSecretProtector(), new("test", "Pro"));
            await journal.OpenAsync(new());
            await journal.AppendAsync(NewCall());
            await File.WriteAllTextAsync(Path.Combine(childRoot, "ready"), "ready");
            await Task.Delay(TimeSpan.FromSeconds(30));
            Assert.Fail("The parent did not terminate the isolated writer.");
            return;
        }
        using var fixture = new Fixture();
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(typeof(ProtectedSipCallOutboxTests).Assembly.Location);
        start.ArgumentList.Add("--filter"); start.ArgumentList.Add("Name~AbruptProcessExitReleasesLockAndPreservesFlushedRecord");
        start.Environment[childVariable] = fixture.Root;
        using var writer = Process.Start(start)!;
        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (!File.Exists(Path.Combine(fixture.Root, "ready")) && !writer.HasExited && DateTime.UtcNow < deadline) await Task.Delay(25);
            Assert.IsTrue(File.Exists(Path.Combine(fixture.Root, "ready")), "The isolated child must flush its record before it is terminated.");
            writer.Kill(entireProcessTree: true);
            await writer.WaitForExitAsync();
            using var restarted = fixture.Create();
            var restored = await restarted.OpenAsync(new());
            Assert.HasCount(1, restored);
            Assert.AreEqual("synthetic-caller", restored[0].RemoteAddress.OriginalValue);
            restarted.Acknowledge(restored[0]);
        }
        finally
        {
            if (!writer.HasExited) { writer.Kill(entireProcessTree: true); await writer.WaitForExitAsync(); }
        }
    }

    [TestMethod]
    public async Task RestartPreservesOrderOriginAndAnsweredDurationWithoutPlaintextFiles()
    {
        using var fixture = new Fixture();
        var ringing = NewCall();
        var connected = ringing with { Revision = 2, State = CallState.Connected, AnsweredAtUtc = ringing.StateChangedAtUtc.AddSeconds(2), StateChangedAtUtc = ringing.StateChangedAtUtc.AddSeconds(2) };
        var ended = connected with { Revision = 3, State = CallState.Ended, EndedAtUtc = connected.StateChangedAtUtc.AddSeconds(7), StateChangedAtUtc = connected.StateChangedAtUtc.AddSeconds(7), EndReason = CallEndReason.RemoteHangup };
        using (var journal = fixture.Create())
        {
            await journal.OpenAsync(new());
            foreach (var call in new[] { ringing, connected, ended }) await journal.AppendAsync(call);
            foreach (var file in Directory.GetFiles(fixture.Pending, "*.pending"))
            {
                var stored = await File.ReadAllTextAsync(file);
                Assert.IsFalse(stored.Contains("synthetic-caller"));
                Assert.IsFalse(stored.Contains("isolated-user"));
                Assert.IsTrue(stored.Contains("dpapi-user-v1:"));
            }
        }
        using var restarted = fixture.Create();
        var restored = await restarted.OpenAsync(new());
        CollectionAssert.AreEqual(new[] { ringing, connected, ended }, restored.ToArray());
        Assert.AreEqual(TimeSpan.FromSeconds(7), restored[^1].EndedAtUtc - restored[^1].AnsweredAtUtc);
        foreach (var call in restored) restarted.Acknowledge(call);
        Assert.IsEmpty(Directory.GetFiles(fixture.Pending, "*.pending"));
    }

    [TestMethod]
    public async Task LockExcludesAnotherHostAndCloseLeavesUnacknowledgedRecords()
    {
        using var fixture = new Fixture();
        using var owner = fixture.Create();
        using var other = fixture.Create();
        await owner.OpenAsync(new());
        await owner.AppendAsync(NewCall());
        await Assert.ThrowsAsync<IOException>(() => other.OpenAsync(new()));
        owner.Close();
        Assert.HasCount(1, await other.OpenAsync(new()));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ChangedDatabaseOrProductCannotConsumePendingCalls(bool changeProduct)
    {
        using var fixture = new Fixture();
        using (var owner = fixture.Create()) { await owner.OpenAsync(new()); await owner.AppendAsync(NewCall()); }
        using (var wrong = fixture.Create(changeProduct ? "FinOffice" : "Pro"))
            await Assert.ThrowsAsync<IOException>(() => wrong.OpenAsync(changeProduct ? new() : new() { SqliteFileName = "other.db" }));
        using var correct = fixture.Create();
        Assert.HasCount(1, await correct.OpenAsync(new()));
    }

    [TestMethod]
    public async Task CorruptLaterRecordDoesNotExposeOrDeleteValidPrefixAndIncompleteTemporaryFileIsIgnored()
    {
        using var fixture = new Fixture();
        using (var owner = fixture.Create())
        {
            await owner.OpenAsync(new());
            await owner.AppendAsync(NewCall()); await owner.AppendAsync(NewCall());
        }
        var files = Directory.GetFiles(fixture.Pending, "*.pending").Order().ToArray();
        var original = await File.ReadAllBytesAsync(files[1]);
        await File.WriteAllTextAsync(files[1], "damaged encrypted record");
        await File.WriteAllTextAsync(Path.Combine(fixture.Pending, "interrupted.tmp"), "incomplete");
        using var restarted = fixture.Create();
        await Assert.ThrowsAsync<IOException>(() => restarted.OpenAsync(new()));
        Assert.HasCount(2, Directory.GetFiles(fixture.Pending, "*.pending"));
        await File.WriteAllBytesAsync(files[1], original);
        Assert.HasCount(2, await restarted.OpenAsync(new()));
    }

    [TestMethod]
    public async Task WrongProtectionPurposeAndOutOfOrderAcknowledgementLeaveRecordsIntact()
    {
        using var fixture = new Fixture();
        var first = NewCall(); var second = NewCall();
        using (var owner = fixture.Create())
        {
            await owner.OpenAsync(new()); await owner.AppendAsync(first); await owner.AppendAsync(second);
            Assert.Throws<IOException>(() => owner.Acknowledge(second));
        }
        var file = Directory.GetFiles(fixture.Pending, "*.pending").Order().First();
        var protector = new WindowsUserSecretProtector();
        await File.WriteAllTextAsync(file, JsonSerializer.Serialize(protector.Protect("synthetic payload", "other-purpose")));
        using var restarted = fixture.Create();
        await Assert.ThrowsAsync<IOException>(() => restarted.OpenAsync(new()));
        Assert.HasCount(2, Directory.GetFiles(fixture.Pending, "*.pending"));
    }

    private static CallSnapshot NewCall()
    {
        var now = DateTimeOffset.UtcNow;
        return new()
        {
            Id = Guid.NewGuid(), Revision = 1, State = CallState.Ringing, Direction = CallDirection.Incoming,
            LocalAddress = new(NumberPresentation.Unknown), RemoteAddress = new(NumberPresentation.Available, "synthetic-caller"),
            Origin = new(Guid.NewGuid(), "isolated-machine", "isolated-user"), StartedAtUtc = now, StateChangedAtUtc = now
        };
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "wawi-outbox-" + Guid.NewGuid().ToString("N"));
        internal string Root => _root;
        internal string Pending => Path.Combine(_root, "sip-pending");
        internal ProtectedSipCallOutbox Create(string product = "Pro") => new(new ApplicationPaths(new ConfigurationLocation(_root)), new WindowsUserSecretProtector(), new("test", product));
        public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
    }
}

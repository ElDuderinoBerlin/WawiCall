using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Common;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Telephony.Sip;

namespace WawiCallPro.Telephony.Tests;

[TestClass]
public sealed class SipCallPersistenceTests
{
    [TestMethod]
    public async Task LostCommitAcknowledgementCanBeRetriedWithoutAppendingAnotherEvent()
    {
        var repository = new Repository { LoseAcknowledgement = true };
        var persistence = Create(repository);
        var call = NewCall();
        await Assert.ThrowsAsync<IOException>(() => persistence.SaveAsync(call));
        var committed = repository.Stored!;

        var retried = await persistence.SaveAsync(call);

        Assert.AreEqual(committed.History.Call, retried);
        Assert.AreSame(committed, repository.Stored);
        Assert.AreEqual(1, repository.Writes);
        Assert.HasCount(1, repository.Stored!.History.Events);
    }

    [TestMethod]
    public async Task DelayedSnapshotCannotOverwriteANewerTerminalState()
    {
        var repository = new Repository();
        var persistence = Create(repository);
        var ringing = await persistence.SaveAsync(NewCall());
        var ended = await persistence.SaveAsync(ringing with
        {
            Revision = 2, State = CallState.Ended, StateChangedAtUtc = ringing.StateChangedAtUtc.AddSeconds(2),
            EndedAtUtc = ringing.StateChangedAtUtc.AddSeconds(2), EndReason = CallEndReason.Missed
        });

        Assert.AreEqual(ended, await persistence.SaveAsync(ringing));
        Assert.AreEqual(2, repository.Writes);
        Assert.HasCount(2, repository.Stored!.History.Events);
    }

    [TestMethod]
    public async Task SameRevisionWithDifferentStateIsStillRejected()
    {
        var repository = new Repository();
        var persistence = Create(repository);
        var saved = await persistence.SaveAsync(NewCall());

        var exception = await Assert.ThrowsAsync<RepositoryConflictException>(() =>
            persistence.SaveAsync(saved with { State = CallState.Dialing }));

        Assert.AreEqual(RepositoryConflictKind.SnapshotMismatch, exception.Kind);
        Assert.AreEqual(1, repository.Writes);
    }

    [TestMethod]
    public async Task RepeatedSnapshotCannotChangeHistoricalOwnership()
    {
        var repository = new Repository();
        var persistence = Create(repository);
        var saved = await persistence.SaveAsync(NewCall());

        var exception = await Assert.ThrowsAsync<RepositoryConflictException>(() => persistence.SaveAsync(saved with
        {
            Origin = new(Guid.NewGuid(), "other-machine", "other-user")
        }));

        Assert.AreEqual(RepositoryConflictKind.OwnershipMismatch, exception.Kind);
        Assert.AreEqual(1, repository.Writes);
    }

    [TestMethod]
    public async Task ConcurrentWriterWithSameRevisionIsRecognizedAfterVersionConflict()
    {
        var repository = new Repository { ConcurrentCommit = true };
        var saved = await Create(repository).SaveAsync(NewCall());

        Assert.AreEqual(repository.Stored!.History.Call, saved);
        Assert.AreEqual(1, repository.Writes);
        Assert.HasCount(1, repository.Stored.History.Events);
    }

    private static SipCallPersistence Create(Repository repository) => new(repository, new OriginProvider(), TimeProvider.System);

    private static CallSnapshot NewCall()
    {
        var now = DateTimeOffset.UtcNow;
        return new()
        {
            Id = Guid.NewGuid(), Revision = 1, Direction = CallDirection.Incoming, State = CallState.Ringing,
            LocalAddress = new(NumberPresentation.Unknown), RemoteAddress = new(NumberPresentation.Withheld),
            StartedAtUtc = now.AddSeconds(-1), StateChangedAtUtc = now
        };
    }

    private sealed class OriginProvider : ICallOriginProvider
    {
        private readonly CallOrigin _origin = new(Guid.NewGuid(), "isolated-machine", "isolated-user");
        public Task<CallOrigin> CaptureAsync(CancellationToken cancellationToken = default) => Task.FromResult(_origin);
    }

    // Inject a commit whose acknowledgement is lost, independently of a particular database engine.
    private sealed class Repository : ICallRepository
    {
        internal StoredCall? Stored;
        internal int Writes;
        internal bool LoseAcknowledgement;
        internal bool ConcurrentCommit;

        public Task<StoredCall?> GetAsync(Guid callId, CancellationToken cancellationToken = default) => Task.FromResult(Stored);
        public Task<Page<CallSnapshot>> GetPageAsync(CallHistoryQuery query, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<StoredCall> WriteAsync(CallWrite write, CancellationToken cancellationToken = default)
        {
            Writes++;
            Stored = new(new()
            {
                Call = write.Call, Parties = write.Parties, Events = (Stored?.History.Events ?? []).AddRange(write.Events),
                Lookup = write.Lookup
            }, (Stored?.Version ?? 0) + 1);
            if (LoseAcknowledgement) throw new IOException("Simulated lost commit acknowledgement.");
            if (ConcurrentCommit) throw new RepositoryConflictException(RepositoryConflictKind.VersionMismatch);
            return Task.FromResult(Stored);
        }
    }
}

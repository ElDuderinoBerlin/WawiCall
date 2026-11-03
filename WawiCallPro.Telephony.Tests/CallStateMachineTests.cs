using Microsoft.VisualStudio.TestTools.UnitTesting;
using WawiCallPro.Interfaces.Telephony;

namespace WawiCallPro.Telephony.Tests;

[TestClass]
public sealed class CallStateMachineTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 3, 12, 0, 0, TimeSpan.FromHours(1));

    private static CallSnapshot NewCall(CallDirection direction = CallDirection.Incoming) => new()
    {
        Id = Guid.NewGuid(),
        Revision = 0,
        Direction = direction,
        State = CallState.Created,
        LocalAddress = new(NumberPresentation.Available, "**620"),
        RemoteAddress = new(NumberPresentation.Withheld),
        StartedAtUtc = Start,
        StateChangedAtUtc = Start
    };

    [TestMethod]
    public void IncomingCallKeepsFirstAnswerTimeAcrossHoldAndResume()
    {
        var machine = new CallStateMachine(NewCall());
        Assert.AreEqual(CallChangeResult.Applied, machine.Apply(new(1, CallState.Ringing, Start)));
        Assert.AreEqual(CallChangeResult.Applied, machine.Apply(new(2, CallState.Connected, Start.AddSeconds(4))));
        Assert.AreEqual(CallChangeResult.Applied, machine.Apply(new(3, CallState.Held, Start.AddSeconds(10))));
        Assert.AreEqual(CallChangeResult.Applied, machine.Apply(new(4, CallState.Connected, Start.AddSeconds(20))));
        Assert.AreEqual(CallChangeResult.Applied, machine.Apply(new(5, CallState.Ended, Start.AddSeconds(34), CallEndReason.Completed)));

        Assert.AreEqual(Start.AddSeconds(4).ToUniversalTime(), machine.Current.AnsweredAtUtc);
        Assert.AreEqual(TimeSpan.FromSeconds(30), machine.Current.EndedAtUtc - machine.Current.AnsweredAtUtc);
        Assert.AreEqual(TimeSpan.Zero, machine.Current.StartedAtUtc.Offset);
        Assert.AreEqual(Start.AddSeconds(34).ToUniversalTime(), machine.Current.StateChangedAtUtc);
    }

    [TestMethod]
    public void OutgoingCallCanConnectWithoutARingingObservation()
    {
        var machine = new CallStateMachine(NewCall(CallDirection.Outgoing));
        Assert.AreEqual(CallChangeResult.Applied, machine.Apply(new(1, CallState.Dialing, Start)));
        Assert.AreEqual(CallChangeResult.Applied, machine.Apply(new(2, CallState.Connected, Start.AddSeconds(1))));
        Assert.AreEqual(CallChangeResult.Applied, machine.Apply(new(3, CallState.Ended, Start.AddSeconds(2), CallEndReason.Transferred)));
    }

    [TestMethod]
    public void DuplicateStaleAndInvalidObservationsLeaveTheSnapshotUnchanged()
    {
        var machine = new CallStateMachine(NewCall());
        Assert.AreEqual(CallChangeResult.InvalidTransition, machine.Apply(new(1, CallState.Dialing, Start)));
        Assert.AreEqual(CallChangeResult.Applied, machine.Apply(new(2, CallState.Ringing, Start)));
        var ringing = machine.Current;
        Assert.AreEqual(CallChangeResult.Stale, machine.Apply(new(1, CallState.Failed, Start, CallEndReason.Error)));
        Assert.AreEqual(CallChangeResult.Duplicate, machine.Apply(new(3, CallState.Ringing, Start.AddSeconds(1))));
        Assert.AreEqual(CallChangeResult.InvalidTransition, machine.Apply(new(4, CallState.Held, Start.AddSeconds(1))));
        Assert.AreEqual(CallChangeResult.InvalidTime, machine.Apply(new(5, CallState.Connected, Start.AddSeconds(-1))));
        Assert.AreEqual(CallChangeResult.InvalidEndReason, machine.Apply(new(6, CallState.Connected, Start, CallEndReason.Completed)));
        Assert.AreSame(ringing, machine.Current);
    }

    [TestMethod]
    public void MissedCallHasNoAnswerTimeAndCannotBeResurrected()
    {
        var machine = new CallStateMachine(NewCall());
        machine.Apply(new(1, CallState.Ringing, Start));
        Assert.AreEqual(CallChangeResult.Applied, machine.Apply(new(2, CallState.Ended, Start.AddSeconds(20), CallEndReason.Missed)));
        var ended = machine.Current;
        Assert.IsNull(ended.AnsweredAtUtc);
        Assert.AreEqual(CallChangeResult.Stale, machine.Apply(new(2, CallState.Ended, Start.AddSeconds(20), CallEndReason.Missed)));
        Assert.AreEqual(CallChangeResult.Terminal, machine.Apply(new(3, CallState.Connected, Start.AddSeconds(21))));
        Assert.AreSame(ended, machine.Current);
    }

    [TestMethod]
    public void EndReasonsMustMatchDirectionAnswerStatusAndTerminalState()
    {
        var incoming = new CallStateMachine(NewCall());
        Assert.AreEqual(CallChangeResult.InvalidEndReason, incoming.Apply(new(1, CallState.Ended, Start, CallEndReason.Completed)));
        Assert.AreEqual(CallChangeResult.InvalidEndReason, incoming.Apply(new(1, CallState.Ended, Start, CallEndReason.Cancelled)));
        Assert.AreEqual(CallChangeResult.InvalidEndReason, incoming.Apply(new(1, CallState.Ended, Start, CallEndReason.NetworkFailure)));
        Assert.AreEqual(CallChangeResult.InvalidEndReason, incoming.Apply(new(1, CallState.Failed, Start, CallEndReason.Missed)));
        Assert.AreEqual(CallChangeResult.InvalidEndReason, incoming.Apply(new(1, CallState.Ended, Start)));
        Assert.AreEqual(CallChangeResult.Applied, incoming.Apply(new(1, CallState.Ended, Start, CallEndReason.Rejected)));

        var outgoing = new CallStateMachine(NewCall(CallDirection.Outgoing));
        Assert.AreEqual(CallChangeResult.InvalidEndReason, outgoing.Apply(new(1, CallState.Ended, Start, CallEndReason.Missed)));
        Assert.AreEqual(CallChangeResult.Applied, outgoing.Apply(new(1, CallState.Ended, Start, CallEndReason.Cancelled)));
    }

    [TestMethod]
    public void RestoredHeldCallRejectsEventsPredatingItsPersistedStateChange()
    {
        var held = NewCall() with
        {
            Revision = 8,
            State = CallState.Held,
            AnsweredAtUtc = Start.AddSeconds(2),
            StateChangedAtUtc = Start.AddSeconds(10)
        };
        var machine = new CallStateMachine(held);
        Assert.AreEqual(CallChangeResult.InvalidTime, machine.Apply(new(9, CallState.Connected, Start.AddSeconds(5))));
        Assert.AreEqual(CallChangeResult.Applied, machine.Apply(new(9, CallState.Failed, Start.AddSeconds(15), CallEndReason.Interrupted)));
        Assert.AreEqual(CallChangeResult.Terminal, machine.Apply(new(10, CallState.Connected, Start.AddSeconds(20))));
    }

    [TestMethod]
    public void InconsistentPersistedSnapshotsAreRejected()
    {
        CallSnapshot[] invalid =
        [
            NewCall() with { Id = Guid.Empty },
            NewCall() with { Revision = -1 },
            NewCall() with { Direction = (CallDirection)999 },
            NewCall() with { State = (CallState)999 },
            NewCall() with { State = CallState.Dialing },
            NewCall() with { State = CallState.Connected },
            NewCall() with { AnsweredAtUtc = Start },
            NewCall() with { StateChangedAtUtc = Start.AddSeconds(-1) },
            NewCall() with { State = CallState.Ended, EndReason = CallEndReason.Missed },
            NewCall() with { EndedAtUtc = Start },
            NewCall() with { State = CallState.Ended, EndedAtUtc = Start.AddSeconds(1), EndReason = CallEndReason.Missed }
        ];
        foreach (var snapshot in invalid)
            Assert.ThrowsExactly<ArgumentException>(() => new CallStateMachine(snapshot));
    }

    [TestMethod]
    public async Task ConcurrentIdenticalObservationsProduceOneAcceptedRevision()
    {
        var machine = new CallStateMachine(NewCall());
        var results = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ =>
            Task.Run(() => machine.Apply(new(1, CallState.Ringing, Start)))));

        Assert.AreEqual(1, results.Count(result => result == CallChangeResult.Applied));
        Assert.AreEqual(15, results.Count(result => result == CallChangeResult.Stale));
        Assert.AreEqual(1L, machine.Current.Revision);
    }
}

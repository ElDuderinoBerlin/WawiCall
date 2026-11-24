using SIPSorcery.SIP.App;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Telephony.Sip;

namespace WawiCallPro.Telephony.Tests;

public sealed partial class SipRuntimeTests
{
    [TestMethod]
    [DataRow("answer")]
    [DataRow("reject")]
    [DataRow("cancel")]
    [DataRow("timeout")]
    [DataRow("disconnect")]
    [DataRow("hangup")]
    public async Task IncomingRingtoneIsReleasedWhenRingingEnds(string ending)
    {
        var ringtone = new RingtoneDouble();
        await using var fixture = await Fixture.CreateAsync(false, ringtone);
        await fixture.Runtime.ConnectAsync();
        await UntilAsync(() => fixture.Runtime.Registration.State == RegistrationState.Registered);

        using var peer = new SIPUserAgent();
        using var peerMedia = new TestMedia();
        var outgoing = peer.Call(fixture.Destination, "caller", "synthetic", peerMedia.Session, 15);
        await UntilAsync(() => fixture.Runtime.GetCalls().SingleOrDefault()?.State == CallState.Ringing);
        var callId = fixture.Runtime.GetCalls().Single().Id;
        Assert.AreEqual(("ring", 35), await ringtone.Started.Task.WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.AreEqual(0, fixture.Media.Created);

        if (ending == "answer")
        {
            var answer = fixture.Runtime.AnswerAsync(callId);
            try
            {
                await ringtone.Stopping.Task.WaitAsync(TimeSpan.FromSeconds(3));
                Assert.IsFalse(answer.IsCompleted);
                Assert.AreEqual(0, fixture.Media.Created);
            }
            finally { ringtone.AllowRelease.TrySetResult(); }
            await answer;
            Assert.IsTrue(await outgoing.WaitAsync(TimeSpan.FromSeconds(3)));
            Assert.AreEqual(1, fixture.Media.Created);
            await fixture.Runtime.HangUpAsync(callId);
        }
        else
        {
            ringtone.AllowRelease.TrySetResult();
            switch (ending)
            {
                case "reject": await fixture.Runtime.RejectAsync(callId); break;
                case "cancel": peer.Cancel(); break;
                case "disconnect": await fixture.Runtime.DisconnectAsync(); break;
                case "hangup": await fixture.Runtime.HangUpAsync(callId); break;
            }
            Assert.IsFalse(await outgoing.WaitAsync(TimeSpan.FromSeconds(8)));
            Assert.AreEqual(0, fixture.Media.Created);
        }

        await UntilAsync(() => fixture.Runtime.GetCalls().Single().State is CallState.Ended or CallState.Failed);
        Assert.AreEqual(1, ringtone.Starts);
        Assert.AreEqual(1, ringtone.Releases);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task MutedOrUnavailableRingtoneDoesNotPreventAnswering(bool failingDevice)
    {
        var ringtone = new RingtoneDouble { FailOnStart = failingDevice };
        await using var fixture = await Fixture.CreateAsync(false, ringtone, failingDevice ? 35 : 0);
        await fixture.Runtime.ConnectAsync();
        await UntilAsync(() => fixture.Runtime.Registration.State == RegistrationState.Registered);
        using var peer = new SIPUserAgent();
        using var peerMedia = new TestMedia();
        var outgoing = peer.Call(fixture.Destination, "caller", "synthetic", peerMedia.Session, 15);
        await UntilAsync(() => fixture.Runtime.GetCalls().SingleOrDefault()?.State == CallState.Ringing);
        if (failingDevice) await ringtone.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await fixture.Runtime.AnswerAsync(fixture.Runtime.GetCalls().Single().Id);
        Assert.IsTrue(await outgoing.WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.AreEqual(failingDevice ? 1 : 0, ringtone.Starts);
        Assert.AreEqual(failingDevice ? 1 : 0, ringtone.Releases);
    }

    private sealed class RingtoneDouble : IIncomingRingtone
    {
        internal bool FailOnStart { get; init; }
        internal int Starts;
        internal int Releases;
        internal readonly TaskCompletionSource<(string? DeviceId, int Volume)> Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Stopping = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource AllowRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task PlayAsync(string? deviceId, int volumePercent, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Starts);
            Started.TrySetResult((deviceId, volumePercent));
            try
            {
                if (FailOnStart) throw new InvalidOperationException("Test device failure");
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            finally
            {
                Stopping.TrySetResult();
                if (!FailOnStart) await AllowRelease.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Interlocked.Increment(ref Releases);
            }
        }
    }
}

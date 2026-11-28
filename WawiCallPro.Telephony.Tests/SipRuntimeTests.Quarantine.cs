using System.Net;
using SIPSorcery.SIP;
using WawiCallPro.Interfaces.Telephony;

namespace WawiCallPro.Telephony.Tests;

public sealed partial class SipRuntimeTests
{
    [TestMethod]
    public async Task PermanentlyRejectedCallIsQuarantinedAndLaterCallsAreStillPersisted()
    {
        await using var fixture = await Fixture.CreateAsync(false, null, acceptInvitesFromAnySource: false);
        fixture.Persistence.Reject = snapshot => snapshot.RemoteAddress.OriginalValue == "+12025550199";
        await fixture.Runtime.ConnectAsync();
        await UntilAsync(() => fixture.Runtime.Registration.State == RegistrationState.Registered);

        var rejected = await RingAsync(fixture, "+12025550199");
        await fixture.Runtime.RejectAsync(rejected);
        await UntilAsync(() => fixture.Runtime.LastFailureCode == "CallSnapshotRejected"
            && fixture.Runtime.GetCalls().Single(call => call.Id == rejected).State == CallState.Ended);

        var accepted = await RingAsync(fixture, "+12025550123");
        await UntilAsync(() => fixture.Persistence.Snapshots.Any(call => call.Id == accepted));
        Assert.IsFalse(fixture.Persistence.Snapshots.Any(call => call.Id == rejected));
        Assert.AreNotEqual("CallStorageUnavailable", fixture.Runtime.LastFailureCode);
        await UntilAsync(() => Directory.GetFiles(fixture.PendingDirectory, "*.pending").Length == 0);
        Assert.IsNotEmpty(Directory.GetFiles(fixture.PendingDirectory, "*.rejected"));
    }

    private static async Task<Guid> RingAsync(Fixture fixture, string number)
    {
        var known = fixture.Runtime.GetCalls().Select(call => call.Id).ToHashSet();
        var target = SIPURI.ParseSIPURI(fixture.Destination);
        var from = new SIPFromHeader(null, SIPURI.ParseSIPURI("sip:" + number + "@example.invalid"), "test-origin-" + Guid.NewGuid().ToString("N"));
        var invite = SIPRequest.GetRequest(SIPMethodsEnum.INVITE, target, new SIPToHeader(null, target, null), from);
        await fixture.RegistrarTransport.SendRequestAsync(new SIPEndPoint(SIPProtocolsEnum.udp, IPAddress.Loopback, fixture.LocalPort), invite);
        await UntilAsync(() => fixture.Runtime.GetCalls().Any(call => !known.Contains(call.Id) && call.State == CallState.Ringing));
        return fixture.Runtime.GetCalls().Single(call => !known.Contains(call.Id)).Id;
    }
}

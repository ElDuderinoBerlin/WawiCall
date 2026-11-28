using System.Net;
using SIPSorcery.SIP;
using WawiCallPro.Interfaces.Telephony;

namespace WawiCallPro.Telephony.Tests;

public sealed partial class SipRuntimeTests
{
    [TestMethod]
    [DataRow(false, false, NumberPresentation.Available)]
    [DataRow(true, false, NumberPresentation.Available)]
    [DataRow(true, true, NumberPresentation.Withheld)]
    public async Task TrustedTelephoneUriSuppliesCallerNumberUnlessPrivacyIsRequested(bool tcp, bool privacy, NumberPresentation expected)
    {
        await using var fixture = await Fixture.CreateAsync();
        if (tcp) await fixture.UseTcpAsync();
        await fixture.Runtime.ConnectAsync();
        await UntilAsync(() => fixture.Runtime.Registration.State == RegistrationState.Registered);
        var target = SIPURI.ParseSIPURI(fixture.Destination + (tcp ? ";transport=tcp" : ""));
        var from = new SIPFromHeader(null, SIPURI.ParseSIPURI("sip:anonymous@anonymous.invalid"), "test-origin");
        var invite = SIPRequest.GetRequest(SIPMethodsEnum.INVITE, target, new SIPToHeader(null, target, null), from);
        invite.Header.UnknownHeaders.Add("P-Asserted-Identity: <tel:+12025550123>");
        if (privacy) invite.Header.UnknownHeaders.Add("Privacy: id");
        var peerPort = tcp ? fixture.LastRegistrationPort : fixture.LocalPort;
        var destination = new SIPEndPoint(tcp ? SIPProtocolsEnum.tcp : SIPProtocolsEnum.udp, IPAddress.Loopback, peerPort);

        await fixture.RegistrarTransport.SendRequestAsync(destination, invite);
        await UntilAsync(() => fixture.Runtime.GetCalls().Any(call => call.State == CallState.Ringing));
        var caller = fixture.Runtime.GetCalls().Single().RemoteAddress;
        Assert.AreEqual(expected, caller.Presentation);
        Assert.AreEqual(privacy ? null : "+12025550123", caller.OriginalValue);
    }

    [TestMethod]
    [DataRow(true, null, NumberPresentation.Available, "+12025550123")]
    [DataRow(true, "id;user", NumberPresentation.Withheld, null)]
    [DataRow(false, null, NumberPresentation.Withheld, null)]
    public async Task IncomingCallerNumberRespectsProxyAndPrivacy(bool registeredPeer, string? privacy,
        NumberPresentation expected, string? number)
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Runtime.ConnectAsync();
        await UntilAsync(() => fixture.Runtime.Registration.State == RegistrationState.Registered);
        var target = SIPURI.ParseSIPURI(fixture.Destination);
        var from = new SIPFromHeader(null, SIPURI.ParseSIPURI("sip:anonymous@anonymous.invalid"), "test-origin");
        var invite = SIPRequest.GetRequest(SIPMethodsEnum.INVITE, target, new SIPToHeader(null, target, null), from);
        invite.Header.PassertedIdentity.Add(new("P-Asserted-Identity", SIPURI.ParseSIPURI("sip:+12025550123@example.invalid")));
        if (privacy is not null) invite.Header.UnknownHeaders.Add("Privacy: " + privacy);
        var destination = new SIPEndPoint(SIPProtocolsEnum.udp, IPAddress.Loopback, fixture.LocalPort);
        var untrusted = new SIPTransport();
        try
        {
            untrusted.AddSIPChannel(new SIPUDPChannel(new IPEndPoint(IPAddress.Loopback, 0)));
            var sender = registeredPeer ? fixture.RegistrarTransport : untrusted;
            await sender.SendRequestAsync(destination, invite);
            await UntilAsync(() => fixture.Runtime.GetCalls().Any(call => call.State == CallState.Ringing));
            var call = fixture.Runtime.GetCalls().Single();
            Assert.AreEqual(expected, call.RemoteAddress.Presentation);
            Assert.AreEqual(number, call.RemoteAddress.OriginalValue);
        }
        finally { untrusted.Shutdown(); }
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task DefaultSettingsAcceptNewInvitesOnlyFromTheRegisteredProxy(bool registeredPeer)
    {
        await using var fixture = await Fixture.CreateAsync(false, null, acceptInvitesFromAnySource: false);
        await fixture.Runtime.ConnectAsync();
        await UntilAsync(() => fixture.Runtime.Registration.State == RegistrationState.Registered);
        var target = SIPURI.ParseSIPURI(fixture.Destination);
        var from = new SIPFromHeader(null, SIPURI.ParseSIPURI("sip:+12025550123@example.invalid"), "test-origin");
        var invite = SIPRequest.GetRequest(SIPMethodsEnum.INVITE, target, new SIPToHeader(null, target, null), from);
        var destination = new SIPEndPoint(SIPProtocolsEnum.udp, IPAddress.Loopback, fixture.LocalPort);
        var scanner = new SIPTransport();
        try
        {
            scanner.AddSIPChannel(new SIPUDPChannel(new IPEndPoint(IPAddress.Loopback, 0)));
            await (registeredPeer ? fixture.RegistrarTransport : scanner).SendRequestAsync(destination, invite);
            if (registeredPeer)
            {
                await UntilAsync(() => fixture.Runtime.GetCalls().Any(call => call.State == CallState.Ringing));
                Assert.AreEqual("+12025550123", fixture.Runtime.GetCalls().Single().RemoteAddress.OriginalValue);
                return;
            }
            // A foreign INVITE is dropped before a call, journal entry or ringtone exists.
            await Task.Delay(TimeSpan.FromMilliseconds(750));
            Assert.IsEmpty(fixture.Runtime.GetCalls());
            Assert.IsEmpty(fixture.Persistence.Snapshots);
            Assert.AreEqual(0, fixture.Media.Created);
        }
        finally { scanner.Shutdown(); }
    }

    [TestMethod]
    [DataRow(256, NumberPresentation.Available)]
    [DataRow(257, NumberPresentation.Unknown)]
    [DataRow(3000, NumberPresentation.Unknown)]
    public async Task OversizedSignalledIdentityIsStoredAsUnknownCaller(int length, NumberPresentation expected)
    {
        await using var fixture = await Fixture.CreateAsync(false, null, acceptInvitesFromAnySource: false);
        await fixture.Runtime.ConnectAsync();
        await UntilAsync(() => fixture.Runtime.Registration.State == RegistrationState.Registered);
        var target = SIPURI.ParseSIPURI(fixture.Destination);
        var user = new string('a', length);
        var from = new SIPFromHeader(null, SIPURI.ParseSIPURI("sip:" + user + "@example.invalid"), "test-origin");
        var invite = SIPRequest.GetRequest(SIPMethodsEnum.INVITE, target, new SIPToHeader(null, target, null), from);
        await fixture.RegistrarTransport.SendRequestAsync(new SIPEndPoint(SIPProtocolsEnum.udp, IPAddress.Loopback, fixture.LocalPort), invite);
        await UntilAsync(() => fixture.Runtime.GetCalls().Any(call => call.State == CallState.Ringing));
        var caller = fixture.Runtime.GetCalls().Single().RemoteAddress;
        Assert.AreEqual(expected, caller.Presentation);
        Assert.AreEqual(expected == NumberPresentation.Available ? user : null, caller.OriginalValue);
        // The journaled observation stays within the persisted address limit.
        await UntilAsync(() => !fixture.Persistence.Snapshots.IsEmpty);
        Assert.IsTrue(fixture.Persistence.Snapshots.All(call => (call.RemoteAddress.OriginalValue?.Length ?? 0) <= 256));
    }
}

using SIPSorcery.SIP;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.Telephony.Tests;

public sealed partial class SipRuntimeTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task KeepAliveUsesRegisteredSocketAndStopsOnDisconnectOrDisabledProfile(bool tcp)
    {
        await using var fixture = await Fixture.CreateAsync();
        if (tcp) await fixture.UseTcpAsync();
        var options = new System.Collections.Concurrent.ConcurrentQueue<int>();
        fixture.RegistrarTransport.SIPTransportRequestReceived += (_, remote, request) =>
        {
            if (request.Method == SIPMethodsEnum.OPTIONS) options.Enqueue(remote.Port);
            return Task.CompletedTask;
        };
        var enabled = Guid.NewGuid();
        var disabled = Guid.NewGuid();
        await fixture.SelectProfileAsync(enabled, enabled, disabled);
        await fixture.SetKeepAliveAsync(1);
        await fixture.Runtime.ConnectAsync();
        await UntilAsync(() => options.Count >= 2);
        Assert.AreEqual(RegistrationState.Registered, fixture.Runtime.Registration.State);
        Assert.IsTrue(options.All(port => port == fixture.LastRegistrationPort));
        Assert.AreEqual(1, fixture.Registers);

        await fixture.Runtime.DisconnectAsync();
        var sent = options.Count;
        await fixture.SelectProfileAsync(disabled, enabled, disabled);
        await fixture.Runtime.ConnectAsync();
        await UntilAsync(() => fixture.Runtime.Registration.State == RegistrationState.Registered);
        await Task.Delay(TimeSpan.FromMilliseconds(1200));
        Assert.AreEqual(sent, options.Count);
    }

    [TestMethod]
    public async Task SavedProfileSelectionDoesNotChangeRegistrationUntilExplicitDisconnectAndReconnect()
    {
        await using var fixture = await Fixture.CreateAsync();
        var first = Guid.NewGuid(); var second = Guid.NewGuid();
        await fixture.SelectProfileAsync(first, first, second);
        await fixture.Runtime.ConnectAsync();
        await UntilAsync(() => fixture.Runtime.Registration.State == RegistrationState.Registered);
        Assert.AreEqual(first, fixture.Runtime.Registration.SipProfileId);
        Assert.AreEqual("workstation", fixture.LastRegisteredUsername);
        var registered = fixture.Runtime.Registration;
        await fixture.SelectProfileAsync(second, first, second);
        Assert.AreSame(registered, fixture.Runtime.Registration, "Saving another account must not affect a running connection.");
        Assert.AreEqual("workstation", fixture.LastRegisteredUsername);
        await fixture.Runtime.ConnectAsync();
        Assert.AreEqual(first, fixture.Runtime.Registration.SipProfileId, "Connect while already registered must retain the original account.");
        await fixture.Runtime.DisconnectAsync();
        Assert.IsNull(fixture.Runtime.Registration.SipProfileId);
        await fixture.Runtime.ConnectAsync();
        await UntilAsync(() => fixture.Runtime.Registration.State == RegistrationState.Registered);
        Assert.AreEqual(second, fixture.Runtime.Registration.SipProfileId);
        Assert.AreEqual("second-user", fixture.LastRegisteredUsername);
        Assert.AreEqual(0, fixture.Media.Created, "Changing accounts must not open audio devices.");
        await fixture.Runtime.DisconnectAsync();
    }
}

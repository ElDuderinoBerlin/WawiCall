using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.Telephony.Tests;

public sealed partial class SipRuntimeTests
{
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

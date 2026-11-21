using WawiCallPro.App.Hosting;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Utilities.Licensing;
using WawiCallPro.Utilities.Plugins;

namespace WawiCallPro.App.Tests;

[TestClass]
public sealed class AppTelephonyCommandTests
{
    [TestMethod]
    public async Task ConcurrentUiAndAutomationCommandsShareOneCallAndOneRegistration()
    {
        await using var f = new ComTelephonyFixture();
        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => f.Service.ConnectAsync()));
        Assert.AreEqual(1, f.Phone.Connects);
        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(async _ =>
        { try { return await f.Service.DialAsync(f.A, "+49301234567"); } catch (TelephonyException e) when (e.Code == "Busy") { return Guid.Empty; } }));
        Assert.AreEqual(1, results.Count(id => id != Guid.Empty)); Assert.AreEqual(1, f.Phone.Attempts);
        Assert.AreEqual(f.A, f.Phone.GetCalls().Single().Company);
    }
    [TestMethod, DataRow(false, false, false, false), DataRow(true, false, false, true), DataRow(false, true, false, true), DataRow(true, true, false, true), DataRow(true, true, true, false)]
    public async Task InstallationGateChecksBothProductsAndFailsClosed(bool pro, bool fin, bool failed, bool allowed)
    {
        await using var f = new ComTelephonyFixture();
        f.Installation.State = new(pro, fin, failed); await f.Service.ConnectAsync();
        if (allowed) await f.Service.DialAsync(f.A, "00123");
        else await Assert.ThrowsExactlyAsync<LexwareUnavailableException>(() => f.Service.DialAsync(f.A, "00123"));
        Assert.AreEqual(allowed ? 1 : 0, f.Phone.Attempts);
        Assert.IsTrue(f.Installation.Checks > 0);
    }
    [TestMethod]
    public async Task LicenseAndSourceRevocationDuringCatalogReadPreventDialButPermitOwnedHangup()
    {
        await using var f = new ComTelephonyFixture(); await f.Service.ConnectAsync();
        f.Source.Catalog = _ => { f.License.Status = LicenseStatus.Revoked; return Task.FromResult(System.Collections.Immutable.ImmutableArray.Create(new Company(f.A, "Firma"))); };
        await Assert.ThrowsExactlyAsync<LicenseAccessException>(() => f.Service.DialAsync(f.A, "00123"));
        Assert.AreEqual(0, f.Phone.Attempts);
        f.License.Status = LicenseStatus.Valid;
        f.Source.Catalog = _ => { f.Source.Available = false; return Task.FromResult(System.Collections.Immutable.ImmutableArray.Create(new Company(f.A, "Firma"))); };
        await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(() => f.Service.DialAsync(f.A, "00123"));
        f.Source.Available = true; f.Source.Catalog = null;
        var id = await f.Service.DialAsync(f.A, "00123");
        f.License.Status = LicenseStatus.Revoked; f.Source.Available = false;
        await f.Service.HangUpAsync(id); Assert.AreEqual(CallState.Ended, f.Phone.GetCalls().Single().State);
    }
    [TestMethod]
    public async Task CompanyIdentityInputRegistrationAndCancellationAreEnforcedBeforeBackend()
    {
        await using var f = new ComTelephonyFixture();
        await Assert.ThrowsExactlyAsync<TelephonyException>(() => f.Service.DialAsync(f.A, "00123"));
        await f.Service.ConnectAsync();
        foreach (var key in new[] { new CompanyKey(f.A.ProductLine, "foreign", "1"),
            new CompanyKey(f.A.ProductLine == LexwareProductLine.Pro ? LexwareProductLine.FinOffice : LexwareProductLine.Pro, f.A.InstallationId, "1") })
            await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(() => f.Service.DialAsync(key, "00123"));
        foreach (var input in new[] { "", "12\r\n34", "abc", "12 34", new string('1', 257) })
            await Assert.ThrowsExactlyAsync<TelephonyException>(() => f.Service.DialAsync(f.A, input));
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => f.Service.DialAsync(f.A, "00123", canceled.Token));
        Assert.AreEqual(0, f.Phone.Attempts);
    }
    [TestMethod]
    public async Task PluginSwitchCannotOvertakeAnAcceptedDial()
    {
        await using var f = new ComTelephonyFixture(); await f.Service.ConnectAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Phone.BeforeDial = _ => { entered.TrySetResult(); return release.Task; };
        var dial = f.Service.DialAsync(f.A, "00123"); await entered.Task;
        var change = f.Source.SwitchAsync("Other.Source"); Assert.IsFalse(change.IsCompleted);
        release.SetResult(); await dial; await change;
        Assert.AreEqual(1, f.Phone.Attempts);
    }
}

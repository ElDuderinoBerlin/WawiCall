using System.Globalization;
using DevExpress.XtraBars;
using DevExpress.XtraEditors;
using Microsoft.Extensions.DependencyInjection;
using WawiCallPro.App.Diagnostics;
using WawiCallPro.App.Hosting;
using WawiCallPro.App.Views;
using WawiCallPro.Interfaces.PhoneNumbers;
using WawiCallPro.Interfaces.Telephony;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    public Task CallerCaptionUsesWindowsRegionForNationalSipNumberWithoutChangingMatching() => WithWorkspace(async (workspace, _, _) =>
    {
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            using var window = new FrmCallerLookup(workspace, null);
            window.PrepareForBackgroundRun();
            var call = CommandCall() with { RemoteAddress = new(NumberPresentation.Available, "0301234567") };
            window.BindCall(call, (_, _) => Task.CompletedTask);
            Assert.Contains("0301234567 (Berlin)", window.Text);
            Assert.AreEqual(PhoneNumberAddressKind.Unresolved, window.Page.InterpretAddress(call.RemoteAddress)?.Kind);
            var shortNumber = call with { RemoteAddress = new(NumberPresentation.Available, "030") };
            window.ObserveCall(shortNumber);
            Assert.Contains("030 ·", window.Text);
            Assert.DoesNotContain("Berlin", window.Text);
            await window.StopAsync();
        }
        finally { CultureInfo.CurrentCulture = culture; }
    }, new ComTelephonyFixture.Backend { Line = new("sip:test", "test") });

    [TestMethod]
    public Task IncomingCallToastPreferenceCanBeSavedIndependentlyOfTheCallerWindow() => WithWorkspace(async (workspace, _, _) =>
    {
        using var settings = new FrmSettings(workspace);
        settings.PrepareForBackgroundRun();
        await settings.PrepareAsync();
        var page = Field<SettingsPage>(settings, "settingsPage");
        var toast = Field<CheckEdit>(page, "incomingCallToastEdit");
        Assert.IsFalse(toast.Checked);
        Field<CheckEdit>(page, "notificationsEdit").Checked = false;
        toast.Checked = true;
        await page.SaveAsync(CancellationToken.None);
        var saved = (await workspace.Settings.LoadAsync()).Options.User;
        Assert.IsTrue(saved.ShowIncomingCallToasts);
        Assert.IsFalse(saved.ShowCallNotifications);
        await settings.StopAsync();
    });

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task CallerWindowPreferenceReusesOnlyEndedWindows(bool reuse) => WithWorkspace(async (workspace, services, _) =>
    {
        await SeedCallerCachesAsync(workspace, services);
        using (var settings = new FrmSettings(workspace))
        {
            settings.PrepareForBackgroundRun();
            var page = Field<SettingsPage>(settings, "settingsPage");
            await settings.PrepareAsync();
            Field<CheckEdit>(page, "reuseCallDialogEdit").Checked = reuse;
            await page.SaveAsync(CancellationToken.None);
            Assert.AreEqual(reuse, (await workspace.Settings.LoadAsync()).Options.User.ReuseCallDialog);
            await settings.StopAsync();
        }
        using var main = new MainForm(workspace.Application, workspace.Configuration, workspace, services.GetRequiredService<AppUiState>());
        main.PrepareForBackgroundRun();
        main.Show();
        var first = (await CreateWorkspaceCallAsync(services)) with { Revision = 2, State = CallState.Ringing };
        await main.ObserveCallAsync(first);
        var original = main.OwnedForms.OfType<FrmCallerLookup>().Single(window => window.CallId == first.Id);
        Assert.IsTrue(original.Visible);
        Assert.Contains("+49301234567 (Berlin)", original.Text);
        Assert.IsFalse(Field<TextEdit>(original.Page, "numberEdit").Visible);

        var second = (await CreateWorkspaceCallAsync(services)) with { Revision = 2, State = CallState.Ringing };
        await main.ObserveCallAsync(second);
        var concurrent = main.OwnedForms.OfType<FrmCallerLookup>().Single(window => window.CallId == second.Id);
        Assert.AreNotSame(original, concurrent);
        concurrent.ObserveCall(second with { RemoteAddress = new(NumberPresentation.Available, E164: "+4940123456") });
        Assert.Contains("+4940123456 (Hamburg)", concurrent.Text);
        concurrent.ObserveCall(second with { RemoteAddress = new(NumberPresentation.Available, "+80012345678") });
        Assert.Contains("+80012345678 ·", concurrent.Text);
        Assert.DoesNotContain("Hamburg", concurrent.Text);
        await main.ObserveCallAsync(first with { Revision = 3, State = CallState.Ended, EndedAtUtc = DateTimeOffset.UtcNow, EndReason = CallEndReason.Completed });
        var status = Field<BarStaticItem>(original, "statusText");
        Assert.Contains("Gespräch beendet", status.Caption);
        Assert.Contains("Die bisherige Auswahl bleibt in der Historie erhalten.", status.Caption);
        Assert.AreSame(Field<Bar>(original, "statusBar"), status.Manager.StatusBar);
        if (reuse)
        {
            var recording = Field<LabelControl>(Field<CallControlPanel>(original, "callControls"), "recordingStatus");
            recording.Text = "Aufnahme gespeichert · Stereo: eigene Stimme links, Gegenstelle rechts.";
            Assert.Contains(recording.Text, status.Caption);
            original.ClientSize = new Size(680, 500);
            original.PerformLayout();
            Application.DoEvents();
            CheckTableBounds(original);
            var output = Path.Combine(RenderOutput(), "caller-window");
            Directory.CreateDirectory(output);
            using var screenshot = UiAudit.Capture(original);
            screenshot.Save(Path.Combine(output, "ended.png"), System.Drawing.Imaging.ImageFormat.Png);
            using var largeFont = new Font(original.Font.FontFamily, 16);
            original.Font = largeFont;
            original.ClientSize = new Size(680, 500);
            original.PerformLayout();
            Application.DoEvents();
            using var largeScreenshot = UiAudit.Capture(original);
            largeScreenshot.Save(Path.Combine(output, "ended-large.png"), System.Drawing.Imaging.ImageFormat.Png);
        }

        var third = (await CreateWorkspaceCallAsync(services)) with
        {
            Revision = 2, State = CallState.Ringing, RemoteAddress = new(NumberPresentation.Withheld, "030555555")
        };
        await main.ObserveCallAsync(third);
        var selected = main.OwnedForms.OfType<FrmCallerLookup>().Single(window => window.CallId == third.Id);
        Assert.AreEqual(reuse, ReferenceEquals(original, selected));
        Assert.Contains("Rufnummer unterdrückt", selected.Text);
        Assert.DoesNotContain("030555555", selected.Text);
        Assert.DoesNotContain("Berlin", selected.Text);
        Assert.DoesNotContain("Gespräch beendet", Field<BarStaticItem>(selected, "statusText").Caption);
        Assert.AreEqual(second.Id, concurrent.CallId);
        await main.PrepareToCloseAsync();
    });
}

using System.Reflection;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using WawiCallPro.App.Views;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.App.Tests;

public partial class AppShellTests
{
    [TestMethod]
    public Task SipDatabasePrerequisiteFailureDoesNotBlameCredentials() => WithWorkspace(async (workspace, _, _) =>
    {
        var backend = (ComTelephonyFixture.Backend)workspace.Telephony!;
        backend.ConnectFailure = new TelephonyException("DatabaseNotReady");
        using var settings = new FrmSettings(workspace);
        settings.PrepareForBackgroundRun();
        settings.ClientSize = new Size(760, 700);
        settings.Show();
        var page = Field<SettingsPage>(settings, "settingsPage");
        await page.ReloadAsync();
        settings.SelectAutomationTab("settings-sip");
        Field<TextEdit>(page, "serverEdit").Text = "sip.example.invalid";
        Field<TextEdit>(page, "usernameEdit").Text = "isolated-test-user";
        var password = Field<ButtonEdit>(page, "passwordEdit");
        password.Text = "isolated-database-test-secret";
        await InvokeActionAsync(page, "SipConnect_Click");
        Application.DoEvents();
        var status = Field<LabelControl>(page, "sipStatus");
        StringAssert.Contains(status.Text, "Die SIP-Anmeldung wurde nicht gestartet");
        StringAssert.Contains(status.Text, "Datenbank → Datenbank einrichten");
        Assert.AreNotEqual(WawiCallPro.Utilities.Operations.SettingsMessages.Get(WawiCallPro.Utilities.Operations.OperationEvent.SettingsFailed), page.LastSettingsNoticeText);
        Assert.AreEqual("isolated-database-test-secret", password.Text);
        Assert.IsTrue(password.Properties.UseSystemPasswordChar);
        Assert.IsTrue(Field<SimpleButton>(page, "sipConnectButton").Enabled);
        CheckTableBounds(settings);
        var directory = Path.Combine(RenderOutput(), "sip-database-status");
        Directory.CreateDirectory(directory);
        using (var bitmap = WawiCallPro.App.Diagnostics.UiAudit.Capture(settings))
            bitmap.Save(Path.Combine(directory, "database-not-ready.png"), System.Drawing.Imaging.ImageFormat.Png);
        await settings.StopAsync();
    }, new ComTelephonyFixture.Backend());

    [TestMethod]
    public Task SipPasswordSurvivesFailedRetriesAndDelayedRegistrationFailure() => WithWorkspace(async (workspace, _, _) =>
    {
        var backend = (ComTelephonyFixture.Backend)workspace.Telephony!;
        using var settings = new FrmSettings(workspace);
        var page = Field<SettingsPage>(settings, "settingsPage");
        _ = page.Handle;
        await page.ReloadAsync();
        Field<TextEdit>(page, "serverEdit").Text = "sip.example.invalid";
        Field<TextEdit>(page, "usernameEdit").Text = "isolated-test-user";
        var password = Field<ButtonEdit>(page, "passwordEdit");
        const string draft = "isolated-password-retry";
        password.Text = draft;
        backend.ConnectFailure = new TelephonyException("RegistrationFailed");
        for (var attempt = 0; attempt < 2; attempt++)
        {
            await InvokeActionAsync(page, "SipConnect_Click");
            Assert.AreEqual(draft, password.Text);
            Assert.IsTrue(password.Properties.UseSystemPasswordChar);
            Assert.IsTrue(Field<SimpleButton>(page, "sipConnectButton").Enabled);
            using var saved = workspace.Secrets.Unprotect((await workspace.Settings.LoadAsync()).Options.SipAccount!.Password!, SipAccountOptions.PasswordPurpose);
            Assert.AreEqual(draft, saved.Reveal());
        }
        backend.ConnectFailure = null;
        backend.RegisterOnConnect = false;
        await InvokeActionAsync(page, "SipConnect_Click");
        backend.Register(RegistrationState.Failed);
        Application.DoEvents();
        Assert.AreEqual(draft, password.Text);
        Assert.IsFalse(page.LastSettingsNoticeText.Contains(draft, StringComparison.Ordinal));
        await settings.StopAsync();
        Assert.AreEqual("", password.Text);
        Assert.IsTrue(password.Properties.UseSystemPasswordChar);
    }, new ComTelephonyFixture.Backend());

    [TestMethod]
    public Task SipPasswordEyeRevealsOnlyOnRequestAndPreservesDraftAndSelection() => WithWorkspace(async (workspace, _, _) =>
    {
        using var settings = new FrmSettings(workspace);
        settings.PrepareForBackgroundRun();
        settings.Show();
        var page = Field<SettingsPage>(settings, "settingsPage");
        await page.ReloadAsync();
        settings.SelectAutomationTab("settings-sip");
        Application.DoEvents();
        Field<TextEdit>(page, "serverEdit").Text = "sip.example.invalid";
        Field<TextEdit>(page, "usernameEdit").Text = "isolated-test-user";
        var password = Field<ButtonEdit>(page, "passwordEdit");
        password.Text = "isolated-eye-secret";
        await page.SaveAsync(CancellationToken.None);
        Assert.AreEqual("", password.Text);
        Assert.IsTrue(password.Properties.UseSystemPasswordChar);
        Assert.AreEqual("Kennwort gespeichert", password.Properties.NullValuePrompt);
        Assert.AreEqual("sip.example.invalid", Field<TextEdit>(page, "serverEdit").Text);
        Assert.AreEqual("isolated-test-user", Field<TextEdit>(page, "usernameEdit").Text);
        var before = workspace.Configuration.Current;
        void Toggle() => typeof(SettingsPage).GetMethod("PasswordVisibility_Click", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(page, [password, new ButtonPressedEventArgs(password.Properties.Buttons[0])]);
        Toggle();
        Assert.AreEqual("isolated-eye-secret", password.Text);
        Assert.IsFalse(password.Properties.UseSystemPasswordChar);
        _ = password.Handle;
        password.SelectionStart = 3;
        password.SelectionLength = 4;
        Toggle();
        Assert.IsTrue(password.Properties.UseSystemPasswordChar);
        Assert.AreEqual(3, password.SelectionStart);
        Assert.AreEqual(4, password.SelectionLength);
        password.Text = "corrected-eye-secret";
        Toggle();
        Assert.AreEqual("corrected-eye-secret", password.Text);
        Assert.AreSame(before, workspace.Configuration.Current, "Visibility must not save settings.");
        await page.ReloadAsync();
        Assert.AreEqual("", password.Text);
        Assert.IsTrue(password.Properties.UseSystemPasswordChar);
        await settings.StopAsync();
        Assert.AreEqual("", password.Text);
        Assert.IsTrue(password.Properties.UseSystemPasswordChar);
    });
}

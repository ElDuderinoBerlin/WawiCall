using System.Reflection;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using DevExpress.XtraBars;
using DevExpress.XtraBars.Ribbon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WawiCallPro.App.Hosting;
using WawiCallPro.App.Views;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Operations;

namespace WawiCallPro.App.Tests;

public partial class AppShellTests
{
    [TestMethod]
    public Task SipProfileCanBeRegisteredFromMainRibbonWithoutOpeningSettings() => WithWorkspace(async (workspace, services, _) =>
    {
        var backend = (ComTelephonyFixture.Backend)workspace.Telephony!;
        var log = new LogWindowService();
        using var main = new MainForm(workspace.Application, workspace.Configuration, workspace,
            services.GetRequiredService<AppUiState>(), log);
        main.PrepareForBackgroundRun();
        main.Show();
        var action = Field<BarButtonItem>(main, "sipConnectItem");
        Assert.IsTrue(action.Enabled);
        Assert.AreEqual("SIP-Konto anmelden", action.Caption);
        Assert.IsNotNull(action.ImageOptions.SvgImage);
        Assert.IsTrue(Field<RibbonPageGroup>(main, "telephonyGroup").ItemLinks.Any(link => link.Item == action));
        var renders = Path.Combine(RenderOutput(), "sip-main-ribbon");
        Directory.CreateDirectory(renders);
        main.ClientSize = new Size(1080, 720);
        using (var shot = WawiCallPro.App.Diagnostics.UiAudit.Capture(main))
            shot.Save(Path.Combine(renders, workspace.Application.ProductLine + ".png"), System.Drawing.Imaging.ImageFormat.Png);
        action.PerformClick();
        Assert.AreEqual(1, backend.Connects);
        Assert.AreEqual(0, backend.Attempts);
        Assert.IsFalse(action.Enabled);
        Assert.IsFalse(main.OwnedForms.OfType<FrmSettings>().Any());
        Assert.AreEqual("SIP: Angemeldet", Field<BarStaticItem>(main, "telephonyStatusItem").Caption);
        await backend.DisconnectAsync();
        await main.InvokeAsync(() => Assert.IsTrue(action.Enabled));
    }, telephony: new ComTelephonyFixture.Backend());

    [TestMethod]
    [DataRow(OperationEvent.SipRegistrationStarted, "SIP-Anmeldung läuft …")]
    [DataRow(OperationEvent.SipRegistrationSucceeded, "SIP-Konto angemeldet.")]
    [DataRow(OperationEvent.SipRegistrationStopped, "SIP-Konto nicht angemeldet.")]
    public void SipProfileStatusAppearsInOutput(OperationEvent operation, string message)
    {
        var logs = new LogWindowService();
        using var provider = new UiLogProvider(logs);
        OperationEvents.Write(provider.CreateLogger("SIP"), operation);
        var entry = logs.Drain().Single();
        Assert.AreEqual(LogLevel.Information, entry.Level);
        Assert.AreEqual(message, entry.Message);
    }

    [TestMethod]
    public Task SipProfilesKeepPasswordsAndAdvancedOptionsSeparateWithoutReconnecting() => WithWorkspace(async (workspace, _, plugin) =>
    {
        var backend = (ComTelephonyFixture.Backend)workspace.Telephony!;
        using var form = new FrmSettings(workspace);
        form.PrepareForBackgroundRun();
        form.Show();
        var page = Field<SettingsPage>(form, "settingsPage");
        await page.ReloadAsync();
        form.SelectAutomationTab("settings-sip");
        var selector = Field<LookUpEdit>(page, "sipProfileEdit");
        var password = Field<ButtonEdit>(page, "passwordEdit");
        var server = Field<TextEdit>(page, "serverEdit");
        var user = Field<TextEdit>(page, "usernameEdit");
        void Action(string method) => typeof(SettingsPage).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(page, [null, EventArgs.Empty]);
        void Toggle() => typeof(SettingsPage).GetMethod("PasswordVisibility_Click", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(page, [password, new ButtonPressedEventArgs(password.Properties.Buttons[0])]);
        Field<TextEdit>(page, "sipProfileNameEdit").Text = "Büro";
        server.Text = "office.invalid"; user.Text = "office-user"; password.Text = "synthetic-office-secret";
        var firstId = (Guid)selector.EditValue;
        var properties = Field<DevExpress.XtraVerticalGrid.PropertyGridControl>(page, "sipProperties");
        var advanced = (SipAdvancedSettings)properties.SelectedObject;
        advanced.RegistrationExpirySeconds = 240;
        advanced.KeepAliveIntervalSeconds = 15;
        var sipPages = Field<DevExpress.XtraTab.XtraTabControl>(page, "sipPages");
        sipPages.SelectedTabPage = Field<DevExpress.XtraTab.XtraTabPage>(page, "sipAdvanced");
        Application.DoEvents();
        var codecRow = properties.GetRowByFieldName(nameof(SipAdvancedSettings.CodecOrder));
        properties.FocusedRow = codecRow;
        properties.ShowEditor();
        Assert.IsInstanceOfType<ComboBoxEdit>(properties.ActiveEditor);
        var editor = (ComboBoxEdit)properties.ActiveEditor;
        Assert.AreEqual(TextEditStyles.DisableTextEditor, editor.Properties.TextEditStyle);
        editor.SelectedIndex = editor.Properties.Items.IndexOf("PCMA, PCMU");
        properties.CloseEditor();
        Assert.AreEqual("PCMA, PCMU", advanced.CodecOrder);
        Assert.AreEqual(SipDtmfMethod.Rtp, advanced.DtmfMethod);
        properties.FocusedRow = properties.GetRowByFieldName(nameof(SipAdvancedSettings.DtmfMethod));
        properties.ShowEditor();
        Assert.IsInstanceOfType<ImageComboBoxEdit>(properties.ActiveEditor);
        properties.ActiveEditor.EditValue = SipDtmfMethod.SipInfo;
        properties.CloseEditor();
        Assert.AreEqual(SipDtmfMethod.SipInfo, advanced.DtmfMethod);
        var directory = Path.Combine(RenderOutput(), "sip-profiles"); Directory.CreateDirectory(directory);
        using (var image = WawiCallPro.App.Diagnostics.UiAudit.Capture(form)) image.Save(Path.Combine(directory, "expert-configured.png"));
        var originalSize = form.ClientSize;
        var originalFont = form.Font;
        var originalEditorFont = WindowsFormsSettings.DefaultFont;
        var originalAppearanceFont = DevExpress.Utils.AppearanceObject.DefaultFont;
        using (var largeFont = new Font(originalFont.FontFamily, originalFont.SizeInPoints * 1.4f, originalFont.Style))
        {
            try
            {
                WindowsFormsSettings.DefaultFont = DevExpress.Utils.AppearanceObject.DefaultFont = largeFont;
                form.Font = largeFont;
                form.ClientSize = new Size(700, 560);
                Application.DoEvents();
                using var image = WawiCallPro.App.Diagnostics.UiAudit.Capture(form);
                image.Save(Path.Combine(directory, "expert-large-font.png"));
            }
            finally
            {
                WindowsFormsSettings.DefaultFont = originalEditorFont;
                DevExpress.Utils.AppearanceObject.DefaultFont = originalAppearanceFont;
                form.Font = originalFont;
                form.ClientSize = originalSize;
            }
        }
        sipPages.SelectedTabPageIndex = 0;
        Action("AddSipProfile_Click");
        var secondId = (Guid)selector.EditValue;
        Assert.AreNotEqual(firstId, secondId);
        Assert.AreEqual("", server.Text); Assert.AreEqual("", user.Text); Assert.AreEqual("", password.Text);
        Toggle(); Assert.AreEqual("", password.Text, "A new profile must not reveal the first profile's stored password.");
        Field<TextEdit>(page, "sipProfileNameEdit").Text = "Außendienst";
        server.Text = "mobile.invalid"; user.Text = "mobile-user"; password.Text = "synthetic-mobile-secret";
        selector.EditValue = firstId;
        Assert.AreEqual("office.invalid", server.Text); Assert.AreEqual("office-user", user.Text);
        Assert.AreEqual("", password.Text); Assert.IsTrue(password.Properties.UseSystemPasswordChar);
        advanced = (SipAdvancedSettings)Field<DevExpress.XtraVerticalGrid.PropertyGridControl>(page, "sipProperties").SelectedObject;
        Assert.AreEqual(240, advanced.RegistrationExpirySeconds);
        Assert.AreEqual(15, advanced.KeepAliveIntervalSeconds);
        Assert.AreEqual("PCMA, PCMU", advanced.CodecOrder);
        Assert.AreEqual(SipDtmfMethod.SipInfo, advanced.DtmfMethod);
        Assert.IsNull(page.ReadOptions().CallerLineAssignment);
        Toggle(); Assert.AreEqual("synthetic-office-secret", password.Text);
        selector.EditValue = secondId;
        Assert.AreEqual("", password.Text); Assert.IsTrue(password.Properties.UseSystemPasswordChar);
        Toggle(); Assert.AreEqual("synthetic-mobile-secret", password.Text);
        backend.Register(RegistrationState.Registered);
        await page.SaveAsync(CancellationToken.None);
        Assert.AreEqual(0, backend.Connects); Assert.AreEqual(0, backend.Disconnects);
        Assert.AreEqual(RegistrationState.Registered, backend.Registration.State);
        var saved = (await workspace.Settings.LoadAsync()).Options;
        Assert.AreEqual(2, saved.SipProfiles.Length); Assert.AreEqual(secondId, saved.ActiveSipProfileId);
        Assert.AreEqual("mobile.invalid", saved.SipAccount!.Server);
        Assert.AreEqual(0, saved.SipAccount.Connection.KeepAliveIntervalSeconds);
        Assert.IsNull(saved.SipAccount.Connection.CodecOrder);
        Assert.AreEqual(SipDtmfMethod.Rtp, saved.SipAccount.Connection.DtmfMethod);
        var firstConnection = saved.SipProfiles.Single(profile => profile.Id == firstId).Account!.Connection;
        Assert.AreEqual(15, firstConnection.KeepAliveIntervalSeconds);
        Assert.AreEqual("PCMA, PCMU", firstConnection.CodecOrder);
        Assert.AreEqual(SipDtmfMethod.SipInfo, firstConnection.DtmfMethod);
        Assert.IsTrue(SipProfileCatalog.Read(saved).All(profile => profile.LineAssignment is null));
        Application.DoEvents(); CheckTableBounds(form);
        using (var image = WawiCallPro.App.Diagnostics.UiAudit.Capture(form)) image.Save(Path.Combine(directory, "two-profiles.png"));
        await InvokeActionAsync(page, "SipDisconnect_Click");
        await InvokeActionAsync(page, "SipConnect_Click");
        Assert.AreEqual(1, backend.Connects); Assert.AreEqual(1, backend.Disconnects);
        await form.StopAsync();
    }, new ComTelephonyFixture.Backend());

    [TestMethod]
    public Task SipProfileDeletionStaysInDraftUntilSavedAndLastDeletionClearsAccount() => WithWorkspace(async (workspace, _, _) =>
    {
        using var form = new FrmSettings(workspace);
        var page = Field<SettingsPage>(form, "settingsPage");
        await page.ReloadAsync();
        Field<TextEdit>(page, "serverEdit").Text = "deletion.invalid";
        Field<TextEdit>(page, "usernameEdit").Text = "test-user";
        Field<ButtonEdit>(page, "passwordEdit").Text = "synthetic-delete-secret";
        await page.SaveAsync(CancellationToken.None);
        void Remove() => typeof(SettingsPage).GetMethod("RemoveSipProfile_Click", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(page, [null, EventArgs.Empty]);
        Remove();
        Assert.IsNotNull((await workspace.Settings.LoadAsync()).Options.SipAccount, "Removing is only an unsaved draft.");
        await page.ReloadAsync();
        Assert.AreEqual("deletion.invalid", Field<TextEdit>(page, "serverEdit").Text);
        Remove(); await page.SaveAsync(CancellationToken.None);
        var after = (await workspace.Settings.LoadAsync()).Options;
        Assert.IsNull(after.SipAccount); Assert.IsNull(after.ActiveSipProfileId); Assert.IsTrue(after.SipProfiles.IsEmpty);
        Assert.AreEqual("", Field<ButtonEdit>(page, "passwordEdit").Text);
        await form.StopAsync();
    });
}

using System.Collections.Immutable;
using System.Drawing.Imaging;
using System.IO;
using DevExpress.Utils;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraTab;
using WawiCallPro.App.Hosting;
using WawiCallPro.App.Views;
using WawiCallPro.Interfaces.Office;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Hosting;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    public Task MailAttachmentActionsRenderAndRemainExplicitAtLargeFonts() => WithWorkspace(async (workspace, _, _) =>
    {
        var output = Path.Combine(RenderOutput(), "mail-settings"); Directory.CreateDirectory(output);
        foreach (var variant in new[] { (Width: 1240, Font: 10F), (Width: 760, Font: 16F) })
        {
            using var font = new Font("Segoe UI", variant.Font); using var host = new MainForm(); host.PrepareForBackgroundRun();
            using var page = new MailPage { Font = font, Size = new(variant.Width, 900) }; page.Bind(new MailSourceDouble(), null); page.ConfigureMailActions(workspace.Mail, false);
            host.Controls.Add(page); host.ClientSize = page.Size; host.Show(); CreateHandles(page); await page.ReloadAsync(); page.PerformLayout();
            Assert.IsTrue(Field<SimpleButton>(page, "saveAttachmentButton").Visible);
            Assert.AreEqual("synthetic-file", Field<LookUpEdit>(page, "attachmentEdit").EditValue); CheckVisibleMailBounds(page);
            using var image = WawiCallPro.App.Diagnostics.UiAudit.CaptureMail(page); image.Save(Path.Combine(output, $"mail-attachments-{variant.Width}-{variant.Font:0}.png"), ImageFormat.Png);
            await page.StopAsync();
        }
    });

    [TestMethod]
    public Task MailCallTabsKeepTheirAcceptedPartyContextAndStopWithTheCall() => WithWorkspace(async (workspace, services, _) =>
    {
        await SeedCallerCachesAsync(workspace, services); var decision = await BuildWorkspaceDecisionAsync(workspace);
        using var host = new FrmCallerLookup(); host.PrepareForBackgroundRun(); host.ClientSize = new(1240, 840);
        foreach (Control child in host.Controls.Cast<Control>().ToArray()) child.Dispose(); host.Controls.Clear();
        using var page = new CallDetailsPage { Dock = DockStyle.Fill }; page.Bind(workspace, await CreateWorkspaceCallAsync(services)); host.Controls.Add(page); host.Show(); CreateHandles(page);
        var selected = decision.Groups[1].Candidates.First().Party;
        await page.AcceptAsync(workspace.CallerDecisions.Choose(decision, selected), CancellationToken.None); await page.PendingLoads;
        var tabs = Field<XtraTabControl>(page, "tabs"); var mailTab = tabs.TabPages.Cast<XtraTabPage>().Single(tab => tab.Controls.OfType<MailPage>().Any());
        var context = (WawiCallPro.Interfaces.PhoneNumbers.CallViewContext)mailTab.Tag!;
        Assert.AreEqual(selected, context.Party); Assert.Contains(context.CompanyName, mailTab.Text);
        tabs.SelectedTabPage = mailTab; host.PerformLayout();
        var output = Path.Combine(RenderOutput(), "mail-settings"); Directory.CreateDirectory(output);
        using var image = WawiCallPro.App.Diagnostics.UiAudit.Capture(host); image.Save(Path.Combine(output, "mail-call-context.png"), ImageFormat.Png);
        await page.StopAsync();
    });
    [TestMethod]
    public Task MailSettingsPreserveSecretsFolderSelectionAndOtherGlobalDrafts() => WithWorkspace(async (workspace, _, _) =>
    {
        using var page = new SettingsPage(); page.Bind(workspace); CreateHandles(page);
        var account = new MailAccountSettings { Id = Guid.NewGuid(), DisplayName = "Synthetic mailbox", Enabled = false, Provider = MailProviderKind.Microsoft365,
            ClientId = Guid.NewGuid().ToString(), ApplicationAuthentication = true, Tenant = Guid.NewGuid().ToString(), Mailbox = "mail@example.test",
            MicrosoftClientSecret = workspace.Secrets.Protect("synthetic-private-secret", MailAccountSettings.MicrosoftSecretPurpose),
            FolderIds = ["inbox"], FolderNames = ImmutableDictionary<string, string>.Empty.Add("inbox", "Posteingang") };
        page.Apply(new(workspace.Configuration.Current.Options with { MailAccounts = [account] }, false));
        var mail = Field<MailSettingsPage>(page, "mailSettingsPage");
        var draft = page.ReadOptions(); Assert.AreEqual(account.MicrosoftClientSecret, draft.MailAccounts[0].MicrosoftClientSecret);
        Assert.AreEqual("", Field<TextEdit>(mail, "secretEdit").Text);
        Field<GridView>(mail, "foldersView").ClearSelection(); Field<CheckEdit>(page, "notificationsEdit").Checked = false;
        draft = page.ReadOptions(); Assert.IsTrue(draft.MailAccounts[0].FolderIds.IsEmpty); Assert.IsFalse(draft.User.ShowCallNotifications);
        Assert.IsTrue(draft.MailAccounts[0].FolderNames.IsEmpty);
        var original = Field<ConfigurationSnapshot>(page, "_snapshot");
        var external = await workspace.Settings.LoadAsync();
        await workspace.Settings.SaveAsync(external.Options with { User = external.Options.User with { ShowCallNotifications = !external.Options.User.ShowCallNotifications } }, external);
        var refresh = (Task)typeof(SettingsPage).GetMethod("ReloadMailSettingsAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(page, [CancellationToken.None])!;
        await Assert.ThrowsExactlyAsync<MailException>(() => refresh); Assert.AreSame(original, Field<ConfigurationSnapshot>(page, "_snapshot"));
        await page.StopAsync();
    });

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    public Task MailSettingsRenderEmptyOutlookMicrosoftAndGoogleAtSmallBoundsAndLargeFonts(int provider) => WithWorkspace(async (workspace, _, _) =>
    {
        AppUiStartup.Apply();
        var originalFont = WindowsFormsSettings.DefaultFont; var originalAppearance = AppearanceObject.DefaultFont;
        var originalSkin = DevExpress.LookAndFeel.UserLookAndFeel.Default.ActiveSkinName;
        var originalPalette = DevExpress.LookAndFeel.UserLookAndFeel.Default.ActiveSvgPaletteName;
        var output = Path.Combine(RenderOutput(), "mail-settings"); Directory.CreateDirectory(output);
        try
        {
            foreach (var variant in new[] { (Width: 1100, Font: 10F, Scale: 1F, Skin: "Office 2019 Colorful"), (Width: 900, Font: 16F, Scale: 1F, Skin: "Office 2019 Black"), (Width: 1100, Font: 10F, Scale: 1.5F, Skin: "Office 2019 Colorful") })
            {
                if (variant.Skin == "Basic") DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(variant.Skin, "Blue Dark"); else DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(variant.Skin);
                using var font = new Font("Segoe UI", variant.Font); WindowsFormsSettings.DefaultFont = AppearanceObject.DefaultFont = font;
                using var form = new FrmSettings(); form.PrepareForBackgroundRun(); var page = Field<SettingsPage>(form, "settingsPage"); page.Bind(workspace);
                var accounts = provider == 0 ? ImmutableArray<MailAccountSettings>.Empty : [new MailAccountSettings
                {
                    Id = Guid.NewGuid(), Provider = (MailProviderKind)provider, DisplayName = "Vertrieb und Kundenbetreuung", Enabled = false,
                    ClientId = "synthetic-client-id", Tenant = "common", Mailbox = "mail@example.test", ApplicationAuthentication = provider == 2,
                    FolderIds = ["inbox", "sent"], FolderNames = ImmutableDictionary<string, string>.Empty.Add("inbox", "Posteingang").Add("sent", "Gesendete Nachrichten")
                }];
                form.Font = font; form.Show(); await page.ReloadAsync();
                page.Apply(new(workspace.Configuration.Current.Options with { MailAccounts = accounts }, false));
                form.MinimumSize = Size.Empty; form.ClientSize = new(variant.Width, 800);
                Field<XtraTabControl>(page, "tabs").SelectedTabPage = Field<XtraTabPage>(page, "mailTab");
                CreateHandles(form); form.PerformLayout();
                if (variant.Scale != 1F) form.Scale(new SizeF(variant.Scale, variant.Scale));
                form.PerformLayout(); CheckVisibleMailBounds(form);
                var cacheControls = Field<MailSettingsPage>(page, "mailSettingsPage");
                var cacheEditor = Field<SpinEdit>(cacheControls, "cacheDaysEdit");
                Assert.IsTrue(cacheEditor.Width >= 100, $"Cache duration clipped: {cacheEditor.Bounds}, parent {cacheEditor.Parent!.Bounds}");
                using var image = WawiCallPro.App.Diagnostics.UiAudit.Capture(form); image.Save(Path.Combine(output, $"mail-settings-{provider}-{variant.Width}-{variant.Font:0}-{variant.Scale:0.0}.png"), ImageFormat.Png);
                if (provider != 0)
                {
                    var mail = Field<MailSettingsPage>(page, "mailSettingsPage");
                    foreach (var name in new[] { "applicationEdit", "startupEdit" })
                    {
                        var check = Field<CheckEdit>(mail, name);
                        if (check.Visible) Assert.IsTrue(TextRenderer.MeasureText(check.Text, check.Font).Width + check.LogicalToDeviceUnits(22) <= check.Width,
                            $"Clipped mail checkbox {name}: {check.Text} in {check.Width}");
                    }
                    var scroll = Field<XtraScrollableControl>(mail, "connectionScroll"); var contents = Field<TableLayoutPanel>(mail, "connectionLayout");
                    Assert.IsTrue(scroll.AutoScrollMinSize.Height >= contents.Height);
                    if (variant.Font == 16F)
                    {
                        scroll.ScrollControlIntoView(Field<LabelControl>(mail, "hintLabel")); form.PerformLayout();
                        using var lower = WawiCallPro.App.Diagnostics.UiAudit.Capture(form); lower.Save(Path.Combine(output, $"mail-settings-{provider}-scroll.png"), ImageFormat.Png);
                    }
                    Field<XtraTabControl>(mail, "detailsTabs").SelectedTabPage = Field<XtraTabPage>(mail, "foldersTab");
                    form.PerformLayout(); CheckVisibleMailBounds(form);
                    Assert.AreEqual(2, Field<GridView>(mail, "foldersView").SelectedRowsCount);
                    Assert.IsTrue(Field<DevExpress.XtraGrid.GridControl>(mail, "foldersGrid").Height >= mail.Font.Height * 4, "The folder selector must retain a usable viewport.");
                    using var folders = WawiCallPro.App.Diagnostics.UiAudit.Capture(form); folders.Save(Path.Combine(output, $"mail-settings-{provider}-folders-{variant.Font:0}-{variant.Scale:0.0}.png"), ImageFormat.Png);
                }
                Assert.AreEqual(0D, form.Opacity); Assert.IsFalse(form.ShowInTaskbar); await page.StopAsync();
            }
        }
        finally
        {
            WindowsFormsSettings.DefaultFont = originalFont; AppearanceObject.DefaultFont = originalAppearance;
            if (string.IsNullOrEmpty(originalPalette)) DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(originalSkin); else DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(originalSkin, originalPalette);
        }
    });
}

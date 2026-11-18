using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using DevExpress.Utils;
using DevExpress.XtraBars;
using DevExpress.XtraBars.Ribbon;
using DevExpress.XtraEditors;
using Microsoft.Extensions.DependencyInjection;
using WawiCallPro.App.Hosting;
using WawiCallPro.App.Views;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Operations;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    public Task MailCacheSettingsSaveTheOfficeDraftWithoutDiscardingOtherUnsavedSettings() => WithWorkspace(async (workspace, services, _) =>
    {
        using var page = new SettingsPage(); page.Bind(workspace); CreateHandles(page);
        var mail = Field<MailSettingsPage>(page, "mailSettingsPage");
        Field<CheckEdit>(page, "notificationsEdit").Checked = !workspace.Configuration.Current.Options.User.ShowCallNotifications;
        Field<CheckEdit>(mail, "cacheEnabledEdit").Checked = false;
        Field<SpinEdit>(mail, "cacheDaysEdit").Value = 75;
        Assert.IsFalse(Field<SpinEdit>(mail, "cacheDaysEdit").Enabled);
        Assert.IsFalse(Field<SimpleButton>(mail, "syncButton").Enabled);
        Assert.AreEqual(new MailCacheSettings { Enabled = false, Days = 75 }, page.ReadOptions().MailCache);
        await (Task)typeof(SettingsPage).GetMethod("SaveMailSettingsAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(page, [CancellationToken.None])!;
        var saved = await workspace.Settings.LoadAsync();
        Assert.AreEqual(new MailCacheSettings { Enabled = false, Days = 75 }, saved.Options.MailCache);
        Assert.AreNotEqual(page.ReadOptions().User.ShowCallNotifications, saved.Options.User.ShowCallNotifications);
        await page.SaveAsync(CancellationToken.None);
        Assert.AreEqual(page.ReadOptions().User.ShowCallNotifications, (await workspace.Settings.LoadAsync()).Options.User.ShowCallNotifications);
        Field<CheckEdit>(mail, "cacheEnabledEdit").Checked = true;
        Assert.IsTrue(Field<SpinEdit>(mail, "cacheDaysEdit").Enabled);
        await page.StopAsync();
    });

    [TestMethod]
    public Task MailRibbonRebuildsOnlyTheIsolatedCacheAndRespectsTheDisabledSetting() => WithWorkspace(async (workspace, services, _) =>
    {
        using var form = new MainForm(workspace.Application, workspace.Configuration, workspace, services.GetRequiredService<AppUiState>());
        form.PrepareForBackgroundRun(); form.Show(); CreateHandles(form);
        var ribbon = Field<RibbonControl>(form, "ribbon");
        var mail = Field<RibbonPage>(form, "mailRibbon"); ribbon.SelectedPage = mail;
        await RefreshMailRibbon(form);
        Assert.AreEqual("Postfach", mail.Groups[0].Text); Assert.AreEqual("Lokaler Cache", mail.Groups[1].Text);
        foreach (var name in new[] { "mailRefreshItem", "mailSettingsItem", "mailCacheRebuildItem", "mailCacheCancelItem" })
        {
            var item = Field<BarButtonItem>(form, name); Assert.IsNotNull(item.ImageOptions.SvgImage); Assert.IsFalse(string.IsNullOrWhiteSpace(item.Hint));
        }
        Assert.IsTrue(Field<BarButtonItem>(form, "mailCacheRebuildItem").Enabled);
        var location = Path.Combine(services.GetRequiredService<ApplicationPaths>().RootDirectory, "mail-cache-v1.db");
        Assert.IsFalse(File.Exists(location)); await form.RebuildMailCacheAsync(); Assert.IsTrue(File.Exists(location));
        Assert.Contains("neu erstellt", Field<LabelControl>(form, "connectionLabel").Text);
        var snapshot = await workspace.Settings.LoadAsync();
        await workspace.Settings.SaveAsync(snapshot.Options with { MailCache = new() { Enabled = false, Days = 30 } }, snapshot);
        await RefreshMailRibbon(form); Assert.IsFalse(Field<BarButtonItem>(form, "mailCacheRebuildItem").Enabled);
        await form.PrepareToCloseAsync();
    });

    [TestMethod]
    public Task MailRibbonCancelsOneOperationAndShutdownAwaitsNonCooperativeWork() => WithWorkspace(async (workspace, services, _) =>
    {
        using var form = new MainForm(workspace.Application, workspace.Configuration, workspace, services.GetRequiredService<AppUiState>());
        form.PrepareForBackgroundRun(); form.Show(); CreateHandles(form);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); CancellationToken captured = default;
        var pending = form.RunMailOperationAsync(async token => { captured = token; entered.SetResult(); await release.Task; }, "Erledigt");
        await entered.Task; Assert.IsFalse(Field<BarButtonItem>(form, "mailCacheRebuildItem").Enabled);
        Assert.AreSame(pending, form.RunMailOperationAsync(_ => throw new AssertFailedException("Overlapping operation"), ""));
        Field<BarButtonItem>(form, "mailCacheCancelItem").PerformClick(); Assert.IsTrue(captured.IsCancellationRequested);
        var closing = form.PrepareToCloseAsync(); await Task.Yield(); Assert.IsFalse(closing.IsCompleted);
        release.SetResult(); await closing; await pending;
    });

    [TestMethod]
    public Task MailRibbonShutdownAlsoCancelsAndAwaitsTheDirectViewReload() => WithWorkspace(async (workspace, services, _) =>
    {
        using var form = new MainForm(workspace.Application, workspace.Configuration, workspace, services.GetRequiredService<AppUiState>());
        form.PrepareForBackgroundRun(); form.Show(); form.Navigate("mail");
        var page = (MailPage)form.GetActiveDocumentView()!;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); CancellationToken captured = default;
        var source = new MailSourceDouble { BeforeFolders = async token => { captured = token; entered.TrySetResult(); await release.Task; } };
        page.Bind(source, null);
        var pending = form.RunMailOperationAsync(_ => Task.CompletedTask, "Erledigt");
        await entered.Task;
        var closing = form.PrepareToCloseAsync();
        for (var i = 0; i < 100 && !captured.IsCancellationRequested; i++) await Task.Delay(5);
        Assert.IsTrue(captured.IsCancellationRequested); Assert.IsFalse(closing.IsCompleted);
        release.SetResult(); await pending; await closing;
    });

    [TestMethod]
    public Task MailRibbonGroupsAndNeutralIconsRenderWithBothSkinsLargeFontsAndScaledControls() => WithWorkspace(async (workspace, services, _) =>
    {
        AppUiStartup.Apply();
        var oldFont = WindowsFormsSettings.DefaultFont; var oldAppearance = AppearanceObject.DefaultFont;
        var oldSkin = DevExpress.LookAndFeel.UserLookAndFeel.Default.ActiveSkinName;
        var oldPalette = DevExpress.LookAndFeel.UserLookAndFeel.Default.ActiveSvgPaletteName;
        var output = Path.Combine(RenderOutput(), "mail-cache-ribbon"); Directory.CreateDirectory(output);
        try
        {
            foreach (var variant in new[] { (Width: 1240, Font: 10F, Scale: 1F, Skin: "Basic"), (Width: 900, Font: 16F, Scale: 1F, Skin: "Office 2019 Black"), (Width: 1240, Font: 10F, Scale: 1.5F, Skin: "Basic") })
            {
                DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(variant.Skin);
                using var font = new Font("Segoe UI", variant.Font); WindowsFormsSettings.DefaultFont = AppearanceObject.DefaultFont = font;
                using var form = new MainForm(workspace.Application, workspace.Configuration, workspace, services.GetRequiredService<AppUiState>());
                form.PrepareForBackgroundRun(); form.Font = font; form.MinimumSize = Size.Empty; form.ClientSize = new(variant.Width, 700);
                form.Show(); CreateHandles(form); if (variant.Scale != 1F) form.Scale(new SizeF(variant.Scale, variant.Scale));
                foreach (var name in new[] { "startRibbon", "mailRibbon", "databaseRibbon", "settingsRibbon" })
                {
                    var ribbon = Field<RibbonControl>(form, "ribbon"); ribbon.Minimized = false; ribbon.SelectedPage = Field<RibbonPage>(form, name);
                    await RefreshMailRibbon(form); form.PerformLayout(); Application.DoEvents();
                    Assert.AreEqual(0D, form.Opacity); Assert.IsFalse(form.ShowInTaskbar);
                    using var image = WawiCallPro.App.Diagnostics.UiAudit.Capture(form);
                    image.Save(Path.Combine(output, $"{name}-{variant.Width}-{variant.Font:0}-{variant.Scale:0.0}.png"), ImageFormat.Png);
                }
                await form.PrepareToCloseAsync();
            }
        }
        finally
        {
            WindowsFormsSettings.DefaultFont = oldFont; AppearanceObject.DefaultFont = oldAppearance;
            if (string.IsNullOrEmpty(oldPalette)) DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(oldSkin);
            else DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(oldSkin, oldPalette);
        }
    });

    private static Task RefreshMailRibbon(MainForm form) => (Task)typeof(MainForm).GetMethod("UpdateMailActionsAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, null)!;
}

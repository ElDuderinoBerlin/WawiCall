using DevExpress.XtraEditors;
using Microsoft.Extensions.DependencyInjection;
using WawiCallPro.App.Hosting;
using WawiCallPro.App.Views;
using WawiCallPro.Interfaces.Plugins;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    public Task SettingsOpensWithLoadedValuesAndKeepsItsOwnerResponsiveWhilePreparing() => WithWorkspace(async (workspace, services, _) =>
    {
        var saved = await workspace.Settings.LoadAsync();
        var showNotifications = !saved.Options.User.ShowCallNotifications;
        await workspace.Settings.SaveAsync(saved.Options with
        {
            User = saved.Options.User with { ShowCallNotifications = showNotifications }
        }, saved);

        var runtime = (TestRuntime)services.GetRequiredService<IProductPluginRuntime>();
        var catalogEntered = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var releaseCatalog = new ManualResetEventSlim();
        var catalogReads = 0;
        var audioEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseAudio = new TaskCompletionSource<Interfaces.Audio.AudioDeviceCatalog>(TaskCreationOptions.RunContinuationsAsynchronously);
        var audio = new AudioCatalogDouble
        {
            Read = token =>
            {
                audioEntered.TrySetResult();
                return releaseAudio.Task.WaitAsync(token);
            }
        };

        using var form = new MainForm(workspace.Application, workspace.Configuration, workspace,
            services.GetRequiredService<AppUiState>(), new LogWindowService(), audio);
        form.PrepareForBackgroundRun();
        form.Show();
        runtime.ReadCatalog = () =>
        {
            Interlocked.Increment(ref catalogReads);
            catalogEntered.TrySetResult(Environment.CurrentManagedThreadId);
            releaseCatalog.Wait(TimeSpan.FromSeconds(5));
        };
        var shown = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var opening = form.ShowSettingsAsync("settings", () =>
        {
            var dialog = form.OwnedForms.OfType<FrmSettings>().Single();
            try
            {
                var page = Field<SettingsPage>(dialog, "settingsPage");
                Assert.IsTrue(dialog.Modal && dialog.Visible);
                Assert.AreEqual("Allgemein", page.SettingsTabs.SelectedTabPage.Text);
                Assert.AreEqual(showNotifications, Field<CheckEdit>(page, "notificationsEdit").Checked);
                Assert.IsTrue(Field<SimpleButton>(page, "saveButton").Enabled);
                var pluginPage = Field<PluginPage>(page, "pluginPage");
                Assert.AreEqual(2, Field<DevExpress.XtraGrid.Views.Items.ItemsView>(pluginPage, "catalogView").DataRowCount);
                CheckTableBounds(dialog);
                shown.SetResult();
            }
            catch (Exception error) { shown.SetException(error); }
            finally
            {
                runtime.ReadCatalog = null;
                dialog.BeginInvoke(new Action(dialog.Close));
            }
        });
        try
        {
            var catalogThread = await catalogEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await audioEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.AreNotEqual(Environment.CurrentManagedThreadId, catalogThread);
            Assert.IsTrue(form.Visible && form.Enabled);
            Assert.AreEqual(0, form.OwnedForms.OfType<FrmSettings>().Count());
            Assert.IsFalse(shown.Task.IsCompleted);
            Assert.IsFalse(await form.ShowSettingsAsync("settings-mail"), "A second request must not open another dialog.");
            Assert.IsFalse(opening.IsCompleted);
        }
        finally { releaseCatalog.Set(); releaseAudio.TrySetResult(new([], [])); }

        await shown.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsTrue(await opening);
        Assert.AreEqual(1, catalogReads);
        Assert.AreEqual(1, audio.Reads);
        Assert.IsFalse(form.UseWaitCursor);
        await form.PrepareToCloseAsync();
    });

    [TestMethod]
    public Task ClosingOwnerCancelsSettingsPreparationWithoutShowingAModal() => WithWorkspace(async (workspace, services, _) =>
    {
        var reading = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<Interfaces.Audio.AudioDeviceCatalog>(TaskCreationOptions.RunContinuationsAsynchronously);
        var audio = new AudioCatalogDouble
        {
            Read = token =>
            {
                reading.SetResult();
                return release.Task.WaitAsync(token);
            }
        };
        using var form = new MainForm(workspace.Application, workspace.Configuration, workspace,
            services.GetRequiredService<AppUiState>(), new LogWindowService(), audio);
        form.PrepareForBackgroundRun();
        form.Show();
        var shown = false;
        var opening = form.ShowSettingsAsync("settings-audio", () => shown = true);
        try
        {
            await reading.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await form.PrepareToCloseAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsFalse(await opening);
            Assert.IsFalse(shown);
            Assert.IsFalse(form.UseWaitCursor);
        }
        finally { release.TrySetResult(new([], [])); }
    });
}

using DevExpress.XtraEditors;
using Microsoft.Extensions.DependencyInjection;
using WawiCallPro.App.Views;
using WawiCallPro.Interfaces.Plugins;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    public Task SettingsShowsGeneralValuesWhilePluginCatalogAndAudioAreStillLoading() => WithWorkspace(async (workspace, services, _) =>
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
        runtime.ReadCatalog = () =>
        {
            Interlocked.Increment(ref catalogReads);
            catalogEntered.TrySetResult(Environment.CurrentManagedThreadId);
            releaseCatalog.Wait(TimeSpan.FromSeconds(5));
        };
        var audioEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var audio = new AudioCatalogDouble
        {
            Read = _ =>
            {
                audioEntered.TrySetResult();
                return Task.FromResult(new Interfaces.Audio.AudioDeviceCatalog([], []));
            }
        };

        using var form = new FrmSettings(workspace);
        form.PrepareForBackgroundRun();
        form.ConfigureAudioDevices(audio);
        var page = Field<SettingsPage>(form, "settingsPage");
        Assert.AreEqual(0, catalogReads);
        Assert.AreEqual(0, audio.Reads);
        form.Show();
        var opening = page.ReloadAsync();
        try
        {
            var catalogThread = await catalogEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await audioEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.AreNotEqual(Environment.CurrentManagedThreadId, catalogThread);
            Assert.IsTrue(form.Visible);
            Assert.AreEqual("Allgemein", page.SettingsTabs.SelectedTabPage.Text);
            Assert.AreEqual(showNotifications, Field<CheckEdit>(page, "notificationsEdit").Checked);
            Assert.IsFalse(opening.IsCompleted);
        }
        finally { releaseCatalog.Set(); }

        await opening;
        Assert.AreEqual(1, catalogReads);
        Assert.AreEqual(1, audio.Reads);
        CheckTableBounds(form);
        await form.StopAsync();
    });
}

using System.Text.Json.Nodes;
using DevExpress.XtraEditors;
using Microsoft.Extensions.Logging;
using WawiCallPro.App.Views;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    public Task SettingsJsonActionsExportSavedValuesImportChangesAndPreserveRunningConfiguration() => WithWorkspace(async (workspace, services, plugin) =>
    {
        var start = await workspace.Settings.LoadAsync(); await workspace.Settings.SaveAsync(start.Options with { User = start.Options.User with { LogLevel = LogLevel.Warning } }, start);
        using var form = new FrmSettings(workspace); form.PrepareForBackgroundRun(); CreateHandles(form);
        var page = Field<SettingsPage>(form, "settingsPage"); page.Apply(await workspace.Settings.LoadAsync());
        Field<ComboBoxEdit>(page, "logLevelEdit").SelectedIndex = (int)LogLevel.Trace;
        var path = Path.Combine(workspace.Paths.RootDirectory, "portable.json"); await page.ExportSettingsAsync(path);
        var json = JsonNode.Parse(await File.ReadAllTextAsync(path))!; Assert.AreEqual("Warning", (string)json["user"]!["logLevel"]!);
        json["user"]!["logLevel"] = "Critical"; json["audio"]!["playbackVolumePercent"] = 42; await File.WriteAllTextAsync(path, json.ToJsonString());
        var running = workspace.Configuration.Current; await page.ImportSettingsAsync(path);
        Assert.AreEqual(LogLevel.Critical, page.ReadOptions().User.LogLevel); Assert.AreEqual(42, page.ReadOptions().Devices.PlaybackVolumePercent);
        Assert.AreEqual(LogLevel.Critical, (await workspace.Settings.LoadAsync()).Options.User.LogLevel); Assert.AreSame(running, workspace.Configuration.Current);
        Assert.Contains("importiert und gespeichert", page.LastSettingsNoticeText);
        await page.SaveAsync(CancellationToken.None); // Import refreshed the expected on-disk revision.
        await form.StopAsync();
    });

    [TestMethod]
    public Task SettingsInvalidImportKeepsTheDialogDraftAndSavedFile() => WithWorkspace(async (workspace, services, plugin) =>
    {
        var initial = await workspace.Settings.LoadAsync(); await workspace.Settings.SaveAsync(initial.Options, initial);
        using var form = new FrmSettings(workspace); form.PrepareForBackgroundRun(); CreateHandles(form);
        var page = Field<SettingsPage>(form, "settingsPage"); page.Apply(await workspace.Settings.LoadAsync());
        Field<ComboBoxEdit>(page, "logLevelEdit").SelectedIndex = (int)LogLevel.Trace;
        var before = (await workspace.Settings.LoadAsync()).Options; var path = Path.Combine(workspace.Paths.RootDirectory, "invalid-import.json");
        await File.WriteAllTextAsync(path, "{\"private-detail\":1}"); await page.ImportSettingsAsync(path);
        Assert.AreEqual(LogLevel.Trace, page.ReadOptions().User.LogLevel); Assert.AreEqual(before.User.LogLevel, (await workspace.Settings.LoadAsync()).Options.User.LogLevel);
        Assert.AreEqual(WawiCallPro.Utilities.Operations.OperationEvent.SettingsValidationFailed, page.LastSettingsNotice); Assert.AreEqual(DialogResult.None, form.DialogResult);
        await form.StopAsync();
    });
}

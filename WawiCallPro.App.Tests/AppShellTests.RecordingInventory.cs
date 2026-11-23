using DevExpress.LookAndFeel;
using DevExpress.Utils;
using DevExpress.XtraBars;
using DevExpress.XtraBars.Ribbon;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid.Views.Grid;
using Microsoft.Extensions.DependencyInjection;
using WawiCallPro.App.Diagnostics;
using WawiCallPro.App.Hosting;
using WawiCallPro.App.Views;
using WawiCallPro.Utilities.Persistence;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    public Task RecordingInventoryDialogShowsRelativeReviewRowsAndClearsFailedResults() => WithWorkspace(async (workspace, services, _) =>
    {
        var inventory = (InventoryProbe)services.GetRequiredService<IRecordingDirectoryInventory>();
        using var main = new MainForm(workspace.Application, workspace.Configuration, workspace, services.GetRequiredService<AppUiState>());
        main.PrepareForBackgroundRun();
        var item = Field<BarButtonItem>(main, "recordingInventoryItem");
        Assert.AreEqual("Aufnahmeordner prüfen", item.Caption);
        Assert.IsNotNull(item.ImageOptions.SvgImage);
        Assert.IsTrue(Field<RibbonPage>(main, "audioRibbon").Groups[0].ItemLinks.Any(link => link.Item == item));

        var look = UserLookAndFeel.Default;
        var originalSkin = look.ActiveSkinName;
        var originalPalette = look.ActiveSvgPaletteName;
        var originalFont = WindowsFormsSettings.DefaultFont;
        var originalAppearance = AppearanceObject.DefaultFont;
        var output = Path.Combine(RenderOutput(), "recording-inventory");
        Directory.CreateDirectory(output);
        try
        {
            foreach (var (skin, size, width) in new[] { ("Basic", 10F, 770), ("Office 2019 Black", 16F, 670) })
            {
                using var font = new Font("Segoe UI", size);
                WindowsFormsSettings.DefaultFont = AppearanceObject.DefaultFont = font;
                look.SetSkinStyle(skin);
                inventory.Read = _ => Task.FromResult(new RecordingDirectoryInventoryResult(4, 1, 1, 1,
                    [new("recordings/z.wav", false), new("recordings/a.tmp", false), new("recordings/link", true), new("recordings/pending.wav", false, true)]));
                using var dialog = main.CreateRecordingInventoryDialog();
                dialog.Font = font;
                dialog.ClientSize = new Size(width, 440);
                var shown = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                dialog.Shown += (_, _) => shown.SetResult();
                dialog.Show();
                await shown.Task.WaitAsync(TimeSpan.FromSeconds(5));
                await WaitForOperationAsync(dialog);
                var view = Field<GridView>(dialog, "reviewView");
                Assert.AreEqual(4, view.DataRowCount);
                Assert.AreEqual("recordings/a.tmp", view.GetRowCellDisplayText(0, "RelativePath"));
                Assert.AreEqual("Temporäre Datei ohne Zuordnung", view.GetRowCellDisplayText(0, "Reason"));
                Assert.AreEqual("Link übersprungen", view.GetRowCellDisplayText(1, "Reason"));
                Assert.AreEqual("WAV-Abschluss ausstehend", view.GetRowCellDisplayText(2, "Reason"));
                Assert.IsFalse(view.OptionsBehavior.Editable);
                Assert.Contains("4 mit lokaler Zuordnung", Field<LabelControl>(dialog, "summaryLabel").Text);
                CheckTableBounds(dialog);
                using (var shot = UiAudit.Capture(dialog))
                    shot.Save(Path.Combine(output, $"{workspace.Application.ProductLine}-{size:0}.png"), System.Drawing.Imaging.ImageFormat.Png);
                if (size == 16F)
                {
                    dialog.Scale(new SizeF(1.5F, 1.5F));
                    dialog.PerformLayout();
                    CheckTableBounds(dialog);
                    using var scaled = UiAudit.Capture(dialog);
                    scaled.Save(Path.Combine(output, $"{workspace.Application.ProductLine}-scale150.png"), System.Drawing.Imaging.ImageFormat.Png);
                }

                inventory.Read = _ => throw new IOException("synthetic private path");
                await dialog.RefreshAsync();
                Assert.AreEqual(0, view.DataRowCount);
                Assert.Contains("konnte nicht vollständig gelesen werden", Field<LabelControl>(dialog, "summaryLabel").Text);
                Assert.DoesNotContain("synthetic private path", Field<LabelControl>(dialog, "summaryLabel").Text);
                inventory.Read = _ => Task.FromResult(new RecordingDirectoryInventoryResult(0, 0, 0, 0, []));
                await dialog.RefreshAsync();
                Assert.Contains("Keine Dateien mit Prüfbedarf", Field<LabelControl>(dialog, "summaryLabel").Text);
                CheckTableBounds(dialog);
                using var empty = UiAudit.Capture(dialog);
                empty.Save(Path.Combine(output, $"{workspace.Application.ProductLine}-empty-{size:0}.png"), System.Drawing.Imaging.ImageFormat.Png);
            }
        }
        finally
        {
            WindowsFormsSettings.DefaultFont = originalFont;
            AppearanceObject.DefaultFont = originalAppearance;
            if (string.IsNullOrEmpty(originalPalette)) look.SetSkinStyle(originalSkin); else look.SetSkinStyle(originalSkin, originalPalette);
        }
    }, configure: services => services.AddSingleton<IRecordingDirectoryInventory, InventoryProbe>());

    [TestMethod]
    public Task RecordingInventoryDialogCancelsPendingReadBeforeClosing() => WithWorkspace(async (_, _, _) =>
    {
        var reading = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var inventory = new InventoryProbe
        {
            Read = async token =>
            {
                reading.SetResult();
                try { await Task.Delay(Timeout.Infinite, token); }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    canceled.SetResult();
                    throw;
                }
                return new RecordingDirectoryInventoryResult(0, 0, 0, 0, []);
            }
        };
        using var dialog = new FrmRecordingInventory(inventory, null);
        dialog.PrepareForBackgroundRun();
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        dialog.FormClosed += (_, _) => closed.SetResult();
        dialog.Show();
        await reading.Task.WaitAsync(TimeSpan.FromSeconds(5));
        dialog.Close();
        await closed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsTrue(canceled.Task.IsCompletedSuccessfully);
    });

    private sealed class InventoryProbe : IRecordingDirectoryInventory
    {
        public required Func<CancellationToken, Task<RecordingDirectoryInventoryResult>> Read { get; set; }
        public Task<RecordingDirectoryInventoryResult> ReadAsync(CancellationToken cancellationToken) => Read(cancellationToken);
    }
}

using DevExpress.LookAndFeel;
using DevExpress.XtraEditors;
using WawiCallPro.App.Hosting;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    public Task DpiReviewRecordsDialogMinimumsWithoutVisibleWindows() => RunStaAsync(() =>
    {
        AppUiStartup.Apply();
        var oldFont = WindowsFormsSettings.DefaultFont;
        var oldAppearance = DevExpress.Utils.AppearanceObject.DefaultFont;
        var measurements = new List<object>();
        try
        {
            foreach (var size in new[] { 9F, 16F, 24F })
            {
                using var font = new Font("Segoe UI", size);
                WindowsFormsSettings.DefaultFont = DevExpress.Utils.AppearanceObject.DefaultFont = font;
                using var dialog = new WawiCallPro.App.Views.FrmSettings();
                dialog.PrepareForBackgroundRun(); dialog.Font = font; dialog.ClientSize = new Size(900, 700);
                CreateHandles(dialog); dialog.PerformLayout();
                typeof(WawiCallPro.App.Views.FrmSettings).GetMethod("ContentSize_Changed", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .Invoke(dialog, [null, EventArgs.Empty]);
                var area = Screen.FromControl(dialog).WorkingArea;
                measurements.Add(new { FontSize = size, dialog.DeviceDpi, MinimumWidth = dialog.MinimumSize.Width,
                    MinimumHeight = dialog.MinimumSize.Height, WorkingWidth = area.Width, WorkingHeight = area.Height });
                Assert.IsFalse(dialog.Visible); Assert.AreEqual(0d, dialog.Opacity); Assert.IsFalse(dialog.ShowInTaskbar);
            }
            var output = Path.Combine(RenderOutput(), "dpi-review-dialog-limits.json");
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            File.WriteAllText(output, System.Text.Json.JsonSerializer.Serialize(measurements, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
        finally { WindowsFormsSettings.DefaultFont = oldFont; DevExpress.Utils.AppearanceObject.DefaultFont = oldAppearance; }
        return Task.CompletedTask;
    });

    [TestMethod]
    [DataRow("Basic", 9F)]
    [DataRow("Basic", 16F)]
    [DataRow("Office 2019 Black", 9F)]
    [DataRow("Office 2019 Black", 16F)]
    public Task ToolbarButtonsCountInsetsOnceAndStayCompactAcrossRepeatedLayouts(string skin, float fontSize) => RunStaAsync(() =>
    {
        AppUiStartup.Apply();
        var oldSkin = UserLookAndFeel.Default.ActiveSkinName;
        var oldPalette = UserLookAndFeel.Default.ActiveSvgPaletteName;
        try
        {
            UserLookAndFeel.Default.SetSkinStyle(skin);
            using var font = new Font("Segoe UI", fontSize);
            using var reference = new SimpleButton { Text = "Gespräche laden", Font = font, Padding = Padding.Empty };
            var native = reference.CalcBestSize();
            reference.Padding = new Padding(reference.LogicalToDeviceUnits(12), reference.LogicalToDeviceUnits(6),
                reference.LogicalToDeviceUnits(12), reference.LogicalToDeviceUnits(6));
            var paddedNative = reference.CalcBestSize();
            using var button = new SimpleButton { Text = reference.Text, Font = font };
            UiButtonSizing.Apply(button);
            var text = TextRenderer.MeasureText(button.Text, button.Font);
            var compactHeight = Math.Max(button.LogicalToDeviceUnits(32), Math.Max(native.Height, text.Height + button.LogicalToDeviceUnits(14)));
            Assert.IsTrue(button.Height <= compactHeight + 1,
                $"{skin}/{fontSize} pt, DPI={button.DeviceDpi}: native={native}, padded native={paddedNative}, actual={button.Size}, compact height={compactHeight}");
            var first = button.Size;
            for (var i = 0; i < 10; i++) UiButtonSizing.Apply(button);
            Assert.AreEqual(first, button.Size, "Repeated layout must not accumulate insets or scaling.");
            Assert.IsTrue(button.Padding.Left >= button.LogicalToDeviceUnits(12));
            Assert.IsTrue(button.Padding.Top >= button.LogicalToDeviceUnits(2));
            using var dialog = new XtraForm();
            dialog.Controls.Add(button);
            UiButtonSizing.Apply(button);
            Assert.IsTrue(button.Padding.Top >= button.LogicalToDeviceUnits(6));
            Assert.IsTrue(button.Height >= button.LogicalToDeviceUnits(40));
            dialog.Controls.Remove(button);
            UiButtonSizing.Apply(button);
            Assert.AreEqual(first, button.Size, "Moving between dialog and toolbar must restore the compact metrics.");
        }
        finally { UserLookAndFeel.Default.SetSkinStyle(oldSkin, oldPalette); }
        return Task.CompletedTask;
    });
}

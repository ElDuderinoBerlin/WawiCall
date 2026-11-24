using DevExpress.LookAndFeel;
using DevExpress.Utils;
using DevExpress.XtraEditors;
using WawiCallPro.App.Hosting;
using WawiCallPro.App.Views;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    [DataRow("Basic", 8F)]
    [DataRow("Office 2019 Black", 8F)]
    [DataRow("Basic", 9F)]
    [DataRow("Basic", 16F)]
    [DataRow("Office 2019 Black", 9F)]
    [DataRow("Office 2019 Black", 16F)]
    public Task HistoryToolbarHeightStaysCloseToItsEditorsInTheActualDocumentHost(string skin, float fontSize) => RunStaAsync(() =>
    {
        AppUiStartup.Apply();
        var oldFont = WindowsFormsSettings.DefaultFont;
        var oldAppearance = AppearanceObject.DefaultFont;
        var oldSkin = UserLookAndFeel.Default.ActiveSkinName;
        var oldPalette = UserLookAndFeel.Default.ActiveSvgPaletteName;
        try
        {
            UserLookAndFeel.Default.SetSkinStyle(skin);
            using var font = new Font(fontSize == 8F ? "Tahoma" : "Segoe UI", fontSize);
            WindowsFormsSettings.DefaultFont = AppearanceObject.DefaultFont = font;
            using var host = new MainForm();
            host.PrepareForBackgroundRun(); host.Font = font; host.ClientSize = new Size(1240, 780);
            host.Show(); host.Navigate("history"); Application.DoEvents();
            var page = (HistoryPage)host.GetActiveDocumentView()!;
            var button = Field<SimpleButton>(page, "refreshButton");
            var from = Field<DateEdit>(page, "fromEdit");
            var to = Field<DateEdit>(page, "toEdit");
            var selected = from.DateTime;
            var output = Path.Combine(RenderOutput(), "history-toolbar-compact"); Directory.CreateDirectory(output);
            Console.WriteLine($"{skin}/{fontSize}pt: host={button.FindForm()?.GetType().FullName}, DPI={button.DeviceDpi}, button={button.Size}, editors={from.Height}/{to.Height}, padding={button.Padding}");
            using var image = WawiCallPro.App.Diagnostics.UiAudit.Capture(host);
            image.Save(Path.Combine(output, $"history-{skin}-{fontSize}.png"));
            Assert.IsTrue(button.Height <= Math.Max(from.Height, to.Height) + button.LogicalToDeviceUnits(8),
                $"Toolbar action {button.Height}px is too tall next to editors {from.Height}/{to.Height}px at {button.DeviceDpi} DPI.");
            Assert.IsTrue(button.Height >= button.CalcBestSize().Height, "Native skin and text must fit.");
            var firstSize = button.Size;
            for (var pass = 0; pass < 5; pass++) { UiButtonSizing.Apply(button); host.PerformLayout(); }
            Assert.AreEqual(firstSize, button.Size);
            Assert.AreEqual(selected, from.DateTime);
            CheckTableBounds(page);
            Assert.AreEqual(0d, host.Opacity); Assert.IsFalse(host.ShowInTaskbar);
        }
        finally
        {
            WindowsFormsSettings.DefaultFont = oldFont; AppearanceObject.DefaultFont = oldAppearance;
            UserLookAndFeel.Default.SetSkinStyle(oldSkin, oldPalette);
        }
        return Task.CompletedTask;
    });
}

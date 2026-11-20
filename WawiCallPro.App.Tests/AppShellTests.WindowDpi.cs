using Microsoft.Win32;
using DevExpress.LookAndFeel;
using DevExpress.XtraEditors;
using WawiCallPro.App.Hosting;
using WawiCallPro.App.Views;
using WawiCallPro.Utilities.Hosting;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    [DataRow(9F)]
    [DataRow(16F)]
    public Task WindowDpiNativeRestoredSettingsRenderWithVisibleActionsWithoutForegroundWindows(float fontSize) => WithWorkspace(async (workspace, _, _) =>
    {
        var product = "WindowDpiRender-" + Guid.NewGuid().ToString("N");
        var root = @"Software\WawiCallPro\" + product;
        var state = new AppUiState(new ApplicationDescriptor("Isolated restored settings", product), new(false));
        var oldFont = WindowsFormsSettings.DefaultFont;
        var oldAppearance = DevExpress.Utils.AppearanceObject.DefaultFont;
        var oldSkin = UserLookAndFeel.Default.ActiveSkinName;
        var oldPalette = UserLookAndFeel.Default.ActiveSvgPaletteName;
        var output = Path.Combine(RenderOutput(), "dpi-r4-restored-settings"); Directory.CreateDirectory(output);
        try
        {
            foreach (var skin in new[] { "Basic", "Office 2019 Black" })
            {
                UserLookAndFeel.Default.SetSkinStyle(skin);
                using var font = new Font("Segoe UI", fontSize);
                WindowsFormsSettings.DefaultFont = DevExpress.Utils.AppearanceObject.DefaultFont = font;
                using (var saved = new XtraForm { Opacity = 0, ShowInTaskbar = false, Bounds = new Rectangle(30, 40, 1000, 740) })
                {
                    _ = saved.Handle; state.SaveWindow(saved, "FrmSettings");
                }
                using var dialog = new FrmSettings(workspace);
                dialog.PrepareForBackgroundRun(); dialog.ConfigureLayoutPersistence(state); dialog.Show();
                var page = Field<SettingsPage>(dialog, "settingsPage"); await page.ReloadAsync(); Application.DoEvents();
                foreach (var mail in new[] { false, true })
                {
                    if (mail) dialog.SelectMailTab();
                    Application.DoEvents(); CheckTableBounds(page);
                    Assert.IsTrue(Screen.FromControl(dialog).WorkingArea.Contains(dialog.Bounds));
                    foreach (var name in new[] { "saveButton", "cancelButton", "importButton", "exportButton" })
                    {
                        var button = Field<SimpleButton>(page, name);
                        Assert.IsTrue(dialog.ClientRectangle.Contains(dialog.RectangleToClient(button.RectangleToScreen(button.ClientRectangle))), name);
                    }
                    using var bitmap = WawiCallPro.App.Diagnostics.UiAudit.Capture(dialog);
                    bitmap.Save(Path.Combine(output, $"settings-{fontSize}-{skin}-{(mail ? "mail" : "general")}.png"));
                }
                Assert.AreEqual(0d, dialog.Opacity); Assert.IsFalse(dialog.ShowInTaskbar);
                await dialog.StopAsync();
            }
        }
        finally
        {
            WindowsFormsSettings.DefaultFont = oldFont; DevExpress.Utils.AppearanceObject.DefaultFont = oldAppearance;
            UserLookAndFeel.Default.SetSkinStyle(oldSkin, oldPalette); Registry.CurrentUser.DeleteSubKeyTree(root, false);
        }
    });

    [TestMethod]
    public Task WindowDpiMaximizedStateKeepsScaledNormalBoundsAndOverridesLegacyMainSize() => RunStaAsync(() =>
    {
        var product = "WindowDpiMax-" + Guid.NewGuid().ToString("N");
        var root = @"Software\WawiCallPro\" + product;
        var descriptor = new ApplicationDescriptor("DPI maximization test", product);
        var area = new Rectangle(0, 0, 3840, 2160);
        try
        {
            using var original = new Form { Bounds = new Rectangle(40, 60, 800, 500), Opacity = 0, ShowInTaskbar = false };
            _ = original.Handle; original.WindowState = FormWindowState.Maximized;
            new AppUiState(descriptor, new(false), new TestWindowDisplays(96, area, "Display")).SaveWindow(original, "MainForm");
            using (var legacy = Registry.CurrentUser.CreateSubKey(root + @"\App\v1"))
            {
                legacy.SetValue("Width", 600); legacy.SetValue("Height", 400); legacy.SetValue("Maximized", 0);
            }
            using var restored = new Form { Opacity = 0, ShowInTaskbar = false };
            _ = restored.Handle;
            var state = new AppUiState(descriptor, new(false), new TestWindowDisplays(144, area, "Display"));
            state.Restore(restored);
            Assert.AreEqual(FormWindowState.Maximized, restored.WindowState);
            var expected = new Rectangle(60, 90, 1200, 750);
            Assert.AreEqual(expected, restored.RestoreBounds, "Maximized bounds must not replace the scaled normal rectangle.");
            state.SaveWindow(restored, "MainForm");
            using var saved = Registry.CurrentUser.OpenSubKey(root + @"\App\v1\Windows\v2\MainForm")!;
            Assert.AreEqual(1200, saved.GetValue("Width")); Assert.AreEqual(750, saved.GetValue("Height"));
            Assert.AreEqual(1, saved.GetValue("Maximized")); Assert.AreEqual(144, saved.GetValue("Dpi"));
            Assert.IsFalse(restored.Visible);
        }
        finally { Registry.CurrentUser.DeleteSubKeyTree(root, false); }
        return Task.CompletedTask;
    });

    [TestMethod]
    [DataRow(96, 96)]
    [DataRow(96, 144)]
    [DataRow(96, 192)]
    [DataRow(144, 96)]
    [DataRow(144, 192)]
    [DataRow(192, 96)]
    [DataRow(192, 144)]
    public Task WindowDpiPersistenceRestartsAtTargetScaleAndReturnsToSource(int sourceDpi, int targetDpi) => RunStaAsync(() =>
    {
        var product = "WindowDpi-" + Guid.NewGuid().ToString("N");
        var root = @"Software\WawiCallPro\" + product;
        var descriptor = new ApplicationDescriptor("Isolated DPI persistence", product);
        var source = new TestWindowDisplays(sourceDpi, new Rectangle(-4000, -200, 3840, 2160), "Source");
        var target = new TestWindowDisplays(targetDpi, new Rectangle(1920, 40, 3840, 2160), "Target");
        var original = new Rectangle(source.Monitor.WorkingArea.Left + 40 * sourceDpi / 96,
            source.Monitor.WorkingArea.Top + 60 * sourceDpi / 96, 800 * sourceDpi / 96, 500 * sourceDpi / 96);
        try
        {
            using (var form = new Form { Bounds = original })
                new AppUiState(descriptor, new(false), source).SaveWindow(form, "Settings");
            using (var saved = Registry.CurrentUser.OpenSubKey(root + @"\App\v1\Windows\v2\Settings")!)
            {
                Assert.AreEqual(2, saved.GetValue("Version")); Assert.AreEqual(sourceDpi, saved.GetValue("Dpi"));
                Assert.AreEqual("Source", saved.GetValue("Monitor"));
            }
            using (var restarted = new Form())
            {
                var state = new AppUiState(descriptor, new(false), target);
                state.RestoreWindow(restarted, "Settings");
                var expected = new Rectangle(target.Monitor.WorkingArea.Left + 40 * targetDpi / 96,
                    target.Monitor.WorkingArea.Top + 60 * targetDpi / 96, 800 * targetDpi / 96, 500 * targetDpi / 96);
                Assert.AreEqual(expected, restarted.Bounds);
                Assert.AreEqual("Source", target.RequestedMonitor, "A removed monitor must be resolved by the display adapter.");
                state.RestoreWindow(restarted, "Settings");
                Assert.AreEqual(expected, restarted.Bounds, "Repeated restore must not apply scaling twice.");
                state.SaveWindow(restarted, "Settings");
            }
            using var returned = new Form();
            new AppUiState(descriptor, new(false), source).RestoreWindow(returned, "Settings");
            Assert.AreEqual(original, returned.Bounds, "Restarting back on the original DPI must preserve logical geometry.");
            using var legacy = Registry.CurrentUser.OpenSubKey(root + @"\App\v1\Windows\Settings");
            Assert.IsNull(legacy, "New saves use only the versioned window key.");
        }
        finally { Registry.CurrentUser.DeleteSubKeyTree(root, false); }
        return Task.CompletedTask;
    });

    [TestMethod]
    [DataRow(0, 2)]
    [DataRow(-96, 2)]
    [DataRow(769, 2)]
    [DataRow(96, 99)]
    public Task WindowDpiInvalidRecordsFallBackToLegacyPixelsWithoutChangingThem(int invalidDpi, int version) => RunStaAsync(() =>
    {
        var product = "WindowLegacy-" + Guid.NewGuid().ToString("N");
        var root = @"Software\WawiCallPro\" + product;
        var displays = new TestWindowDisplays(192, new Rectangle(0, 0, 1920, 1080), "Current");
        var state = new AppUiState(new ApplicationDescriptor("Legacy DPI test", product), new(false), displays);
        try
        {
            using (var form = new Form { Bounds = new Rectangle(40, 60, 800, 500) }) state.SaveWindow(form, "Settings");
            using (var legacy = Registry.CurrentUser.CreateSubKey(root + @"\App\v1\Windows\Settings"))
            {
                legacy.SetValue("Left", 80); legacy.SetValue("Top", 90);
                legacy.SetValue("Width", 720); legacy.SetValue("Height", 480); legacy.SetValue("Maximized", 0);
            }
            using (var current = Registry.CurrentUser.OpenSubKey(root + @"\App\v1\Windows\v2\Settings", true)!)
            {
                current.SetValue("Dpi", invalidDpi); current.SetValue("Version", version);
            }
            using var restored = new Form(); state.RestoreWindow(restored, "Settings");
            Assert.AreEqual(new Rectangle(80, 90, 720, 480), restored.Bounds, "Legacy pixels have no known source DPI and must not be blindly rescaled.");
            using var oldKey = Registry.CurrentUser.OpenSubKey(root + @"\App\v1\Windows\Settings")!;
            Assert.AreEqual(720, oldKey.GetValue("Width")); Assert.IsNull(oldKey.GetValue("Dpi"));
            using var newKey = Registry.CurrentUser.OpenSubKey(root + @"\App\v1\Windows\v2\Settings")!;
            Assert.AreEqual(invalidDpi, newKey.GetValue("Dpi"), "Reading must not rewrite registry data.");
            Registry.CurrentUser.DeleteSubKeyTree(root + @"\App\v1\Windows\v2");
            state.RestoreWindow(restored, "Settings");
            Assert.AreEqual(new Rectangle(80, 90, 720, 480), restored.Bounds);
            using var noNewKey = Registry.CurrentUser.OpenSubKey(root + @"\App\v1\Windows\v2");
            Assert.IsNull(noNewKey, "A legacy-only restore is also read-only.");
        }
        finally { Registry.CurrentUser.DeleteSubKeyTree(root, false); }
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task WindowDpiPlacementClampsToChangedWorkAreaAndPreservesLegacyMainAndAuditIsolation() => RunStaAsync(() =>
    {
        var product = "WindowDpiBounds-" + Guid.NewGuid().ToString("N");
        var root = @"Software\WawiCallPro\" + product;
        var descriptor = new ApplicationDescriptor("DPI bounds test", product);
        var displays = new TestWindowDisplays(192, new Rectangle(-1280, -100, 1280, 720), "New monitor");
        var state = new AppUiState(descriptor, new(false), displays);
        try
        {
            using (var key = Registry.CurrentUser.CreateSubKey(root + @"\App\v1"))
            {
                key.SetValue("Width", 1000); key.SetValue("Height", 650); key.SetValue("Maximized", 0);
            }
            using var form = new Form { Bounds = new Rectangle(-1200, 0, 800, 500) };
            state.Restore(form);
            Assert.AreEqual(new Size(1000, 650), form.Size, "Old root-level MainForm values remain readable.");
            var placement = new WindowPlacement(new Rectangle(100, 100, 1400, 900), 96,
                new Rectangle(0, 0, 1920, 1080), "Removed monitor", false);
            var converted = placement.GetBounds(192, displays.Monitor.WorkingArea);
            Assert.AreEqual(new Rectangle(-1080, 100, 2800, 1800), converted);
            Assert.AreEqual(displays.Monitor.WorkingArea,
                WindowWorkArea.Fit(converted, new Size(4000, 3000), displays.Monitor.WorkingArea));
            var before = form.Bounds;
            var suppressed = new AppUiState(descriptor, new(true), new ThrowingWindowDisplays());
            suppressed.Restore(form); suppressed.RestoreWindow(form, "Settings"); suppressed.AttachWindow(form, "Settings");
            suppressed.Save(form); suppressed.SaveWindow(form, "Settings");
            Assert.AreEqual(before, form.Bounds);
            using var windows = Registry.CurrentUser.OpenSubKey(root + @"\App\v1\Windows");
            Assert.IsNull(windows, "Audit mode must not create any window key or touch the display adapter.");
        }
        finally { Registry.CurrentUser.DeleteSubKeyTree(root, false); }
        return Task.CompletedTask;
    });

    private sealed class TestWindowDisplays(int dpi, Rectangle area, string name) : IWindowDisplayEnvironment
    {
        internal WindowMonitor Monitor { get; } = new(name, area);
        internal string? RequestedMonitor { get; private set; }
        public WindowMonitor GetCurrent(Form form) => Monitor;
        public WindowMonitor Select(string deviceName, Rectangle bounds) { RequestedMonitor = deviceName; return Monitor; }
        public int GetDpi(Form form) => dpi;
        public int PrepareRestore(Form form, WindowMonitor monitor) => dpi;
    }

    private sealed class ThrowingWindowDisplays : IWindowDisplayEnvironment
    {
        public WindowMonitor GetCurrent(Form form) => throw new InvalidOperationException("Audit display access");
        public WindowMonitor Select(string deviceName, Rectangle bounds) => throw new InvalidOperationException("Audit display access");
        public int GetDpi(Form form) => throw new InvalidOperationException("Audit display access");
        public int PrepareRestore(Form form, WindowMonitor monitor) => throw new InvalidOperationException("Audit display access");
    }
}

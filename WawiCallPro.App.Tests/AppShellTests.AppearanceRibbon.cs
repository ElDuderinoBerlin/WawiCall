using DevExpress.LookAndFeel;
using DevExpress.Utils;
using DevExpress.XtraBars;
using DevExpress.XtraBars.Ribbon;
using DevExpress.XtraEditors;
using Microsoft.Win32;
using WawiCallPro.App.Diagnostics;
using WawiCallPro.App.Hosting;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    public Task AppearanceRibbonRestoresAccentColorsAndRendersWithBothSkins() => WithWorkspace(async (workspace, _, _) =>
    {
        AppUiStartup.Apply();
        var key = @"Software\WawiCallPro.Tests\Appearance\" + Guid.NewGuid().ToString("N");
        var state = new AppUiState(workspace.Application, new(false), key);
        var look = UserLookAndFeel.Default;
        var originalSkin = look.ActiveSkinName;
        var originalPalette = look.ActiveSvgPaletteName;
        var originalFont = WindowsFormsSettings.DefaultFont;
        var originalAppearanceFont = AppearanceObject.DefaultFont;
        var originalTracking = WindowsFormsSettings.TrackWindowsAccentColor;
        var originalAppMode = WindowsFormsSettings.TrackWindowsAppMode;
        var originalAccent = WindowsFormsSettings.GetAccentColor();
        var originalSecondAccent = WindowsFormsSettings.GetAccentColor2();
        var primary = Color.FromArgb(156, 71, 186);
        var secondary = Color.FromArgb(64, 151, 138);
        try
        {
            WindowsFormsSettings.TrackWindowsAppMode = DefaultBoolean.False;
            WindowsFormsSettings.TrackWindowsAccentColor = DefaultBoolean.False;
            look.SetSkinStyle("The Bezier");
            WindowsFormsSettings.SetAccentColor(primary);
            WindowsFormsSettings.SetAccentColor2(secondary);

            using var form = new MainForm(workspace.Application, workspace.Configuration, workspace, state);
            form.PrepareForBackgroundRun();
            state.Save(form);
            WindowsFormsSettings.SetAccentColor(Color.Empty);
            WindowsFormsSettings.SetAccentColor2(Color.Empty);
            form.Show();
            CreateHandles(form);
            Assert.AreEqual(primary.ToArgb(), WindowsFormsSettings.GetAccentColor().ToArgb());
            Assert.AreEqual(secondary.ToArgb(), WindowsFormsSettings.GetAccentColor2().ToArgb());

            var track = Field<BarCheckItem>(form, "windowsAccentItem");
            var original = Field<BarCheckItem>(form, "originalPaletteItem");
            var firstCustom = Field<BarButtonItem>(form, "customAccentItem");
            var secondCustom = Field<BarButtonItem>(form, "customAccent2Item");
            Assert.IsNotNull(firstCustom.DropDownControl);
            Assert.IsNotNull(secondCustom.DropDownControl);
            var appMode = Field<BarCheckItem>(form, "windowsAppModeItem");
            appMode.PerformClick();
            Assert.AreEqual(DefaultBoolean.True, WindowsFormsSettings.TrackWindowsAppMode);
            appMode.PerformClick();
            Assert.AreEqual(DefaultBoolean.False, WindowsFormsSettings.TrackWindowsAppMode);
            track.PerformClick();
            Assert.AreEqual(DefaultBoolean.True, WindowsFormsSettings.TrackWindowsAccentColor);
            Assert.IsTrue(WindowsFormsSettings.GetAccentColor().IsEmpty);
            Assert.IsTrue(WindowsFormsSettings.GetAccentColor2().IsEmpty);
            original.PerformClick();
            Assert.IsFalse(track.Checked);
            Assert.IsTrue(WindowsFormsSettings.GetAccentColor().IsEmpty);
            Assert.IsTrue(WindowsFormsSettings.GetAccentColor2().IsEmpty);

            var ribbon = Field<RibbonControl>(form, "ribbon");
            ribbon.SelectedPage = Field<RibbonPage>(form, "settingsRibbon");
            var output = Path.Combine(RenderOutput(), "appearance-ribbon");
            Directory.CreateDirectory(output);
            foreach (var (skin, width, fontSize, scale) in new[]
            {
                ("The Bezier", 1500, 10F, 1F),
                ("Office 2019 Black", 1000, 16F, 1F),
                ("Basic", 1250, 10F, 1.5F)
            })
            {
                if (skin == "Basic") look.SetSkinStyle(skin, "Blue Dark");
                else look.SetSkinStyle(skin);
                using var font = new Font("Segoe UI", fontSize);
                WindowsFormsSettings.DefaultFont = AppearanceObject.DefaultFont = form.Font = font;
                form.ClientSize = new Size(width, 680);
                if (scale != 1F) form.Scale(new SizeF(scale, scale));
                form.PerformLayout();
                Application.DoEvents();
                using var image = UiAudit.Capture(form);
                image.Save(Path.Combine(output, $"appearance-{width}.png"));
            }
            await form.PrepareToCloseAsync();
        }
        finally
        {
            WindowsFormsSettings.DefaultFont = originalFont;
            AppearanceObject.DefaultFont = originalAppearanceFont;
            WindowsFormsSettings.TrackWindowsAppMode = originalAppMode;
            WindowsFormsSettings.TrackWindowsAccentColor = originalTracking;
            if (string.IsNullOrEmpty(originalPalette)) look.SetSkinStyle(originalSkin);
            else look.SetSkinStyle(originalSkin, originalPalette);
            WindowsFormsSettings.SetAccentColor(originalAccent);
            WindowsFormsSettings.SetAccentColor2(originalSecondAccent);
            Registry.CurrentUser.DeleteSubKeyTree(key, false);
        }
    });
}

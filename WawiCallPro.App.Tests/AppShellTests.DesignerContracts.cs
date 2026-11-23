using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Resources;
using DevExpress.LookAndFeel;
using DevExpress.Utils;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using Microsoft.Win32;
using WawiCallPro.App.Hosting;
using WawiCallPro.App.Views;
using WawiCallPro.Plugin.Windows;
using WawiCallPro.Utilities.Hosting;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    public Task AllOwnDesignerSurfacesConstructWithoutRuntimeServicesAndExposeOnlyExplicitDesignerProperties() => RunStaAsync(() =>
    {
        var surfaces = new[] { typeof(MainForm).Assembly, typeof(PartySearchPage).Assembly }
            .SelectMany(assembly => assembly.GetTypes()).Where(type => !type.IsAbstract
                && (typeof(Form).IsAssignableFrom(type) || typeof(UserControl).IsAssignableFrom(type))).ToArray();
        CollectionAssert.AreEquivalent(new[] { "MainForm", "LogListBoxSearchController", "CallControlPanel", "CallDetailsPage",
            "CallerLookupPage", "CallNotesPanel", "CompanySelector", "DialKeypad", "FrmAudioConvert", "FrmCallerLookup", "FrmDatabaseSetup", "FrmDial", "FrmSettings", "FunctionPluginsPage",
            "FrmStartupSplashScreen", "HistoryPage", "HomeDashboard", "LogPanel", "MailPage", "MailSettingsPage",
            "PhoneNumberDiagnosticsPage", "PluginPage", "SettingsPage", "StartPage", "UcDbStep1DbType", "UcDbStep2Connection",
            "UcDbStep3Migration", "UcConMyPg", "UcConSqlite", "UcConSqlServer", "PartySearchPage", "PluginCompanySettingsPage" }, surfaces.Select(type => type.Name).ToArray(),
            "Neue direkte oder indirekte Oberflächen auch in die Nachweismatrix aufnehmen.");
        foreach (var type in surfaces.OrderBy(type => type.Name))
        {
            Assert.IsNotNull(type.GetConstructor(Type.EmptyTypes), type.FullName);
            using var control = (Control)Activator.CreateInstance(type)!;
            if (control is Form form) Assert.IsFalse(form.Visible, type.FullName);
            Assert.IsFalse(control.IsHandleCreated, type.FullName + " erstellt im Designer-Konstruktor ein natives Fenster.");
            var manager = new ResourceManager(type);
            try
            {
                Assert.IsNotNull(manager.GetResourceSet(CultureInfo.InvariantCulture, true, false), type.FullName);
                foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    Assert.IsTrue(property.IsDefined(typeof(DesignerSerializationVisibilityAttribute)) || property.IsDefined(typeof(DefaultValueAttribute)),
                        type.Name + "." + property.Name + " hat keine explizite Designer-Serialisierung.");
                    _ = TypeDescriptor.GetProperties(control)[property.Name]!.ShouldSerializeValue(control);
                }
                Console.WriteLine(type.FullName + ": Konstruktor, eingebettete Ressource und Designer-Properties bestanden.");
            }
            finally { manager.ReleaseAllResources(); }
        }
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task DesignerWindowAndGridPersistenceUsesIsolatedKeysAndExcludesSessionFilters() => RunStaAsync(() =>
    {
        var product = "DesignerAudit-" + Guid.NewGuid().ToString("N");
        var root = @"Software\WawiCallPro\" + product;
        var descriptor = new ApplicationDescriptor("Isolated designer audit", product);
        var state = new AppUiState(descriptor, new(false));
        var suppressed = new AppUiState(descriptor, new(true));
        try
        {
            foreach (var type in new[] { typeof(MainForm), typeof(FrmSettings), typeof(FrmDial), typeof(FrmCallerLookup), typeof(FrmDatabaseSetup) })
            {
                using var form = (Form)Activator.CreateInstance(type)!;
                Assert.AreEqual(FormBorderStyle.Sizable, form.FormBorderStyle, type.Name);
                var minimum = form.MinimumSize;
                var area = Screen.PrimaryScreen!.WorkingArea;
                form.Bounds = new Rectangle(area.Left + 8, area.Top + 8, Math.Max(minimum.Width, 900), Math.Max(minimum.Height, 740));
                _ = form.Handle;
                state.SaveWindow(form, type.Name);
                var saved = form.Bounds;
                form.Location = new Point(area.Left + 30, area.Top + 30);
                state.RestoreWindow(form, type.Name);
                Assert.AreEqual(saved, form.Bounds, type.Name);
                suppressed.SaveWindow(form, "Suppressed-" + type.Name);
            }
            GridControl CreateGrid()
            {
                var grid = new GridControl { DataSource = new[] { new { Name = "One", Number = 1 }, new { Name = "Two", Number = 2 } } };
                var view = new GridView(grid);
                grid.MainView = view;
                view.Columns.AddVisible("Name"); view.Columns.AddVisible("Number");
                _ = grid.Handle; grid.ForceInitialize();
                return grid;
            }
            using (var grid = CreateGrid())
            {
                var view = (GridView)grid.MainView;
                state.Restore(view, "Columns");
                view.OptionsView.ColumnAutoWidth = false;
                view.Columns["Name"].Width = 231;
                view.Columns["Number"].VisibleIndex = 0;
                view.ActiveFilterString = "[Number] = 1";
                view.ApplyFindFilter("One");
                state.Save(view, "Columns");
                suppressed.Save(view, "SuppressedGrid");
            }
            using (var grid = CreateGrid())
            {
                var view = (GridView)grid.MainView;
                view.OptionsView.ColumnAutoWidth = false;
                state.Restore(view, "Columns");
                Assert.AreEqual(231, view.Columns["Name"].Width);
                Assert.AreEqual(0, view.Columns["Number"].VisibleIndex);
                Assert.IsTrue(string.IsNullOrEmpty(view.ActiveFilterString));
                Assert.IsTrue(string.IsNullOrEmpty(view.FindFilterText));
                Assert.IsFalse(view.OptionsLayout.StoreDataSettings);
            }
            using (var grid = new GridControl())
            {
                var tiles = new DevExpress.XtraGrid.Views.Tile.TileView(grid);
                grid.MainView = tiles; tiles.Columns.AddVisible("Name"); tiles.Columns.AddVisible("Number");
                state.Restore(tiles, "MailTiles");
                tiles.ColumnSet.GroupColumn = tiles.Columns["Name"];
                state.Save(tiles, "MailTiles");
                suppressed.Save(tiles, "SuppressedTiles");
            }
            using (var grid = new GridControl())
            {
                var tiles = new DevExpress.XtraGrid.Views.Tile.TileView(grid);
                grid.MainView = tiles; tiles.Columns.AddVisible("Name"); tiles.Columns.AddVisible("Number");
                state.Restore(tiles, "MailTiles");
                Assert.AreEqual("Name", tiles.ColumnSet.GroupColumn?.FieldName);
                Assert.IsFalse(tiles.OptionsLayout.StoreDataSettings);
            }
            using var windows = Registry.CurrentUser.OpenSubKey(root + @"\App\v1\Windows\v2");
            Assert.IsNotNull(windows);
            Assert.AreEqual(5, windows.SubKeyCount);
            using var suppressedGrid = Registry.CurrentUser.OpenSubKey(root + @"\App\v1\Grids\SuppressedGrid");
            Assert.IsNull(suppressedGrid);
            using var suppressedTiles = Registry.CurrentUser.OpenSubKey(root + @"\App\v1\Grids\SuppressedTiles");
            Assert.IsNull(suppressedTiles);
        }
        finally { Registry.CurrentUser.DeleteSubKeyTree(root, false); }
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task ButtonInsetsAndBrowseCursorSurviveAppearanceChangesCancellationAndErrors() => RunStaAsync(() =>
    {
        using var owner = new XtraUserControl { Cursor = Cursors.Cross };
        foreach (var fail in new[] { false, true })
        {
            try
            {
                using var waiting = new UiWaitCursor(owner);
                Assert.IsTrue(owner.UseWaitCursor); Assert.AreEqual(Cursors.WaitCursor, owner.Cursor);
                using (new UiWaitCursor(owner)) { }
                Assert.IsTrue(owner.UseWaitCursor); Assert.AreEqual(Cursors.WaitCursor, owner.Cursor);
                if (fail) throw new InvalidOperationException("Synthetic browse error");
            }
            catch (InvalidOperationException) when (fail) { }
            Assert.IsFalse(owner.UseWaitCursor); Assert.AreEqual(Cursors.Cross, owner.Cursor);
        }
        using var first = new SimpleButton { Text = "Einstellungen speichern", AutoSize = true };
        using var second = new SimpleButton { Text = "Abbrechen" };
        using var dialog = new XtraForm();
        dialog.Controls.Add(first); dialog.Controls.Add(second);
        UiButtonSizing.Apply(first, second);
        foreach (var button in new[] { first, second })
        {
            Assert.IsTrue(button.Padding.Left >= button.LogicalToDeviceUnits(12));
            Assert.IsTrue(button.Height >= button.CalcBestSize().Height);
            Assert.IsTrue(button.Width >= button.GetPreferredSize(Size.Empty).Width);
        }
        Assert.AreEqual(first.Size, second.Size);
        using var font = new Font("Segoe UI", 16F);
        first.Font = second.Font = font;
        Assert.AreEqual(first.Size, second.Size);
        Assert.IsTrue(first.Padding.Left >= first.LogicalToDeviceUnits(12));
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task MigrationFailureLayoutGapRendersLongDiagnosticsWithoutVisibleWindows() => RunStaAsync(() =>
    {
        AppUiStartup.Apply();
        var oldFont = WindowsFormsSettings.DefaultFont;
        var oldAppearance = AppearanceObject.DefaultFont;
        var oldSkin = UserLookAndFeel.Default.ActiveSkinName;
        var oldPalette = UserLookAndFeel.Default.ActiveSvgPaletteName;
        var output = Path.Combine(RenderOutput(), "phase8-a4-migration"); Directory.CreateDirectory(output);
        try
        {
            foreach (var skin in new[] { "Basic", "Office 2019 Black" })
            foreach (var size in new[] { 10F, 16F })
            {
                UserLookAndFeel.Default.SetSkinStyle(skin);
                using var font = new Font("Segoe UI", size);
                WindowsFormsSettings.DefaultFont = AppearanceObject.DefaultFont = font;
                using var host = new MainForm(); host.PrepareForBackgroundRun(); host.Font = font;
                using var page = new UcDbStep3Migration { Font = font, Size = new Size(640, 400) };
                page.ShowResult(false, "Die Datenbank konnte nicht eingerichtet werden. Bitte Serveradresse, Zugriffsrechte und Verbindung prüfen.\r\n\r\nDie vorhandene Konfiguration wurde beibehalten. Wiederholen Sie die Einrichtung nach Behebung des Verbindungsfehlers.");
                host.Controls.Add(page); page.BringToFront(); host.Show(); CreateHandles(page); page.PerformLayout();
                foreach (var scaled in new[] { false, true })
                {
                    if (scaled) { page.Scale(new SizeF(1.5F, 1.5F)); page.PerformLayout(); }
                    CheckTableBounds(page);
                    var result = Field<MemoEdit>(page, "memoResult");
                    Assert.IsTrue(result.Visible); Assert.Contains("nicht abgeschlossen", result.Text); Assert.IsTrue(result.Properties.ReadOnly);
                    using var image = new Bitmap(page.Width, page.Height); page.DrawToBitmap(image, new Rectangle(Point.Empty, page.Size));
                    image.Save(Path.Combine(output, $"MigrationFailure-{skin}-{size}-{(scaled ? "scale150" : "normal")}.png"));
                }
                Assert.AreEqual(0d, host.Opacity); Assert.IsFalse(host.ShowInTaskbar);
            }
        }
        finally { WindowsFormsSettings.DefaultFont = oldFont; AppearanceObject.DefaultFont = oldAppearance; UserLookAndFeel.Default.SetSkinStyle(oldSkin, oldPalette); }
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task StartPageLayoutGapRendersEmptyAndWrappedContentWithoutVisibleWindows() => RunStaAsync(() =>
    {
        AppUiStartup.Apply();
        var oldFont = WindowsFormsSettings.DefaultFont;
        var oldAppearance = AppearanceObject.DefaultFont;
        var oldSkin = UserLookAndFeel.Default.ActiveSkinName;
        var oldPalette = UserLookAndFeel.Default.ActiveSvgPaletteName;
        var output = Path.Combine(RenderOutput(), "phase8-a4-start"); Directory.CreateDirectory(output);
        try
        {
            foreach (var skin in new[] { "Basic", "Office 2019 Black" })
            foreach (var size in new[] { 10F, 16F })
            foreach (var filled in new[] { false, true })
            {
                UserLookAndFeel.Default.SetSkinStyle(skin);
                using var font = new Font("Segoe UI", size);
                WindowsFormsSettings.DefaultFont = AppearanceObject.DefaultFont = font;
                using var host = new MainForm(); host.PrepareForBackgroundRun(); host.Font = font;
                using var page = new StartPage { Font = font, Size = new Size(680, 540) };
                page.SetContent(filled ? "Arbeitsbereich vorbereiten" : "", filled ? "Noch keine Daten verfügbar. Öffnen Sie die Einstellungen und richten Sie die Kommunikationsdatenbank und Ihre Warenwirtschaft ein." : "", filled ? "Bereit für die Einrichtung" : "");
                host.Controls.Add(page); page.BringToFront(); host.Show(); CreateHandles(page); page.PerformLayout();
                CheckTableBounds(page);
                foreach (var scaled in new[] { false, true })
                {
                    if (scaled) { page.Scale(new SizeF(1.5F, 1.5F)); page.PerformLayout(); }
                    CheckTableBounds(page);
                    using var image = new Bitmap(page.Width, page.Height); page.DrawToBitmap(image, new Rectangle(Point.Empty, page.Size));
                    image.Save(Path.Combine(output, $"Start-{skin}-{size}-{filled}-{(scaled ? "scale150" : "normal")}.png"));
                }
                Assert.AreEqual(0d, host.Opacity); Assert.IsFalse(host.ShowInTaskbar);
            }
        }
        finally { WindowsFormsSettings.DefaultFont = oldFont; AppearanceObject.DefaultFont = oldAppearance; UserLookAndFeel.Default.SetSkinStyle(oldSkin, oldPalette); }
        return Task.CompletedTask;
    });
}

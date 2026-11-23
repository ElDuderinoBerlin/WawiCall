using System.Globalization;
using DevExpress.LookAndFeel;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraBars.Docking;
using Microsoft.Win32;
using WawiCallPro.Utilities.Hosting;
using WawiCallPro.Utilities.Licensing;
using WawiCallPro.Utilities.Diagnostics;

namespace WawiCallPro.App.Hosting;

/// <summary>Versioned per-user appearance/layout preferences. Diagnostics and render runs never persist them.</summary>
public sealed class AppUiState(ApplicationDescriptor application, StartupExecutionMode mode)
{
    private readonly IWindowDisplayEnvironment _displays = new WindowDisplayEnvironment();

    internal AppUiState(ApplicationDescriptor application, StartupExecutionMode mode, IWindowDisplayEnvironment displays)
        : this(application, mode) => _displays = displays;

    private string KeyPath => @"Software\WawiCallPro\" + application.ProductLine + @"\App\v1";
    private bool Enabled => !mode.SuppressNetwork && !mode.IsolatedProfile;

    internal void Restore(Form form)
    {
        if (!Enabled) return;
        WindowWorkArea.Attach(form);
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
            ApplyAppearance(form, key);
            var placement = ReadWindow("MainForm");
            if (placement is null && key?.GetValue("Width") is int width && key.GetValue("Height") is int height
                && width > 0 && height > 0 && width <= 1000000 && height <= 1000000)
                placement = new WindowPlacement(new Rectangle(form.Location, new Size(width, height)), 0,
                    Rectangle.Empty, "", key.GetValue("Maximized") is int maximized && maximized == 1);
            if (placement is { } saved) ApplyWindow(form, saved);
        }
        catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { }
    }

    internal void RestoreAppearance()
    {
        if (!Enabled) return;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
            ApplyAppearance(null, key);
        }
        catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { }
    }

    private static void ApplyAppearance(Form? form, RegistryKey? key)
    {
        if (key?.GetValue("Skin") is string skin)
        {
            if (key.GetValue("Palette") is string palette && palette.Length != 0) UserLookAndFeel.Default.SetSkinStyle(skin, palette);
            else UserLookAndFeel.Default.SetSkinStyle(skin);
        }

        if (key?.GetValue("Font") is string text && new FontConverter().ConvertFromInvariantString(text) is Font font)
        {
            WindowsFormsSettings.DefaultFont = DevExpress.Utils.AppearanceObject.DefaultFont = font;
            if (form is not null) form.Font = font;
        }
    }

    internal void Save(Form form)
    {
        if (!Enabled) return;
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
            var bounds = form.WindowState == FormWindowState.Normal ? form.Bounds : form.RestoreBounds;
            key.SetValue("Width", bounds.Width); key.SetValue("Height", bounds.Height);
            key.SetValue("Maximized", form.WindowState == FormWindowState.Maximized ? 1 : 0);
            key.SetValue("Skin", UserLookAndFeel.Default.SkinName);
            key.SetValue("Palette", UserLookAndFeel.Default.ActiveSvgPaletteName ?? "");
            key.SetValue("Font", new FontConverter().ConvertToInvariantString(WindowsFormsSettings.DefaultFont) ?? "");
            SaveWindow(form, "MainForm");
        }
        catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException or ArgumentException) { }
    }

    /// <summary>Stores each user-facing dialog independently; audits and diagnostics leave HKCU unchanged.</summary>
    internal void AttachWindow(Form form, string name)
    {
        if (!Enabled) return;
        WindowWorkArea.Attach(form);
        form.Load += (_, _) => RestoreWindow(form, name);
        form.FormClosed += (_, _) => SaveWindow(form, name);
    }

    internal DialogResult ShowSaveDialog(XtraSaveFileDialog dialog, Control owner, string name) =>
        ShowFileDialog(dialog.Title, "DevExpress.XtraEditors.SaveFileDialogForm", name, () => dialog.ShowDialog(owner));

    internal DialogResult ShowOpenDialog(XtraOpenFileDialog dialog, Control owner, string name) =>
        ShowFileDialog(dialog.Title, "DevExpress.XtraEditors.OpenFileDialogForm", name, () => dialog.ShowDialog(owner));

    private DialogResult ShowFileDialog(string title, string dialogType, string name, Func<DialogResult> show)
    {
        if (!Enabled) return show();

        // The DevExpress component creates its form inside the modal loop.
        using var attach = new System.Windows.Forms.Timer { Interval = 25 };
        attach.Tick += (_, _) =>
        {
            var window = System.Windows.Forms.Application.OpenForms.Cast<Form>().FirstOrDefault(form =>
                form.Text == title && form.GetType().FullName == dialogType);
            if (window is null) return;
            attach.Stop();
            WindowWorkArea.Attach(window);
            RestoreWindow(window, name);
            window.FormClosed += (_, _) => SaveWindow(window, name);
        };
        attach.Start();
        return show();
    }

    internal void RestoreWindow(Form form, string name)
    {
        if (!Enabled) return;
        try
        {
            if (ReadWindow(name) is { } placement) ApplyWindow(form, placement);
        }
        catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { }
    }

    internal void SaveWindow(Form form, string name)
    {
        if (!Enabled) return;
        try
        {
            var bounds = form.WindowState == FormWindowState.Normal ? form.Bounds : form.RestoreBounds;
            if (bounds.Width <= 0 || bounds.Height <= 0) bounds = form.Bounds;
            var monitor = _displays.GetCurrent(form);
            using var key = Registry.CurrentUser.CreateSubKey(KeyPath + @"\Windows\v2\" + name);
            key.SetValue("Version", 2);
            key.SetValue("Left", bounds.Left); key.SetValue("Top", bounds.Top);
            key.SetValue("Width", bounds.Width); key.SetValue("Height", bounds.Height);
            key.SetValue("Maximized", form.WindowState == FormWindowState.Maximized ? 1 : 0);
            key.SetValue("Dpi", _displays.GetDpi(form));
            key.SetValue("Monitor", monitor.Name);
            key.SetValue("WorkLeft", monitor.WorkingArea.Left); key.SetValue("WorkTop", monitor.WorkingArea.Top);
            key.SetValue("WorkWidth", monitor.WorkingArea.Width); key.SetValue("WorkHeight", monitor.WorkingArea.Height);
        }
        catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException or ArgumentException) { }
    }

    private WindowPlacement? ReadWindow(string name)
    {
        using var current = Registry.CurrentUser.OpenSubKey(KeyPath + @"\Windows\v2\" + name);
        if (ReadPlacement(current, true) is { } placement) return placement;
        using var legacy = Registry.CurrentUser.OpenSubKey(KeyPath + @"\Windows\" + name);
        return ReadPlacement(legacy, false);
    }

    private static WindowPlacement? ReadPlacement(RegistryKey? key, bool versioned)
    {
        if (key?.GetValue("Left") is not int left || key.GetValue("Top") is not int top
            || key.GetValue("Width") is not int width || key.GetValue("Height") is not int height
            || width <= 0 || height <= 0 || width > 1000000 || height > 1000000
            || left is < -1000000 or > 1000000 || top is < -1000000 or > 1000000) return null;
        var maximized = key.GetValue("Maximized") is int value && value == 1;
        var bounds = new Rectangle(left, top, width, height);
        if (!versioned) return new WindowPlacement(bounds, 0, Rectangle.Empty, "", maximized);
        if (key.GetValue("Version") is not int version || version != 2
            || key.GetValue("Dpi") is not int dpi || dpi is < 48 or > 768
            || key.GetValue("Monitor") is not string monitor
            || key.GetValue("WorkLeft") is not int workLeft || key.GetValue("WorkTop") is not int workTop
            || key.GetValue("WorkWidth") is not int workWidth || key.GetValue("WorkHeight") is not int workHeight
            || workWidth <= 0 || workHeight <= 0 || workWidth > 1000000 || workHeight > 1000000
            || workLeft is < -1000000 or > 1000000 || workTop is < -1000000 or > 1000000) return null;
        return new WindowPlacement(bounds, dpi, new Rectangle(workLeft, workTop, workWidth, workHeight), monitor, maximized);
    }

    private void ApplyWindow(Form form, WindowPlacement placement)
    {
        var monitor = _displays.Select(placement.Monitor, placement.Bounds);
        var area = monitor.WorkingArea;
        var sizable = form.FormBorderStyle is FormBorderStyle.Sizable or FormBorderStyle.SizableToolWindow;
        form.StartPosition = FormStartPosition.Manual;
        form.WindowState = FormWindowState.Normal;
        var dpi = _displays.PrepareRestore(form, monitor);
        var bounds = placement.GetBounds(dpi, area);
        if (!sizable) bounds.Size = form.Size;
        form.MinimumSize = WindowWorkArea.LimitMinimum(form.MinimumSize, area);
        form.Bounds = WindowWorkArea.Fit(bounds, form.MinimumSize, area);
        if (sizable && placement.Maximized) form.WindowState = FormWindowState.Maximized;
    }

    internal void Restore(GridView view, string name)
    {
        view.OptionsLayout.StoreAllOptions = false;
        view.OptionsLayout.StoreDataSettings = false;
        view.OptionsLayout.Columns.StoreAllOptions = true;
        if (Enabled) view.RestoreLayoutFromRegistry(KeyPath + @"\Grids\" + name);
    }
    internal void Save(GridView view, string name)
    {
        if (Enabled) view.SaveLayoutToRegistry(KeyPath + @"\Grids\" + name);
    }
    internal void Restore(DevExpress.XtraGrid.Views.Tile.TileView view, string name)
    {
        view.OptionsLayout.StoreAllOptions = false;
        view.OptionsLayout.StoreDataSettings = false;
        view.OptionsLayout.StoreColumnSet = true;
        if (Enabled) view.RestoreLayoutFromRegistry(KeyPath + @"\Grids\" + name);
    }
    internal void Save(DevExpress.XtraGrid.Views.Tile.TileView view, string name)
    {
        if (Enabled) view.SaveLayoutToRegistry(KeyPath + @"\Grids\" + name);
    }
    internal void Reset()
    {
        if (Enabled) Registry.CurrentUser.DeleteSubKeyTree(KeyPath, false);
    }
    internal void Restore(DockManager manager)
    {
        if (Enabled) manager.RestoreLayoutFromRegistry(KeyPath + @"\Docking");
    }
    internal void Save(DockManager manager)
    {
        if (Enabled) manager.SaveLayoutToRegistry(KeyPath + @"\Docking");
    }
}

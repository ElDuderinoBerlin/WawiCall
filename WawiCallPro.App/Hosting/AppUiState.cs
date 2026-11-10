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
    private string KeyPath => @"Software\WawiCallPro\" + application.ProductLine + @"\App\v1";
    private bool Enabled => !mode.SuppressNetwork;

    internal void Restore(Form form)
    {
        if (!Enabled) return;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
            ApplyAppearance(form, key);
            if (key?.GetValue("Width") is int width && key.GetValue("Height") is int height)
                form.Size = new Size(Math.Clamp(width, 760, 2400), Math.Clamp(height, 520, 1600));
            if (key?.GetValue("Maximized") is int maximized && maximized == 1) form.WindowState = FormWindowState.Maximized;
            RestoreWindow(form, "MainForm");
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
        form.Load += (_, _) => RestoreWindow(form, name);
        form.FormClosed += (_, _) => SaveWindow(form, name);
    }

    internal void RestoreWindow(Form form, string name)
    {
        if (!Enabled) return;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath + @"\Windows\" + name);
            if (key?.GetValue("Left") is not int left || key.GetValue("Top") is not int top
                || key.GetValue("Width") is not int width || key.GetValue("Height") is not int height) return;
            var area = Screen.FromRectangle(new Rectangle(left, top, Math.Max(1, width), Math.Max(1, height))).WorkingArea;
            var sizable = form.FormBorderStyle is FormBorderStyle.Sizable or FormBorderStyle.SizableToolWindow;
            var restoredWidth = sizable ? Math.Clamp(width, Math.Max(1, form.MinimumSize.Width), Math.Max(form.MinimumSize.Width, area.Width)) : form.Width;
            var restoredHeight = sizable ? Math.Clamp(height, Math.Max(1, form.MinimumSize.Height), Math.Max(form.MinimumSize.Height, area.Height)) : form.Height;
            form.StartPosition = FormStartPosition.Manual;
            form.WindowState = FormWindowState.Normal;
            form.Bounds = new Rectangle(Math.Clamp(left, area.Left, Math.Max(area.Left, area.Right - restoredWidth)),
                Math.Clamp(top, area.Top, Math.Max(area.Top, area.Bottom - restoredHeight)), restoredWidth, restoredHeight);
            if (sizable && key.GetValue("Maximized") is int maximized && maximized == 1)
                form.WindowState = FormWindowState.Maximized;
        }
        catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { }
    }

    internal void SaveWindow(Form form, string name)
    {
        if (!Enabled) return;
        try
        {
            var bounds = form.WindowState == FormWindowState.Normal ? form.Bounds : form.RestoreBounds;
            using var key = Registry.CurrentUser.CreateSubKey(KeyPath + @"\Windows\" + name);
            key.SetValue("Left", bounds.Left); key.SetValue("Top", bounds.Top);
            key.SetValue("Width", bounds.Width); key.SetValue("Height", bounds.Height);
            key.SetValue("Maximized", form.WindowState == FormWindowState.Maximized ? 1 : 0);
        }
        catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException or ArgumentException) { }
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

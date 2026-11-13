using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using DevExpress.LookAndFeel;
using DevExpress.Utils;
using DevExpress.XtraBars;
using DevExpress.XtraEditors;
using Quantarix.Windows.Win32;
using WawiCallPro.App.Hosting;

namespace WawiCallPro.App.Diagnostics;

/// <summary>Explicit, visible acceptance mode; invoked only by --ui-audit with the TrayNative surface.</summary>
internal sealed class NativeTrayAudit(string directory)
{
    private readonly List<object> _results = [];
    internal static bool IsRequested => Environment.GetEnvironmentVariable("LX_UI_AUDIT_SURFACES") == "TrayNative";
    internal async Task RunAsync(MainForm form, Action ready, CancellationToken token)
    {
        Directory.CreateDirectory(directory);
        var originalFont = WindowsFormsSettings.DefaultFont;
        var originalAppearance = AppearanceObject.DefaultFont;
        var originalSkin = UserLookAndFeel.Default.ActiveSkinName;
        var originalPalette = UserLookAndFeel.Default.ActiveSvgPaletteName;
        try
        {
            form.StartPosition = FormStartPosition.Manual; form.Location = new(80, 70); form.ClientSize = new(1000, 700);
            form.ShowAfterStartup(false);
            form.Text = "WawiCallPro - Native tray audit " + Environment.ProcessId;
            // The explicitly opted-in launcher activates this exact window once via Windows UI Automation.
            // No later test corrects a failed foreground transition.
            await File.WriteAllTextAsync(Path.Combine(directory, "activation-requested"), form.Handle.ToInt64().ToString(), token);
            using (var initial = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                initial.CancelAfter(TimeSpan.FromSeconds(15));
                while (!File.Exists(Path.Combine(directory, "activated"))) await Task.Delay(50, initial.Token);
            }
            await CheckAsync("initial", form, token);
            // The application context deliberately keeps the activation request pending until ready().
            form.WindowState = FormWindowState.Minimized;
            await StartSecondProcessAsync(false, token);
            Require(form.WindowState == FormWindowState.Minimized, "Early request was consumed before UI readiness.");
            ready(); await CheckAsync("during-startup-dotnet", form, token);

            form.WindowState = FormWindowState.Maximized;
            form.WindowState = FormWindowState.Minimized;
            await StartSecondProcessAsync(true, token);
            await CheckAsync("minimized-from-maximized-explorer", form, token);
            Require(form.WindowState == FormWindowState.Maximized, "Maximized state was not preserved.");
            form.WindowState = FormWindowState.Normal;
            Require(form.MoveToTray(), "Tray move failed.");
            await StartSecondProcessAsync(true, token);
            await CheckAsync("hidden-explorer", form, token);
            Require(form.Visible, "The hidden window was not shown.");
            using (var companion = new Views.FrmCallerLookup { Text = "WawiCallPro – Vordergrund-Testfenster", StartPosition = FormStartPosition.Manual, Location = new(180, 120) })
            {
                companion.Show(); companion.Activate(); await CheckAsync("companion-foreground", companion, token);
                await StartSecondProcessAsync(true, token); await CheckAsync("background-explorer", form, token);
                Require(form.MoveToTray(), "Tray move failed.");
                companion.Activate();
                var show = (BarButtonItem)typeof(MainForm).GetField("trayShowItem", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
                show.PerformClick();
                await CheckAsync("tray-show-command", form, token);
                await companion.StopAsync(); companion.Close();
            }
            using (var modal = form.CreateSettingsDialog())
            {
                Exception? failure = null;
                modal.Shown += async (_, _) =>
                {
                    try
                    {
                        await CheckAsync("modal-before", modal, token);
                        await StartSecondProcessAsync(true, token);
                        await CheckAsync("modal-explorer", modal, token);
                        Require(!form.MoveToTray(), "A modal dialog must prevent hiding its owner.");
                    }
                    catch (Exception exception) { failure = exception; }
                    finally { modal.Close(); }
                };
                modal.ShowDialog(form);
                if (failure is not null) throw failure;
            }
            var menu = (PopupMenu)typeof(MainForm).GetField("trayMenu", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
            Require(form.MoveToTray(), "Tray menu requires a hidden main window.");
            var icon = (NotifyIcon)typeof(MainForm).GetField("tray", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
            Require(!form.Visible && icon.Visible, "The real tray icon was not available for the hidden window.");
            var smallMenu = Size.Empty;
            foreach (var (skin, palette, size) in new[] { ("Basic", "Blue Dark", 10F), ("Basic", "Blue Dark", 16F), ("Office 2019 Black", "", 16F) })
            {
                using var font = new Font("Segoe UI", size);
                WindowsFormsSettings.DefaultFont = AppearanceObject.DefaultFont = font; form.Font = font;
                if (palette.Length == 0) UserLookAndFeel.Default.SetSkinStyle(skin); else UserLookAndFeel.Default.SetSkinStyle(skin, palette);
                menu.ShowPopup(new Point(form.Left + 60, form.Top + 130));
                try
                {
                    await Task.Delay(200, token);
                    Require(menu.Visible, "The actual DevExpress tray menu was not visible.");
                    var rectangle = FindPopupBounds(form.Handle);
                    if (size == 10F) smallMenu = rectangle.Size;
                    else Require(rectangle.Width > smallMenu.Width && rectangle.Height > smallMenu.Height, "Tray menu did not adapt to the larger UI font.");
                    using var shot = new Bitmap(rectangle.Width, rectangle.Height);
                    using (var graphics = Graphics.FromImage(shot)) graphics.CopyFromScreen(rectangle.Location, Point.Empty, rectangle.Size);
                    shot.Save(Path.Combine(directory, $"tray-{skin}-{palette}-{size:0}.png"), ImageFormat.Png);
                    _results.Add(new { Case = "menu", Skin = skin, Font = size, Dpi = form.DeviceDpi, Bounds = rectangle.ToString() });
                }
                finally { menu.HidePopup(); }
            }
        }
        catch (Exception exception)
        {
            _results.Add(new { Case = "failure", Message = exception.Message });
            throw;
        }
        finally
        {
            WindowsFormsSettings.DefaultFont = originalFont; AppearanceObject.DefaultFont = originalAppearance;
            if (originalPalette.Length == 0) UserLookAndFeel.Default.SetSkinStyle(originalSkin); else UserLookAndFeel.Default.SetSkinStyle(originalSkin, originalPalette);
            var assembly = typeof(MainForm).Assembly;
            await File.WriteAllTextAsync(Path.Combine(directory, "native-tray.json"), JsonSerializer.Serialize(new
            {
                Exe = Environment.ProcessPath, ProcessId = Environment.ProcessId, Assembly = assembly.Location,
                Build = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                Sha256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(assembly.Location))), Results = _results
            }, new JsonSerializerOptions { WriteIndented = true }));
            await form.PrepareToCloseAsync();
        }
    }
    private async Task CheckAsync(string name, Form target, CancellationToken token)
    {
        await Task.Delay(300, token);
        var foreground = User32.GetForegroundWindow();
        _results.Add(new { Case = name, Expected = target.Handle.ToInt64(), Foreground = foreground.ToInt64(), Visible = target.Visible, State = target.WindowState.ToString() });
        Require(foreground == target.Handle, "Native foreground check failed: " + name);
    }
    private async Task StartSecondProcessAsync(bool explorer, CancellationToken token)
    {
        var exe = Environment.ProcessPath ?? throw new InvalidOperationException("Executable unavailable.");
        if (explorer)
        {
            var profile = Environment.GetEnvironmentVariable("WAWICALLPRO_CONFIG_DIR");
            Require(profile is not null && Path.IsPathFullyQualified(profile) && profile.IndexOfAny(['\"', '\r', '\n']) < 0, "An explicit isolated activation profile is required.");
            // Explorer does not inherit the audit environment. Carry the target explicitly and never start a host.
            dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")!)!;
            try { shell.ShellExecute(exe, "--activate-existing \"" + profile + "\"", AppContext.BaseDirectory, "open", 1); }
            finally { Marshal.FinalReleaseComObject(shell); }
            await Task.Delay(800, token);
            return;
        }
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? @"C:\Program Files\dotnet\dotnet.exe")
        { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, WorkingDirectory = AppContext.BaseDirectory };
        start.ArgumentList.Add(typeof(MainForm).Assembly.Location);
        using var second = Process.Start(start) ?? throw new InvalidOperationException("Second launch failed.");
        await second.WaitForExitAsync(token); Require(second.ExitCode == 0, "Second dotnet launch failed.");
        await Task.Delay(500, token);
    }
    private static Rectangle FindPopupBounds(IntPtr main)
    {
        var found = Rectangle.Empty;
        EnumThreadWindows(GetCurrentThreadId(), (window, _) =>
        {
            if (window != main && IsWindowVisible(window) && GetWindowRect(window, out var bounds))
            {
                var rectangle = Rectangle.FromLTRB(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);
                if (rectangle.Width > 80 && rectangle.Height > 40 && rectangle.Width < 900 && rectangle.Height < 700 && rectangle.Width * rectangle.Height > found.Width * found.Height)
                    found = rectangle;
            }
            return true;
        }, IntPtr.Zero);
        Require(!found.IsEmpty, "Native popup bounds not found.");
        return found;
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    private delegate bool WindowCallback(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool EnumThreadWindows(uint thread, WindowCallback callback, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out NativeRect rectangle);
}

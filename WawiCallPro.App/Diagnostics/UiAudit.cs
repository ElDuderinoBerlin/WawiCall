using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using DevExpress.XtraEditors;

namespace WawiCallPro.App.Diagnostics;

internal static class UiAudit
{
    internal static void Render(string outputDirectory)
    {
        Render(outputDirectory, () => new MainForm());
    }

    internal static void Render(string outputDirectory, Func<MainForm> createForm)
    {
        var requested = Environment.GetEnvironmentVariable("LX_UI_AUDIT_SURFACES")?.Split(',', StringSplitOptions.TrimEntries);
        var surfaces = new[] { ("MainForm", "home"), ("SettingsPage", "settings"), ("DatabasePage", "database"), ("PluginPage", "plugin"), ("HistoryPage", "history") }
            .Where(surface => requested is null || requested.Contains(surface.Item1)).ToArray();
        if (surfaces.Length == 0) throw new ArgumentException("Keine bekannte Oberfläche für die Renderprüfung ausgewählt.");
        Directory.CreateDirectory(outputDirectory);
        foreach (var (surface, page) in surfaces)
        foreach (var fontSize in new[] { 10F, 16F })
        {
            foreach (var size in new[] { new Size(1240, 780), new Size(900, 700) })
            {
                using var font = new Font(SystemFonts.MessageBoxFont!.FontFamily, fontSize);
                var originalFont = WindowsFormsSettings.DefaultFont;
                try
                {
                    WindowsFormsSettings.DefaultFont = font;
                    using var form = createForm();
                    form.PrepareForBackgroundRun();
                    form.Font = font;
                    form.ClientSize = size;
                    _ = form.Handle;
                    typeof(Form).GetMethod("OnLoad", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, [EventArgs.Empty]);
                    form.Navigate(page);
                    form.MinimumSize = Size.Empty;
                    form.ClientSize = size;
                    form.PerformLayout();
                    CreateHandles(form);
                    form.Show();
                    Application.DoEvents();
                    form.Refresh();
                    using var bitmap = Capture(form);
                    bitmap.Save(Path.Combine(outputDirectory, $"{surface}-{size.Width}-{fontSize:0}.png"), ImageFormat.Png);
                    form.Scale(new SizeF(1.5F, 1.5F));
                    form.PerformLayout();
                    using var scaledBitmap = Capture(form);
                    scaledBitmap.Save(Path.Combine(outputDirectory, $"{surface}-{size.Width}-{fontSize:0}-scale150.png"), ImageFormat.Png);
                }
                finally
                {
                    WindowsFormsSettings.DefaultFont = originalFont;
                }
            }
        }
    }

    private static void CreateHandles(Control control)
    {
        _ = control.Handle;
        foreach (Control child in control.Controls) CreateHandles(child);
    }

    internal static Bitmap Capture(Form form)
    {
        var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
        // DocumentContainer's native print does not cascade into its hosted control.
        // Render that actual control at its measured screen position, without a screen capture.
        if (form is MainForm main && main.GetActiveDocumentView() is { Width: > 0, Height: > 0 } view)
        {
            using var graphics = Graphics.FromImage(bitmap);
            DrawHostedControl(view, form, graphics);
        }
        return bitmap;
    }

    private static void DrawHostedControl(Control control, Form form, Graphics graphics)
    {
        if (!control.Visible || control.Width <= 0 || control.Height <= 0) return;
        using var content = new Bitmap(control.Width, control.Height);
        control.DrawToBitmap(content, new Rectangle(Point.Empty, control.Size));
        var location = control.PointToScreen(Point.Empty);
        var state = graphics.Save();
        graphics.IntersectClip(new Rectangle(location.X - form.Left, location.Y - form.Top, control.Width, control.Height));
        graphics.DrawImageUnscaled(content, location.X - form.Left, location.Y - form.Top);
        foreach (Control child in control.Controls) DrawHostedControl(child, form, graphics);
        graphics.Restore(state);
    }
}

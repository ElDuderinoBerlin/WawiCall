using System.Drawing.Imaging;
using System.IO;
using DevExpress.XtraEditors;

namespace WawiCallPro.App.Diagnostics;

internal static class UiAudit
{
    internal static void Render(string outputDirectory)
    {
        var requestedSurfaces = Environment.GetEnvironmentVariable("LX_UI_AUDIT_SURFACES");
        if (!string.IsNullOrWhiteSpace(requestedSurfaces) &&
            !requestedSurfaces.Split(',', StringSplitOptions.TrimEntries).Contains(nameof(MainForm)))
        {
            throw new ArgumentException("Für diese Renderprüfung ist MainForm auszuwählen.");
        }

        Directory.CreateDirectory(outputDirectory);
        foreach (var fontSize in new[] { 10F, 16F })
        {
            foreach (var size in new[] { new Size(720, 420), new Size(440, 260) })
            {
                using var font = new Font(SystemFonts.MessageBoxFont!.FontFamily, fontSize);
                var originalFont = WindowsFormsSettings.DefaultFont;
                try
                {
                    WindowsFormsSettings.DefaultFont = font;
                    using var form = new MainForm();
                    form.Font = font;
                    form.ClientSize = size;
                    form.Show();
                    Application.DoEvents();
                    using var bitmap = new Bitmap(form.Width, form.Height);
                    form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
                    bitmap.Save(Path.Combine(outputDirectory, $"MainForm-{size.Width}-{fontSize:0}.png"), ImageFormat.Png);
                    form.Scale(new SizeF(1.5F, 1.5F));
                    form.PerformLayout();
                    using var scaledBitmap = new Bitmap(form.Width, form.Height);
                    form.DrawToBitmap(scaledBitmap, new Rectangle(Point.Empty, form.Size));
                    scaledBitmap.Save(Path.Combine(outputDirectory, $"MainForm-{size.Width}-{fontSize:0}-scale150.png"), ImageFormat.Png);
                    form.Close();
                }
                finally
                {
                    WindowsFormsSettings.DefaultFont = originalFont;
                }
            }
        }
    }
}

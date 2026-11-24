using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using DevExpress.XtraEditors;

namespace WawiCallPro.App.Diagnostics;

internal static class UiAudit
{
    internal static Task Render(string outputDirectory) => Render(outputDirectory, () => new MainForm());

    internal static Task Render(string outputDirectory, Func<MainForm> createForm) => Render(outputDirectory, createForm, null);

    internal static async Task Render(string outputDirectory, Func<MainForm> createForm, string? selectedSurface)
    {
        var requested = selectedSurface is not null ? new[] { selectedSurface }
            : Environment.GetEnvironmentVariable("LX_UI_AUDIT_SURFACES")?.Split(',', StringSplitOptions.TrimEntries);
        var surfaces = new[] { ("MainForm", "home"), ("PartySearchPage", "parties"), ("FrmSettings", "settings"), ("PluginPage", "plugins"), ("MailSettingsPage", "settings"), ("FrmDial", "dial"), ("HistoryPage", "history"), ("MailPage", "mail"), ("CallerLookupPage", "caller"), ("CallDetailsPage", "call-details"), ("PhoneNumberDiagnosticsPage", "number-diagnostics") }
            .Where(surface => requested is null || requested.Contains(surface.Item1)).ToArray();
        if (surfaces.Length == 0) throw new ArgumentException("Keine bekannte Oberfläche für die Renderprüfung ausgewählt.");
        Directory.CreateDirectory(outputDirectory);
        foreach (var (surface, page) in surfaces)
        foreach (var fontSize in surface is "HistoryPage" or "FrmSettings" or "PluginPage" or "MailSettingsPage" ? new[] { 8F, 10F, 16F } : new[] { 10F, 16F })
        {
            foreach (var size in new[] { new Size(1240, 780), new Size(900, 700) })
            {
                using var font = new Font(fontSize == 8F ? "Tahoma" : SystemFonts.MessageBoxFont!.FontFamily.Name, fontSize);
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
                    if (surface == "PartySearchPage")
                    {
                        using var content = new WawiCallPro.Plugin.Windows.PartySearchPage { Dock = DockStyle.Fill };
                        form.Controls.Add(content); content.BringToFront(); CreateHandles(content); form.PerformLayout();
                        using var shot = new Bitmap(content.Width, content.Height);
                        content.DrawToBitmap(shot, new Rectangle(Point.Empty, content.Size));
                        shot.Save(Path.Combine(outputDirectory, $"PartySearchPage-{size.Width}-{fontSize:0}.png"), ImageFormat.Png);
                        continue;
                    }
                    if (surface == "PhoneNumberDiagnosticsPage")
                    {
                        using var content = new WawiCallPro.App.Views.PhoneNumberDiagnosticsPage { Dock = DockStyle.Fill };
                        form.Controls.Add(content); content.BringToFront(); CreateHandles(content); form.PerformLayout();
                        using var shot = new Bitmap(content.Width, content.Height);
                        content.DrawToBitmap(shot, new Rectangle(Point.Empty, content.Size));
                        shot.Save(Path.Combine(outputDirectory, $"PhoneNumberDiagnosticsPage-{size.Width}-{fontSize:0}.png"), ImageFormat.Png);
                        continue;
                    }
                    if (surface == "CallDetailsPage")
                    {
                        using var content = new WawiCallPro.App.Views.CallDetailsPage { Dock = DockStyle.Fill };
                        form.Controls.Add(content); content.BringToFront(); CreateHandles(content); form.PerformLayout();
                        using var shot = new Bitmap(content.Width, content.Height);
                        content.DrawToBitmap(shot, new Rectangle(Point.Empty, content.Size));
                        shot.Save(Path.Combine(outputDirectory, $"CallDetailsPage-{size.Width}-{fontSize:0}.png"), ImageFormat.Png);
                        continue;
                    }
                    if (surface == "CallerLookupPage")
                    {
                        using var lookup = form.CreateCallerDialog();
                        lookup.PrepareForBackgroundRun(); lookup.Font = font; lookup.ClientSize = size;
                        CreateHandles(lookup); lookup.PerformLayout();
                        using var shot = Capture(lookup);
                        shot.Save(Path.Combine(outputDirectory, $"CallerLookupPage-{size.Width}-{fontSize:0}.png"), ImageFormat.Png);
                        continue;
                    }
                    if (surface is "PluginPage" or "MailSettingsPage")
                    {
                        using var settings = form.CreateSettingsDialog(surface == "PluginPage");
                        if (surface == "MailSettingsPage") settings.SelectMailTab();
                        settings.PrepareForBackgroundRun(); settings.Font = font;
                        settings.MinimumSize = Size.Empty; settings.ClientSize = size;
                        await settings.PrepareAsync();
                        settings.Show(); Application.DoEvents();
                        CreateHandles(settings); settings.PerformLayout();
                        using var shot = Capture(settings);
                        shot.Save(Path.Combine(outputDirectory, $"{surface}-{size.Width}-{fontSize:0}.png"), ImageFormat.Png);
                        continue;
                    }
                    if (surface == "FrmSettings")
                    {
                        using var settings = form.CreateSettingsDialog();
                        settings.Font = font;
                        settings.MinimumSize = Size.Empty;
                        settings.ClientSize = size;
                        await settings.PrepareAsync();
                        foreach (DevExpress.XtraTab.XtraTabPage tab in settings.SettingsTabs.TabPages)
                        {
                            settings.SettingsTabs.SelectedTabPage = tab;
                            CreateHandles(settings);
                            settings.PerformLayout();
                            using var shot = Capture(settings);
                            var tabFileName = string.Concat(tab.Text.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
                            shot.Save(Path.Combine(outputDirectory, $"FrmSettings-{tabFileName}-{size.Width}-{fontSize:0}.png"), ImageFormat.Png);
                        }
                        using var reference = new SimpleButton { Text = "Aktualisieren", Font = font };
                        form.Controls.Add(reference); CreateHandles(reference);
                        Hosting.UiButtonSizing.Apply(reference);
                        var names = new[] { "importButton", "exportButton", "saveButton", "cancelButton", "logDirectoryBrowseButton" };
                        var footer = settings.Controls.Find("footer", true).Single();
                        var actions = names
                            .Select(name => (name == "logDirectoryBrowseButton" ? (Control)settings : footer)
                                .Controls.Find(name, true).OfType<SimpleButton>().Single()).ToArray();
                        var measurements = new
                        {
                            reference.DeviceDpi, Font = font.ToString(), ReferenceHeight = reference.Height,
                            Buttons = actions.Select(button => new { button.Name, button.Height, NativeHeight = button.CalcBestSize().Height }).ToArray()
                        };
                        File.WriteAllText(Path.Combine(outputDirectory, $"FrmSettings-{size.Width}-{fontSize:0}-metrics.json"),
                            System.Text.Json.JsonSerializer.Serialize(measurements, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                        if (actions.Any(button => button.Height != reference.Height) || footer.Controls.Find("reloadButton", true).Length != 0)
                            throw new InvalidOperationException("Settings buttons must match view actions and have no reload action.");
                        continue;
                    }
                    if (surface == "FrmDial")
                    {
                        using var dial = form.CreateDialDialog();
                        dial.PrepareForBackgroundRun(); dial.Font = font;
                        dial.ClientSize = new Size(Math.Min(size.Width, 620), dial.ClientSize.Height);
                        CreateHandles(dial); dial.PerformLayout();
                        using var shot = Capture(dial);
                        shot.Save(Path.Combine(outputDirectory, $"FrmDial-{size.Width}-{fontSize:0}.png"), ImageFormat.Png);
                        continue;
                    }
                    form.Navigate(page);
                    form.MinimumSize = Size.Empty;
                    form.ClientSize = size;
                    form.PerformLayout();
                    CreateHandles(form);
                    form.Show();
                    Application.DoEvents();
                    form.Refresh();
                    if (surface == "MailPage" && form.GetActiveDocumentView() is WawiCallPro.App.Views.MailPage mail)
                    {
                        using var mailBitmap = CaptureMail(mail);
                        mailBitmap.Save(Path.Combine(outputDirectory, $"MailPage-{size.Width}-{fontSize:0}.png"), ImageFormat.Png);
                        form.Scale(new SizeF(1.5F, 1.5F)); form.PerformLayout();
                        using var scaledMailBitmap = CaptureMail(mail);
                        scaledMailBitmap.Save(Path.Combine(outputDirectory, $"MailPage-{size.Width}-{fontSize:0}-scale150.png"), ImageFormat.Png);
                        continue;
                    }
                    if (surface == "HistoryPage" && form.GetActiveDocumentView() is WawiCallPro.App.Views.HistoryPage history)
                    {
                        var action = history.Controls.Find("refreshButton", true).OfType<DevExpress.XtraEditors.SimpleButton>().Single();
                        var from = history.Controls.Find("fromEdit", true).OfType<DevExpress.XtraEditors.DateEdit>().Single();
                        var to = history.Controls.Find("toEdit", true).OfType<DevExpress.XtraEditors.DateEdit>().Single();
                        var measurements = new
                        {
                            action.DeviceDpi, Font = action.Font.ToString(), Host = action.FindForm()?.GetType().FullName,
                            ButtonHeight = action.Height, FromHeight = from.Height, ToHeight = to.Height,
                            NativeHeight = action.CalcBestSize().Height, TextHeight = TextRenderer.MeasureText(action.Text, action.Font).Height,
                            Insets = action.Padding.Vertical, AppearanceFont = action.Appearance.Font.ToString(),
                            AppearanceFontEnabled = action.Appearance.Options.UseFont,
                            AllowedHeight = Math.Max(from.Height, to.Height) + action.LogicalToDeviceUnits(4)
                        };
                        File.WriteAllText(Path.Combine(outputDirectory, $"HistoryPage-{size.Width}-{fontSize:0}-metrics.json"),
                            System.Text.Json.JsonSerializer.Serialize(measurements, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                        if (measurements.ButtonHeight > measurements.AllowedHeight)
                            throw new InvalidOperationException("History toolbar action is too tall beside its date editors.");
                    }
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

    // RichEdit's WM_PRINT ignores the destination offset when a parent prints its children.
    // Print it separately at its actual client position instead of capturing the desktop.
    internal static Bitmap CaptureMail(WawiCallPro.App.Views.MailPage page)
    {
        var editor = page.Controls.Find("mailBody", true).OfType<DevExpress.XtraRichEdit.RichEditControl>().Single();
        var bitmap = new Bitmap(page.Width, page.Height);
        var wasVisible = editor.Visible;
        page.SuspendLayout();
        try
        {
            editor.Visible = false;
            page.DrawToBitmap(bitmap, new Rectangle(Point.Empty, page.Size));
        }
        catch { bitmap.Dispose(); throw; }
        finally { editor.Visible = wasVisible; page.ResumeLayout(false); }
        if (wasVisible)
        {
            using var content = new Bitmap(editor.Width, editor.Height);
            editor.DrawToBitmap(content, new Rectangle(Point.Empty, editor.Size));
            var location = page.PointToClient(editor.PointToScreen(Point.Empty));
            using var graphics = Graphics.FromImage(bitmap);
            graphics.SetClip(new Rectangle(location, editor.Size));
            graphics.DrawImageUnscaled(content, location);
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

using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.CompilerServices;
using DevExpress.LookAndFeel;
using DevExpress.Utils;
using DevExpress.XtraEditors;
using Quantarix.DevExpressExtensions.Licensing;
using WawiCallPro.App.Hosting;
using ApplicationIdentity = WawiCallPro.Utilities.ApplicationIdentity;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Hosting;
using WawiCallPro.Utilities.Security;

namespace WawiCallPro.App.Tests;

[TestClass, DoNotParallelize]
public sealed class StartupAppearanceTests
{
    [TestMethod]
    public async Task AffectedStartupViewsRenderWithoutShowingWindowsAtSmallBoundsAndLargerFonts()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { RenderAffectedViews(); completion.SetResult(); }
            catch (Exception exception) { completion.SetException(exception); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(2)));
    }

    private static void RenderAffectedViews([CallerFilePath] string source = "")
    {
        AppUiStartup.Apply();
        var output = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(source))!, ".artifacts", "startup-integration", "ui",
            ApplicationIdentity.GetProductLine(typeof(MainForm).Assembly));
        Directory.CreateDirectory(output);
        var originalFont = WindowsFormsSettings.DefaultFont;
        var originalAppearance = AppearanceObject.DefaultFont;
        try
        {
            foreach (var populated in new[] { false, true })
            foreach (var fontSize in new[] { 10F, 16F })
            foreach (var small in new[] { false, true })
            {
                using var font = new Font("Segoe UI", fontSize);
                WindowsFormsSettings.DefaultFont = font;
                AppearanceObject.DefaultFont = font;
                foreach (var surface in new[] { "MainForm", "FrmLicenseKeyInput" })
                {
                    var requested = Environment.GetEnvironmentVariable("LX_UI_AUDIT_SURFACES");
                    if (!string.IsNullOrWhiteSpace(requested) && !requested.Split(',', StringSplitOptions.TrimEntries).Contains(surface)) continue;
                    var root = Path.Combine(Path.GetTempPath(), "WawiCallPro-ui-startup-" + Guid.NewGuid().ToString("N"));
                    try
                    {
                        using var form = CreateView(surface, populated, root);
                        form.Font = font;
                        _ = form.Handle;
                        // Bind the real Load handlers, without Show/ShowDialog or a foreground window.
                        typeof(Form).GetMethod("OnLoad", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, [EventArgs.Empty]);
                        form.ClientSize = surface == "MainForm" ? small ? new Size(440, 260) : new Size(720, 420)
                            : small ? new Size(500, 360) : new Size(760, 420);
                        form.PerformLayout();
                        CreateHandles(form);
                        var name = $"{surface}-{(populated ? "populated" : "empty")}-{(small ? "small" : "normal")}-font{fontSize:0}";
                        Save(form, Path.Combine(output, name + ".png"));
                        CheckBounds(form);
                        form.Scale(new SizeF(1.5F, 1.5F));
                        form.PerformLayout();
                        Save(form, Path.Combine(output, name + "-scale150.png"));
                        CheckBounds(form);
                        Assert.IsFalse(form.Visible);
                    }
                    finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
                }
            }
        }
        finally { WindowsFormsSettings.DefaultFont = originalFont; AppearanceObject.DefaultFont = originalAppearance; }
        File.WriteAllText(Path.Combine(output, "appearance.txt"), "Skin=" + UserLookAndFeel.Default.SkinName + "; no visible windows; scale150 is a layout simulation.");
    }

    private static Form CreateView(string surface, bool populated, string root)
    {
        if (surface == "FrmLicenseKeyInput")
            return new FrmLicenseKeyInput { Text = "WawiCallPro – Lizenzaktivierung", MachineKey = populated ? new string('A', 64) : null,
                LicenseKey = populated ? "ABCDE-FGHIJ-KLMNO-PQRST" : "" };
        var protector = new WindowsUserSecretProtector();
        var store = new ConfigurationStore(new ConfigurationLocation(root), new ApplicationOptionsValidator(protector));
        if (populated)
            store.SaveAsync(new ApplicationOptions
            {
                SipAccount = new SipAccountOptions { Server = "test.invalid", Username = "synthetic", Password = protector.Protect("synthetic", SipAccountOptions.PasswordPurpose) }
            }).GetAwaiter().GetResult();
        var configuration = new ApplicationConfigurationService(store);
        configuration.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
        return new MainForm(ApplicationDescriptor.FromAssembly(typeof(MainForm).Assembly), configuration);
    }

    private static void Save(Form form, string path)
    {
        using var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        bitmap.Save(path, ImageFormat.Png);
    }

    private static void CheckBounds(Control parent)
    {
        foreach (Control control in parent.Controls)
        {
            if (control.Name == "linkMachineKey" && parent.FindForm() is FrmLicenseKeyInput dialog && string.IsNullOrWhiteSpace(dialog.MachineKey)) continue;
            if (control.Parent is TableLayoutPanel && control is LabelControl or SimpleButton or TextEdit)
            {
                Assert.IsTrue(control.Left >= 0 && control.Top >= 0 && control.Right <= parent.ClientSize.Width + 1
                    && control.Bottom <= parent.ClientSize.Height + 1, $"Clipped control: {control.Name}");
            }
            CheckBounds(control);
        }
    }

    private static void CreateHandles(Control control)
    {
        _ = control.Handle;
        foreach (Control child in control.Controls) CreateHandles(child);
    }
}

using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.CompilerServices;
using DevExpress.LookAndFeel;
using DevExpress.Utils;
using DevExpress.XtraEditors;
using Quantarix.DevExpressExtensions.Controls;
using Quantarix.Windows.Win32;
using WawiCallPro.App.Hosting;
using WawiCallPro.App.Views;

namespace WawiCallPro.App.Tests;

[TestClass, DoNotParallelize]
public sealed class StartupSplashTests
{
    [TestMethod]
    public Task SplashGlowFadesBeforeAllControlEdgesAtPartialAndFullProgress() => RunStaAsync(async () =>
    {
        AppUiStartup.Apply();
        var originalSkin = UserLookAndFeel.Default.ActiveSkinName;
        var originalPalette = UserLookAndFeel.Default.ActiveSvgPaletteName;
        try
        {
            foreach (var skin in new[] { "Basic", "Office 2019 Black" })
            {
                UserLookAndFeel.Default.SetSkinStyle(skin);
                using var form = new FrmStartupSplashScreen();
                form.PrepareForBackgroundRun(); form.Show(); Application.DoEvents();
                var progress = Field<PulsingProgressBarControl>(form, "progressBar");
                foreach (var position in new[] { 0, 40, 100 })
                {
                    form.SetStatus("Anwendungsdienste werden vorbereitet …", position, 100);
                    var deadline = DateTime.UtcNow.AddSeconds(2);
                    while (progress.DisplayedPosition != position && DateTime.UtcNow < deadline) await Task.Delay(30);
                    Assert.AreEqual(position, progress.DisplayedPosition);
                    using var bitmap = new Bitmap(progress.Width, progress.Height);
                    progress.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                    var surface = bitmap.GetPixel(0, 0);
                    void CheckEdge(int x, int y)
                    {
                        var pixel = bitmap.GetPixel(x, y);
                        Assert.IsTrue(Math.Abs(pixel.R - surface.R) <= 4 && Math.Abs(pixel.G - surface.G) <= 4
                            && Math.Abs(pixel.B - surface.B) <= 4,
                            $"Clipped glow at ({x},{y}), progress {position}, skin {skin}.");
                    }
                    for (var y = 0; y < bitmap.Height; y++) { CheckEdge(0, y); CheckEdge(bitmap.Width - 1, y); }
                    for (var x = 0; x < bitmap.Width; x++) { CheckEdge(x, 0); CheckEdge(x, bitmap.Height - 1); }
                    if (position > 0)
                    {
                        var blue = 0;
                        for (var x = 0; x < bitmap.Width; x++)
                        {
                            var pixel = bitmap.GetPixel(x, bitmap.Height / 2);
                            if (pixel.B > pixel.R + 60) blue++;
                        }
                        Assert.IsTrue(blue > 20, "The progress must remain visible while its glow fades at the edges.");
                    }
                    Save(form, $"SplashGlow-{skin}-{position}");
                    var output = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../.artifacts/lx-startup-log"));
                    bitmap.Save(Path.Combine(output, $"SplashGlow-detail-{skin}-{position}.png"), ImageFormat.Png);
                }
                Assert.AreNotEqual(form.Handle, User32.GetForegroundWindow());
            }
        }
        finally
        {
            if (string.IsNullOrWhiteSpace(originalPalette)) UserLookAndFeel.Default.SetSkinStyle(originalSkin);
            else UserLookAndFeel.Default.SetSkinStyle(originalSkin, originalPalette);
        }
    });

    [TestMethod]
    public Task SplashMatchesReferenceAtSmallBoundsLargeFontsAndBothSkinsWithoutVisibleWindows() => RunStaAsync(async () =>
    {
        AppUiStartup.Apply();
        var originalFont = WindowsFormsSettings.DefaultFont;
        var originalAppearance = AppearanceObject.DefaultFont;
        var originalSkin = UserLookAndFeel.Default.ActiveSkinName;
        var originalPalette = UserLookAndFeel.Default.ActiveSvgPaletteName;
        try
        {
            foreach (var skin in new[] { originalSkin, "Office 2019 Black" }.Distinct())
            foreach (var size in new[] { 10F, 16F })
            foreach (var small in new[] { true, false })
            foreach (var filled in new[] { false, true })
            {
                UserLookAndFeel.Default.SetSkinStyle(skin);
                using var font = new Font("Segoe UI", size);
                WindowsFormsSettings.DefaultFont = AppearanceObject.DefaultFont = font;
                using var form = new FrmStartupSplashScreen();
                Assert.AreEqual(AutoScaleMode.Dpi, form.AutoScaleMode);
                Assert.IsTrue(form.Width <= 480 * form.DeviceDpi / 96 + 2);
                Assert.IsNotNull(form.IconOptions.SvgImage);
                Assert.IsNotNull(Field<SvgImageBox>(form, "svgLogo").SvgImage);
                form.SetStatus(filled ? "Warenwirtschaftsplugin und Kommunikationsdienste werden vorbereitet. Bitte einen Moment warten …" : "", 2, 5);
                Assert.AreEqual(40, Field<PulsingProgressBarControl>(form, "progressBar").Position);
                CreateHandles(form);
                form.ClientSize = small ? new Size(480, 297) : new Size(720, 445);
                form.PrepareForBackgroundRun();
                form.Show();
                Application.DoEvents();
                form.PerformLayout();
                CheckBounds(form);
                var progress = Field<PulsingProgressBarControl>(form, "progressBar");
                var deadline = DateTime.UtcNow.AddSeconds(2);
                while (progress.DisplayedPosition != 40 && DateTime.UtcNow < deadline) await Task.Delay(30);
                Assert.IsTrue(progress.IsPulsing);
                Assert.AreEqual(40, progress.DisplayedPosition);
                Assert.IsTrue(Field<LabelControl>(form, "lblSubtitle").Bottom <= progress.Top,
                    $"Progress overlaps subtitle: {progress.Bounds}");
                Assert.IsTrue(progress.Bottom <= Field<LabelControl>(form, "lblStatus").Top,
                    $"Progress overlaps status: {progress.Bounds}");
                var name = $"Splash-{skin}-{size:0}-{small}-{filled}";
                Save(form, name);
                if (small && filled)
                {
                    form.Scale(new SizeF(1.5F, 1.5F));
                    form.PerformLayout(); CheckBounds(form);
                    Save(form, name + "-scale150");
                }
                form.SetStatus("Bereit", 10, 5);
                Assert.AreEqual(100, Field<PulsingProgressBarControl>(form, "progressBar").Position);
                form.SetStatus("Vorbereiten", -5, 0);
                Assert.AreEqual(0, Field<PulsingProgressBarControl>(form, "progressBar").Position);
                Assert.AreEqual(0d, form.Opacity);
                Assert.IsFalse(form.ShowInTaskbar);
                Assert.AreNotEqual(form.Handle, User32.GetForegroundWindow());
            }
        }
        finally
        {
            WindowsFormsSettings.DefaultFont = originalFont; AppearanceObject.DefaultFont = originalAppearance;
            if (string.IsNullOrWhiteSpace(originalPalette)) UserLookAndFeel.Default.SetSkinStyle(originalSkin);
            else UserLookAndFeel.Default.SetSkinStyle(originalSkin, originalPalette);
        }
    });

    [TestMethod]
    public Task IndependentSplashThreadUpdatesAndStopsWithoutTakingForeground() => RunStaAsync(async () =>
    {
        AppUiStartup.Apply();
        await using var splash = new StartupSplash();
        await splash.ShowAsync(CancellationToken.None, hidden: true);
        Assert.AreNotEqual(nint.Zero, splash.WindowHandle);
        Assert.AreNotEqual(splash.WindowHandle, User32.GetForegroundWindow());
        await splash.UpdateAsync("Hintergrundprüfung", 3);
        using var main = new MainForm();
        main.PrepareForBackgroundRun();
        main.ShowAfterStartup(splash.ShouldPreserveForeground());
        Assert.AreNotEqual(main.Handle, User32.GetForegroundWindow());
        await splash.CompleteAsync(main);
        Assert.AreEqual(nint.Zero, splash.WindowHandle);
        await splash.CloseAsync();
        Assert.AreNotEqual(main.Handle, User32.GetForegroundWindow());
        main.Close();
    });

    [TestMethod]
    public async Task ForegroundIsCapturedBeforeActivationAndSplashAlwaysCloses()
    {
        var steps = new List<string>();
        await StartupFocusHandoff.CompleteAsync(() => { steps.Add("capture"); return true; },
            () => { steps.Add("close"); return Task.CompletedTask; }, () => steps.Add("activate"));
        CollectionAssert.AreEqual(new[] { "capture", "activate", "close" }, steps);
        steps.Clear();
        await StartupFocusHandoff.CompleteAsync(() => false,
            () => { steps.Add("close"); return Task.CompletedTask; }, () => steps.Add("activate"));
        CollectionAssert.AreEqual(new[] { "close" }, steps);
        foreach (var failCapture in new[] { false, true })
        {
            steps.Clear();
            await Assert.ThrowsAsync<InvalidOperationException>(() => StartupFocusHandoff.CompleteAsync(
                () => failCapture ? throw new InvalidOperationException() : true,
                () => { steps.Add("close"); return Task.CompletedTask; }, () => throw new InvalidOperationException()));
            CollectionAssert.AreEqual(new[] { "close" }, steps);
        }
    }

    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;

    private static void CreateHandles(Control control)
    {
        _ = control.Handle;
        foreach (Control child in control.Controls) CreateHandles(child);
    }

    private static void CheckBounds(Control parent)
    {
        foreach (Control control in parent.Controls)
        {
            if (control is LabelControl label)
            {
                Assert.IsTrue(label.Text.Length == 0 || label.Width > 0 && label.Height > 0,
                    $"Empty bounds for {label.Name}: {label.Bounds} in {parent.ClientSize}");
                Assert.IsTrue(control.Left >= 0 && control.Top >= 0 && control.Right <= parent.ClientSize.Width + 1 && control.Bottom <= parent.ClientSize.Height + 1,
                    $"Clipped {control.Name}: {control.Bounds} in {parent.ClientSize}");
                Assert.IsTrue(label.Height >= label.CalcBestSize().Height, $"Text clipped in {control.Name}");
            }
            CheckBounds(control);
        }
    }

    private static void Save(Form form, string name, [CallerFilePath] string source = "")
    {
        var output = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(source))!, ".artifacts", "lx-startup-log");
        Directory.CreateDirectory(output);
        using var bitmap = WawiCallPro.App.Diagnostics.UiAudit.Capture(form);
        bitmap.Save(Path.Combine(output, name + ".png"), ImageFormat.Png);
    }

    private static async Task RunStaAsync(Func<Task> action)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            using var context = new WindowsFormsSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(context);
            EventHandler? start = null;
            start = async (_, _) =>
            {
                Application.Idle -= start;
                try { await action(); done.SetResult(); }
                catch (Exception exception) { done.SetException(exception); }
                finally { Application.ExitThread(); }
            };
            Application.Idle += start;
            Application.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        await done.Task.WaitAsync(TimeSpan.FromSeconds(40));
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(2)));
    }
}

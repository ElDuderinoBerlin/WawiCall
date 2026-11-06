using System.Runtime.InteropServices;
using WawiCallPro.App;

namespace WawiCallPro.App.Tests;

[TestClass, DoNotParallelize]
public sealed class BackgroundWindowTests
{
    [TestMethod]
    public async Task BackgroundWindowUsesNativeNoActivateStyleAndDoesNotTakeForeground()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                using (var normal = new MainForm())
                    Assert.AreEqual(0, GetWindowLong(normal.Handle, -20) & 0x08000000);
                using (var form = new MainForm())
                {
                    form.PrepareForBackgroundRun();
                    form.Show();
                    Application.DoEvents();
                    Assert.AreEqual(0.0, form.Opacity);
                    Assert.IsFalse(form.ShowInTaskbar);
                    Assert.AreNotEqual(0, GetWindowLong(form.Handle, -20) & 0x08000000);
                    Assert.AreNotEqual(form.Handle, GetForegroundWindow());
                    form.Close();
                }
                completion.SetResult();
            }
            catch (Exception exception) { completion.SetException(exception); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(2)));
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr window, int index);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WawiCallPro.App;
using WawiCallPro.App.Hosting;
using WawiCallPro.Core.Threading;
using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Hosting;

namespace WawiCallPro.App.Tests;

[TestClass]
public sealed class ApplicationLifetimeTests
{
    [TestMethod]
    public async Task MessageLoopFailureClosesFormAndReturnsTheOriginalFailureAfterShutdown()
    {
        var directory = Path.Combine(Path.GetTempPath(), "WawiCallPro-ui-failure-tests-" + Guid.NewGuid().ToString("N"));
        var completion = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var expected = new InvalidOperationException("private-ui-failure-sentinel");
        var thread = new Thread(() =>
        {
            try
            {
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                using var synchronization = new WindowsFormsSynchronizationContext();
                SynchronizationContext.SetSynchronizationContext(synchronization);
                var host = WawiCallHost.CreateBuilder(typeof(MainForm).Assembly).ConfigureServices(services =>
                {
                    services.AddSingleton(new ConfigurationLocation(directory));
                    services.AddSingleton<IUiDispatcher>(new SynchronizationContextUiDispatcher(synchronization));
                    services.AddSingleton<Microsoft.Extensions.Hosting.IHostLifetime, AppHostLifetime>();
                }).Build();
                using var context = new AppApplicationContext(host, false, null);
                MainForm? observedForm = null;
                using var timer = new System.Windows.Forms.Timer { Interval = 20 };
                timer.Tick += (_, _) =>
                {
                    observedForm = Application.OpenForms.OfType<MainForm>().SingleOrDefault();
                    if (observedForm is null) return;
                    timer.Stop();
                    observedForm.Opacity = 0;
                    observedForm.BeginInvoke(() => throw expected);
                };
                timer.Start();
                Application.Run(context);
                Assert.IsNotNull(observedForm);
                Assert.IsTrue(observedForm.IsDisposed);
                completion.SetResult(context.Failure);
            }
            catch (Exception error) { completion.SetException(error); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        try
        {
            Assert.AreSame(expected, await completion.Task.WaitAsync(TimeSpan.FromSeconds(20)));
            Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(2)));
            var logs = string.Join("\n", Directory.GetFiles(Path.Combine(directory, "logs")).Select(File.ReadAllText));
            Assert.Contains("UiFailed", logs);
            Assert.Contains("HostStopped", logs);
            Assert.DoesNotContain(expected.Message, logs);
        }
        finally
        {
            if (!thread.IsAlive && Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}

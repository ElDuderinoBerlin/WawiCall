using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WawiCallPro.App;
using WawiCallPro.App.Hosting;
using WawiCallPro.Core.Threading;
using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Hosting;
using WawiCallPro.Utilities.Diagnostics;

namespace WawiCallPro.App.Tests;

[TestClass]
public sealed class ApplicationLifetimeTests
{
    [TestMethod]
    public async Task SecondLaunchRequestsSurviveStartupAndRestoreHiddenAndPreviouslyMaximizedWindows()
    {
        var root = Path.Combine(Path.GetTempPath(), "WawiCallPro-activation-tests-" + Guid.NewGuid().ToString("N"));
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                using var synchronization = new WindowsFormsSynchronizationContext(); SynchronizationContext.SetSynchronizationContext(synchronization);
                var instance = new ActivationInstanceGuard(); instance.Signal();
                var host = WawiCallHost.CreateBuilder(typeof(MainForm).Assembly).ConfigureServices(services =>
                {
                    services.AddSingleton(new ConfigurationLocation(root));
                    services.AddSingleton<IUiDispatcher>(new SynchronizationContextUiDispatcher(synchronization));
                    services.AddSingleton<IHostLifetime, AppHostLifetime>();
                    services.AddSingleton<CrashReportQueue>(); services.AddSingleton(new CrashApplicationInfo("test", "test"));
                    services.AddSingleton<ICrashReporter, CrashReporter>(); services.AddSingleton<AppExceptionBoundary>();
                    services.AddSingleton(new AppWindowOptions(Hidden: true)); services.AddSingleton<IAppInstanceGuard>(instance);
                }).Build();
                using var context = new AppApplicationContext(host, false, null);
                using var timer = new System.Windows.Forms.Timer { Interval = 30 };
                var stage = 0; var observed = false; Exception? failure = null;
                timer.Tick += (_, _) =>
                {
                    var form = Application.OpenForms.OfType<MainForm>().SingleOrDefault(); if (form is null) return;
                    try
                    {
                        if (stage == 0)
                        {
                            if (instance.Pending) return;
                            Assert.IsFalse(instance.Pending, "The request sent before startup must be consumed after the window is ready.");
                            form.WindowState = FormWindowState.Maximized; form.MoveToTray(); Assert.IsFalse(form.Visible);
                            instance.Signal(); stage = 1; return;
                        }
                        if (stage == 1)
                        {
                            if (!form.Visible) return;
                            Assert.AreEqual(FormWindowState.Maximized, form.WindowState);
                            form.WindowState = FormWindowState.Minimized; instance.Signal(); stage = 2; return;
                        }
                        if (form.WindowState == FormWindowState.Minimized) return;
                        Assert.AreEqual(FormWindowState.Maximized, form.WindowState);
                        observed = true; timer.Stop(); form.Close();
                    }
                    catch (Exception exception) { failure = exception; timer.Stop(); form.Close(); }
                };
                timer.Start(); Application.Run(context);
                if (failure is not null) throw failure;
                Assert.IsNull(context.Failure); Assert.IsTrue(observed); completion.SetResult();
            }
            catch (Exception exception) { completion.TrySetException(exception); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        try { await completion.Task.WaitAsync(TimeSpan.FromSeconds(20)); Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(2))); }
        finally { if (!thread.IsAlive && Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private sealed class ActivationInstanceGuard : IAppInstanceGuard
    {
        private int _pending;
        internal bool Pending => Volatile.Read(ref _pending) != 0;
        internal void Signal() { Interlocked.Exchange(ref _pending, 1); ActivationRequested?.Invoke(this, EventArgs.Empty); }
        public bool TryAcquire() => true;
        public void RequestExistingInstanceActivation() => Signal();
        public bool TryConsumeActivation() => Interlocked.Exchange(ref _pending, 0) != 0;
        public event EventHandler? ActivationRequested;
        public void Dispose() { }
    }

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
                    services.AddSingleton<CrashReportQueue>();
                    services.AddSingleton(new CrashApplicationInfo("test", "test"));
                    services.AddSingleton<ICrashReporter, CrashReporter>();
                    services.AddSingleton<AppExceptionBoundary>();
                    services.AddSingleton(new AppWindowOptions(Hidden: true));
                    services.AddSingleton<IAppInstanceGuard, NoInstanceGuard>();
                }).Build();
                using var exceptions = host.Services.GetRequiredService<AppExceptionBoundary>();
                exceptions.Install();
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
            var report = File.ReadAllText(Directory.GetFiles(Path.Combine(directory, "crash-reports")).Single());
            Assert.Contains("UiThread", report);
            Assert.DoesNotContain(expected.Message, report);
        }
        finally
        {
            if (!thread.IsAlive && Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    private sealed class NoInstanceGuard : IAppInstanceGuard
    {
        public bool TryAcquire() => true;
        public void RequestExistingInstanceActivation() { }
        public bool TryConsumeActivation() => false;
        public event EventHandler? ActivationRequested { add { } remove { } }
        public void Dispose() { }
    }
}

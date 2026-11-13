using DevExpress.XtraEditors;
using WawiCallPro.App.Hosting;
using WawiCallPro.Utilities.Configuration;
using System.Text;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Runtime.CompilerServices;
using Velopack;
using Microsoft.Extensions.DependencyInjection;
using WawiCallPro.Utilities.Diagnostics;

namespace WawiCallPro.App;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            // Installer hooks must finish before UI, licensing or host services are loaded.
            VelopackApp.Build().SetAutoApplyOnStartup(false).Run();
        }
        catch (Exception)
        {
            Console.Error.WriteLine("WawiCallPro: Installationsstart fehlgeschlagen.");
            return 1;
        }

        return RunAfterInstaller(args);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int RunAfterInstaller(string[] args)
    {
        if (args.Length > 0 && args[0] == "--activate-existing")
            return ActivateExisting(args);
        var result = RunApplication(args);
        if (result != AppRestartLauncher.RestartRequestedExitCode) return result;
        try { AppRestartLauncher.Start(); return 0; }
        catch (Exception)
        {
            XtraMessageBox.Show("Die Einstellungen sind gespeichert. Der automatische Neustart ist fehlgeschlagen. Bitte WawiCallPro erneut öffnen.",
                "Neustart", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }

    // Activation-only launches never load configuration or create a replacement main instance.
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static int ActivateExisting(string[] args)
    {
        try
        {
            if (args is not ["--activate-existing", var profile] || !Path.IsPathFullyQualified(profile)) return 2;
            var application = WawiCallPro.Utilities.Hosting.ApplicationDescriptor.FromAssembly(typeof(Program).Assembly);
            using var instance = new AppInstanceGuard(application,
                new WawiCallPro.Utilities.Operations.ApplicationPaths(new ConfigurationLocation(profile)));
            if (instance.TryAcquire()) return 2;
            instance.RequestExistingInstanceActivation();
            return 0;
        }
        catch (Exception)
        {
            Console.Error.WriteLine("WawiCallPro: Aktivierung der bestehenden Instanz fehlgeschlagen.");
            return 1;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int RunApplication(string[] args)
    {
        var diagnostics = args is ["--diagnostics"];
        ServiceProvider? bootstrap = null;
        AppExceptionBoundary? exceptions = null;
        try
        {
            bootstrap = AppBootstrap.Create();
            exceptions = bootstrap.GetRequiredService<AppExceptionBoundary>();
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            exceptions.Install();
            Console.OutputEncoding = new UTF8Encoding(false);
            ApplicationConfiguration.Initialize();
            using var uiContext = new WindowsFormsSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(uiContext);
            var auditDirectory = args is ["--ui-audit", var directory] ? directory : null;
            var mode = new StartupExecutionMode(diagnostics || auditDirectory is not null);
            var instance = bootstrap.GetRequiredService<IAppInstanceGuard>();
            if (!mode.SuppressNetwork && !instance.TryAcquire())
            {
                instance.RequestExistingInstanceActivation();
                return 0;
            }
            var host = AppCompositionRoot.Build(uiContext, exceptions, mode, instance);
            using var application = new AppApplicationContext(host, diagnostics, auditDirectory);
            Application.Run(application);
            if (application.Failure is not null) ExceptionDispatchInfo.Capture(application.Failure).Throw();
            if (exceptions.Failure is not null) ExceptionDispatchInfo.Capture(exceptions.Failure).Throw();
            return application.RestartRequested ? AppRestartLauncher.RestartRequestedExitCode : 0;
        }
        catch (Exception exception)
        {
            exceptions?.Report(exception, CrashOrigin.Startup);
            var message = StartupFailureMessage.Format(exception);
            Console.Error.WriteLine(message);
            if (!diagnostics && args is not ["--ui-audit", _])
                XtraMessageBox.Show(message, "WawiCallPro",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
        finally { bootstrap?.Dispose(); }
    }
}

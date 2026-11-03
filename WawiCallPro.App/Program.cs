using DevExpress.XtraEditors;
using WawiCallPro.App.Hosting;
using WawiCallPro.Utilities.Configuration;
using System.Text;
using System.Runtime.ExceptionServices;

namespace WawiCallPro.App;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var diagnostics = args is ["--diagnostics"];
        try
        {
            Console.OutputEncoding = new UTF8Encoding(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            ApplicationConfiguration.Initialize();
            using var uiContext = new WindowsFormsSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(uiContext);
            var auditDirectory = args is ["--ui-audit", var directory] ? directory : null;
            var host = AppCompositionRoot.Build(uiContext);
            using var application = new AppApplicationContext(host, diagnostics, auditDirectory);
            Application.Run(application);
            if (application.Failure is not null) ExceptionDispatchInfo.Capture(application.Failure).Throw();
            return 0;
        }
        catch (Exception exception)
        {
            var message = StartupFailureMessage.Format(exception);
            Console.Error.WriteLine(message);
            if (!diagnostics && args is not ["--ui-audit", _])
                XtraMessageBox.Show(message, "WawiCallPro",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }
}

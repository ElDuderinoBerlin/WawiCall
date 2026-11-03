using DevExpress.XtraEditors;
using WawiCallPro.App.Hosting;

namespace WawiCallPro.App;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var diagnostics = args is ["--diagnostics"];
        try
        {
            ApplicationConfiguration.Initialize();
            using var uiContext = new WindowsFormsSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(uiContext);
            var auditDirectory = args is ["--ui-audit", var directory] ? directory : null;
            var host = AppCompositionRoot.Build(uiContext);
            using var application = new AppApplicationContext(host, diagnostics, auditDirectory);
            Application.Run(application);
            if (application.Failure is not null) throw application.Failure;
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Hoststart oder Beenden fehlgeschlagen ({exception.GetType().Name}).");
            if (!diagnostics && args is not ["--ui-audit", _])
                XtraMessageBox.Show("Die Anwendung konnte nicht gestartet oder beendet werden.", "WawiCallPro",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }
}

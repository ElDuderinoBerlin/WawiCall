using System.Reflection;
using System.Text.Json;
using WawiCallPro.App.Diagnostics;
using ApplicationIdentity = WawiCallPro.Utilities.ApplicationIdentity;

namespace WawiCallPro.App;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args is ["--diagnostics"])
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                Application = ApplicationIdentity.Name,
                ProductLine = ApplicationIdentity.GetProductLine(Assembly.GetExecutingAssembly()),
                ConnectionConfigured = false
            }));
            return;
        }

        ApplicationConfiguration.Initialize();

        if (args is ["--ui-audit", var outputDirectory])
        {
            UiAudit.Render(outputDirectory);
            return;
        }

        Application.Run(new MainForm());
    }
}

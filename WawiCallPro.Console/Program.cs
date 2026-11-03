using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using WawiCallPro.ConsoleApp;
using WawiCallPro.Utilities.Hosting;
using WawiCallPro.Utilities.Configuration;
using System.Text;
using WawiCallPro.ConsoleApp.Diagnostics;
using WawiCallPro.ConsoleApp.Presentation;
using WawiCallPro.Utilities.Operations;

try
{
    Console.OutputEncoding = new UTF8Encoding(false);
    var command = args switch
    {
        [] => "--status",
        ["--status" or "--paths" or "--logs" or "--diagnostics" or "--help"] => args[0],
        _ => throw new DiagnosticException("Unbekannter Aufruf. Erlaubt: --status, --paths, --logs, --diagnostics, --help.")
    };
    var host = ConsoleCompositionRoot.Build();
    HostDiagnosticSnapshot? diagnostic = null;
    await HostExecution.RunAsync(host, async stoppingToken =>
    {
        diagnostic = await host.Services.GetRequiredService<HostDiagnostics>().CaptureAsync(stoppingToken);
        if (command != "--diagnostics")
        {
            var paths = host.Services.GetRequiredService<ApplicationPaths>();
            var table = command switch
            {
                "--paths" => DiagnosticTables.Paths(paths, host.Services.GetRequiredService<ConfigurationLocation>()),
                "--logs" => DiagnosticTables.Logs(DiagnosticLogReader.Read(paths)),
                "--help" => DiagnosticTables.Help(),
                _ => DiagnosticTables.Status(diagnostic)
            };
            await new TablePager(new SystemConsoleTerminal()).WriteAsync(table, stoppingToken);
        }
    });

    if (command == "--diagnostics") Console.WriteLine(JsonSerializer.Serialize(diagnostic));
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception is DiagnosticException diagnostic ? diagnostic.Message : StartupFailureMessage.Format(exception));
    return 1;
}

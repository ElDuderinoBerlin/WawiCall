using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using WawiCallPro.ConsoleApp;
using WawiCallPro.Utilities.Hosting;
using WawiCallPro.Utilities.Configuration;
using System.Text;
using WawiCallPro.ConsoleApp.Diagnostics;
using WawiCallPro.ConsoleApp.Presentation;
using WawiCallPro.Utilities.Operations;
using WawiCallPro.Utilities.Persistence;

try
{
    Console.OutputEncoding = new UTF8Encoding(false);
    var command = ConsoleCommand.Parse(args);
    var host = ConsoleCompositionRoot.Build();
    HostDiagnosticSnapshot? diagnostic = null;
    await HostExecution.RunAsync(host, async stoppingToken =>
    {
        diagnostic = await host.Services.GetRequiredService<HostDiagnostics>().CaptureAsync(stoppingToken);
        if (command.Name != "--diagnostics")
        {
            var paths = host.Services.GetRequiredService<ApplicationPaths>();
            var table = command.Name switch
            {
                "--paths" => DiagnosticTables.Paths(paths, host.Services.GetRequiredService<ConfigurationLocation>()),
                "--logs" => DiagnosticTables.Logs(DiagnosticLogReader.Read(paths)),
                "--help" => DiagnosticTables.Help(),
                _ => DiagnosticTables.Status(diagnostic)
            };
            if (command.UsesDatabase)
                table = await DatabaseCommands.ExecuteAsync(command, host.Services.GetRequiredService<ICommunicationDatabase>(), stoppingToken);
            else if (command.Name == "--workstation-init")
            {
                var id = await host.Services.GetRequiredService<IWorkstationIdentity>().InitializeAsync(stoppingToken);
                table = new("Arbeitsplatz eingerichtet", "Merkmal", "Wert", [new("Arbeitsplatz-GUID", id.ToString("D"))]);
            }
            await new TablePager(new SystemConsoleTerminal()).WriteAsync(table, stoppingToken);
        }
    });

    if (command.Name == "--diagnostics") Console.WriteLine(JsonSerializer.Serialize(diagnostic));
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception is DiagnosticException diagnostic ? diagnostic.Message : StartupFailureMessage.Format(exception));
    return 1;
}

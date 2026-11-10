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
    var host = ConsoleCompositionRoot.Build(loadPlugins: !command.UsesDatabase && command.Name is not ("--workstation-init" or "--erp-configure"));
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
            if (command.PhoneSearchNumber is { } searchNumber)
                table = await PhoneMatchingCommands.SearchAsync(searchNumber,
                    host.Services.GetRequiredService<WawiCallPro.Interfaces.PhoneNumbers.IPhoneSearchIndexService>(),
                    host.Services.GetRequiredService<WawiCallPro.Interfaces.PhoneNumbers.IPhoneNumberInterpreter>(), stoppingToken);
            else if (command.UsesPhoneMatching)
                table = await PhoneMatchingCommands.ExecuteAsync(command,
                    Enum.Parse<WawiCallPro.Interfaces.Companies.LexwareProductLine>(host.Services.GetRequiredService<ApplicationDescriptor>().ProductLine),
                    host.Services.GetRequiredService<WawiCallPro.Interfaces.PhoneNumbers.ICompanyPhoneMatchingService>(),
                    host.Services.GetRequiredService<WawiCallPro.Interfaces.PhoneNumbers.IPhoneNumberInterpreter>(), stoppingToken);
            else if (command.UsesDatabase)
                table = await DatabaseCommands.ExecuteAsync(command, host.Services.GetRequiredService<ICommunicationDatabase>(), stoppingToken);
            else if (command.Name == "--erp-configure")
                table = await ErpSetupCommands.ExecuteAsync(host.Services.GetRequiredService<ApplicationDescriptor>().ProductLine,
                    host.Services.GetRequiredService<ConfigurationStore>(),
                    host.Services.GetRequiredService<WawiCallPro.Utilities.Security.ISecretProtector>(), new ErpSetupInput(), stoppingToken);
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

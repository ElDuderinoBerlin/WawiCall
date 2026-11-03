using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using WawiCallPro.ConsoleApp;
using WawiCallPro.Utilities.Hosting;

try
{
    var host = ConsoleCompositionRoot.Build();
    HostDiagnosticSnapshot? diagnostic = null;
    await HostExecution.RunAsync(host, async stoppingToken =>
    {
        diagnostic = await host.Services.GetRequiredService<HostDiagnostics>().CaptureAsync(stoppingToken);
        if (args is not ["--diagnostics"])
        {
            var application = host.Services.GetRequiredService<ApplicationDescriptor>();
            Console.WriteLine($"{application.Name} – {application.ProductLine}");
            Console.WriteLine("Keine Verbindung konfiguriert.");
            Console.WriteLine("--diagnostics gibt den Einrichtungsstatus als JSON aus.");
        }
    });

    if (args is ["--diagnostics"]) Console.WriteLine(JsonSerializer.Serialize(diagnostic));
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Hoststart oder Beenden fehlgeschlagen ({exception.GetType().Name}).");
    return 1;
}

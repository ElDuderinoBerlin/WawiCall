using System.Reflection;
using System.Text.Json;
using WawiCallPro.Utilities;

var productLine = ApplicationIdentity.GetProductLine(Assembly.GetExecutingAssembly());
if (args is ["--diagnostics"])
{
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        Application = ApplicationIdentity.Name,
        ProductLine = productLine,
        ConnectionConfigured = false
    }));
    return;
}

Console.WriteLine($"{ApplicationIdentity.Name} – {productLine}");
Console.WriteLine("Keine Verbindung konfiguriert.");
Console.WriteLine("--diagnostics gibt den Einrichtungsstatus als JSON aus.");

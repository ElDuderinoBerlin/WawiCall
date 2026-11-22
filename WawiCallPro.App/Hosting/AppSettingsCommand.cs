using System.IO;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.App.Hosting;

/// <summary>File-only maintenance, before splash, COM registration or application services.</summary>
internal static class AppSettingsCommand
{
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static int? TryRun(string[] args)
    {
        if (args.Length == 0 || !args[0].Equals("--secure-settings", StringComparison.OrdinalIgnoreCase)) return null;
        if (args.Length > 2 || args.Length == 2 && !Path.IsPathFullyQualified(args[1])) return 2;
        try
        {
            var file = args.Length == 2 ? args[1] : Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            var changed = new ProductAppSettings(file).EncryptClientKey();
            Console.WriteLine(changed ? "Lokaler Client-Key verschlüsselt." : "Lokaler Client-Key ist bereits verschlüsselt.");
            return 0;
        }
        catch (Exception)
        {
            Console.Error.WriteLine("Client-Key konnte nicht verschlüsselt werden. appsettings.json und Zugriffsrechte prüfen.");
            return 1;
        }
    }
}

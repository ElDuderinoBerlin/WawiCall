using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using WawiCallPro.Automation.Contracts;

namespace WawiCallPro.App.Hosting;

/// <summary>Dependency-light entry point; registration never starts UI, configuration, license or application services.</summary>
internal static class AppComDeployment
{
    internal static int? TryRun(string[] args)
    {
        var fixture = args.Length == 3 && args[0].Equals("--com-fixture", StringComparison.OrdinalIgnoreCase) ? args[1] : null;
        var command = fixture is null ? args.FirstOrDefault() : args[2];
        if (command is null || !IsCommand(command)) return null;
        if (fixture is null && (args.Length is < 1 or > 2 || args.Length == 2 && !args[1].Equals("/user", StringComparison.OrdinalIgnoreCase))) return 2;
        try
        {
            var identity = Current(fixture);
            var registration = new UserComRegistration(new WindowsUserComRegistry());
            if (command.TrimStart('/', '-').Equals("regserver", StringComparison.OrdinalIgnoreCase)) registration.Register(identity);
            else registration.Unregister(identity);
            return 0;
        }
        catch (Exception) { Console.Error.WriteLine("WawiCallPro: COM-Benutzerregistrierung fehlgeschlagen."); return 1; }
    }
    private static bool IsCommand(string command) => command.TrimStart('/', '-').Equals("regserver", StringComparison.OrdinalIgnoreCase)
        || command.TrimStart('/', '-').Equals("unregserver", StringComparison.OrdinalIgnoreCase);

    internal static ComRegistrationIdentity Current(string? fixture = null, string? installationRoot = null)
    {
        if (fixture is null && Environment.GetEnvironmentVariable("WAWICALLPRO_CONFIG_DIR") is not null)
            throw new InvalidOperationException("An overridden profile cannot register the product identity.");
        var assembly = typeof(AppComDeployment).Assembly;
        var directory = Path.GetFullPath(AppContext.BaseDirectory);
        var executable = Path.Combine(directory, assembly.GetName().Name + ".exe");
        if (!File.Exists(executable)) throw new InvalidOperationException("Apphost is required for registration.");
        using (var file = File.Open(assembly.Location, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        using (var pe = new PEReader(file))
        {
            var metadata = pe.GetMetadataReader();
            if (metadata.GetGuid(metadata.GetModuleDefinition().Mvid) != assembly.ManifestModule.ModuleVersionId)
                throw new InvalidOperationException("The executing assembly has already been replaced.");
        }
        var version = FileVersionInfo.GetVersionInfo(assembly.Location).ProductVersion?.Split('+')[0]
            ?? throw new InvalidOperationException("Application version missing.");
#if LX_PRO
        const string line = "Pro";
        var clsid = new Guid(AutomationIds.ProClass); var progId = AutomationIds.ProProgId;
#else
        const string line = "FinOffice";
        var clsid = new Guid(AutomationIds.FinOfficeClass); var progId = AutomationIds.FinOfficeProgId;
#endif
        var arguments = "";
        if (fixture is not null)
        {
            if (!Path.IsPathFullyQualified(fixture) || fixture.Contains('"') || fixture.Any(char.IsControl)) throw new ArgumentException("Invalid fixture path.");
            fixture = Path.GetFullPath(fixture);
            using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(fixture, "com-fixture.json")));
            clsid = json.RootElement.GetProperty("ClassId").GetGuid(); progId = json.RootElement.GetProperty("ProgId").GetString()!;
            if (clsid == Guid.Empty || clsid == new Guid(AutomationIds.ProClass) || clsid == new Guid(AutomationIds.FinOfficeClass)
                || progId != "WCP.p" + clsid.ToString("N")) throw new ArgumentException("Invalid fixture identity.");
            arguments = " --com-fixture \"" + fixture + "\"";
        }
        var content = directory.TrimEnd(Path.DirectorySeparatorChar);
        var parent = Path.GetDirectoryName(content);
        if (installationRoot is null && Path.GetFileName(content).Equals("current", StringComparison.OrdinalIgnoreCase)
            && parent is not null && File.Exists(Path.Combine(parent, "Update.exe"))) installationRoot = parent;
        return new(clsid, progId, line, executable, version, Path.GetFullPath(installationRoot ?? directory).TrimEnd(Path.DirectorySeparatorChar), arguments)
        { AssemblyPath = assembly.Location, ModuleId = assembly.ManifestModule.ModuleVersionId };
    }
}

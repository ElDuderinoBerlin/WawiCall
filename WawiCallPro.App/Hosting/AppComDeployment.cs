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
#if DEBUG
        if (args.Length > 0 && args[0].Equals("--refresh-debug-com", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length != 1) return 2;
            try
            {
                var identity = Current();
                if (!identity.RequireSameInstallationRoot) return 2;
                new UserComRegistration(new WindowsUserComRegistry()).Register(identity with { RefreshExistingDebugRegistration = true });
                return 0;
            }
            catch (Exception) { Console.Error.WriteLine("WawiCallPro: COM-Debug-Ausgabe konnte nicht registriert werden."); return 1; }
        }
#endif
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
#if DEBUG
        var debugRoot = installationRoot is null ? DebugBuildRoot(content, line) : null;
        installationRoot ??= debugRoot;
#endif
        return new(clsid, progId, line, executable, version, Path.GetFullPath(installationRoot ?? directory).TrimEnd(Path.DirectorySeparatorChar), arguments)
        {
            AssemblyPath = assembly.Location,
            ModuleId = assembly.ManifestModule.ModuleVersionId,
#if DEBUG
            RequireSameInstallationRoot = debugRoot is not null
#endif
        };
    }

#if DEBUG
    private static string? DebugBuildRoot(string directory, string productLine)
    {
        var framework = new DirectoryInfo(directory);
        var generation = framework.Parent;
        var edition = generation?.Parent;
        var snapshots = edition?.Parent;
        var bin = snapshots?.Parent;
        if (generation is null || !Guid.TryParseExact(generation.Name, "N", out _)
            || !string.Equals(edition?.Name, productLine, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(snapshots?.Name, "ComDebug", StringComparison.OrdinalIgnoreCase)
            || bin is null || !bin.Name.Equals("bin", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(bin.Parent?.Name, "WawiCallPro.App", StringComparison.OrdinalIgnoreCase)) return null;
        // Existing Debug registrations and all completed generations share the same repository owner.
        return Path.Combine(bin.FullName, "Debug", framework.Name);
    }
#endif
}

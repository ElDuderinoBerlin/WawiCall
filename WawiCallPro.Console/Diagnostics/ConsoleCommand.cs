namespace WawiCallPro.ConsoleApp.Diagnostics;

internal sealed record ConsoleCommand(string Name, string? File = null)
{
    internal bool UsesDatabase => Name.StartsWith("--db-", StringComparison.Ordinal);
    internal static ConsoleCommand Parse(string[] arguments) => arguments switch
    {
        [] => new("--status"),
        ["--status" or "--paths" or "--logs" or "--diagnostics" or "--help" or "--workstation-init" or "--db-status" or "--db-init" or "--db-migrate" or "--db-approve-empty"] => new(arguments[0]),
        ["--db-backup" or "--db-restore", var file] when Path.IsPathFullyQualified(file) => new(arguments[0], file),
        _ => throw new DiagnosticException("Unbekannter oder unvollständiger Aufruf. --help zeigt die verfügbaren Befehle; Sicherung/Wiederherstellung benötigen einen absoluten Dateipfad.")
    };
}

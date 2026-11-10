namespace WawiCallPro.ConsoleApp.Diagnostics;

internal sealed record PhoneRuleRequest(string InstallationId, string CompanyId, bool? Enabled = null, int StartWidth = 5, long? ExpectedVersion = null);
internal sealed record PhoneLookupRequest(string InstallationId, string CompanyId, string Number);

internal sealed record ConsoleCommand(string Name, string? File = null)
{
    internal PhoneRuleRequest? PhoneRule { get; init; }
    internal PhoneLookupRequest? PhoneLookup { get; init; }
    internal bool UsesPhoneMatching => Name is "--phone-rule" or "--phone-match";
    internal bool UsesDatabase => Name.StartsWith("--db-", StringComparison.Ordinal) || UsesPhoneMatching;
    internal static ConsoleCommand Parse(string[] arguments) => arguments switch
    {
        [] => new("--status"),
        ["--status" or "--paths" or "--logs" or "--diagnostics" or "--help" or "--workstation-init" or "--erp-configure" or "--db-status" or "--db-init" or "--db-migrate" or "--db-approve-empty"] => new(arguments[0]),
        ["--db-backup" or "--db-restore", var file] when Path.IsPathFullyQualified(file) => new(arguments[0], file),
        ["--phone-rule", var installation, var company] => new(arguments[0]) { PhoneRule = new(installation, company) },
        ["--phone-rule", var installation, var company, "on" or "off", var width, var version]
            when int.TryParse(width, out var digits) && digits is >= 1 and <= 15
                && (version == "new" || long.TryParse(version, out var number) && number > 0) => new(arguments[0])
            { PhoneRule = new(installation, company, arguments[3] == "on", digits, version == "new" ? null : long.Parse(version, System.Globalization.CultureInfo.InvariantCulture)) },
        ["--phone-match", var installation, var company, var number] => new(arguments[0]) { PhoneLookup = new(installation, company, number) },
        _ => throw new DiagnosticException("Unbekannter oder unvollständiger Aufruf. --help zeigt die verfügbaren Befehle; Sicherung/Wiederherstellung benötigen einen absoluten Dateipfad.")
    };
}

using System.IO;

namespace WawiCallPro.App.Hosting;

internal enum AppStartKind { Interactive, Diagnostics, UiAudit, ActivateExisting, Com, ImportLicense, ResetLicense, Documentation }

/// <summary>Validates the complete command line before loading configuration or creating controls.</summary>
internal sealed record AppStartArguments(AppStartKind Kind, string? Path = null)
{
    internal string? FixtureDirectory { get; init; }
    internal AppDocumentationView? DocumentationView { get; init; }
    internal static AppStartArguments Parse(string[] arguments)
    {
        if (arguments.Length > 0 && (EqualsOption(arguments[0], "--automation-docs") || EqualsOption(arguments[0], "/automation-docs")))
            return ParseDocumentation(arguments);
        if (arguments.Length is 2 or 3 && EqualsOption(arguments[0], "--com-fixture")
            && System.IO.Path.IsPathFullyQualified(arguments[1]))
        {
            var kind = arguments.Length == 2 ? AppStartKind.Interactive
                : EqualsOption(arguments[2], "-Embedding") ? AppStartKind.Com
                : throw new ArgumentException("Invalid COM fixture mode.");
            return new(kind) { FixtureDirectory = arguments[1] };
        }
        if (arguments.Length == 0) return new(AppStartKind.Interactive);
        if (arguments.Length == 1)
        {
            if (EqualsOption(arguments[0], "-Embedding")) return new(AppStartKind.Com);
            if (EqualsOption(arguments[0], "--diagnostics")) return new(AppStartKind.Diagnostics);
            if (EqualsOption(arguments[0], "--reset-license")) return new(AppStartKind.ResetLicense);
        }
        if (arguments.Length == 2)
        {
            if (EqualsOption(arguments[0], "--ui-audit") && !string.IsNullOrWhiteSpace(arguments[1]))
                return new(AppStartKind.UiAudit, arguments[1]);
            if (EqualsOption(arguments[0], "--activate-existing") && System.IO.Path.IsPathFullyQualified(arguments[1]))
                return new(AppStartKind.ActivateExisting, arguments[1]);
            if (EqualsOption(arguments[0], "--import-license") && System.IO.Path.IsPathFullyQualified(arguments[1]))
                return new(AppStartKind.ImportLicense, arguments[1]);
        }
        throw new ArgumentException("Ungültige oder widersprüchliche Startargumente.");
    }

    private static AppStartArguments ParseDocumentation(string[] arguments)
    {
        AppDocumentationView? view = null;
        string? output = null;
        for (var index = 1; index < arguments.Length; index += 2)
        {
            if (index + 1 >= arguments.Length || string.IsNullOrWhiteSpace(arguments[index + 1])
                || arguments[index + 1].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException("Ein Wert für die Dokumentationsautomation fehlt.");
            var value = arguments[index + 1];
            if (view is null && EqualsOption(arguments[index], "--automation-open")) view = AppDocumentationView.Parse(value);
            else if (output is null && EqualsOption(arguments[index], "--automation-output")) output = value;
            else throw new ArgumentException("Ungültige oder doppelte Dokumentationsoption.");
        }
        return new(AppStartKind.Documentation, output) { DocumentationView = view ?? AppDocumentationView.Parse("home") };
    }

    internal string[] ToInteractiveArguments() => Kind switch
    {
        AppStartKind.Interactive => [],
        AppStartKind.Diagnostics => ["--diagnostics"],
        AppStartKind.UiAudit => ["--ui-audit", Path!],
        AppStartKind.ActivateExisting => ["--activate-existing", Path!],
        _ => throw new InvalidOperationException("COM requires its own host startup path.")
    };

    private static bool EqualsOption(string value, string expected) => string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
}

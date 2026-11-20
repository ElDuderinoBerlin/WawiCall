using System.IO;

namespace WawiCallPro.App.Hosting;

internal enum AppStartKind { Interactive, Diagnostics, UiAudit, ActivateExisting, Com }

/// <summary>Validates the complete command line before loading configuration or creating controls.</summary>
internal sealed record AppStartArguments(AppStartKind Kind, string? Path = null)
{
    internal static AppStartArguments Parse(string[] arguments)
    {
        if (arguments.Length == 0) return new(AppStartKind.Interactive);
        if (arguments.Length == 1)
        {
            if (EqualsOption(arguments[0], "-Embedding")) return new(AppStartKind.Com);
            if (EqualsOption(arguments[0], "--diagnostics")) return new(AppStartKind.Diagnostics);
        }
        if (arguments.Length == 2)
        {
            if (EqualsOption(arguments[0], "--ui-audit") && !string.IsNullOrWhiteSpace(arguments[1]))
                return new(AppStartKind.UiAudit, arguments[1]);
            if (EqualsOption(arguments[0], "--activate-existing") && System.IO.Path.IsPathFullyQualified(arguments[1]))
                return new(AppStartKind.ActivateExisting, arguments[1]);
        }
        throw new ArgumentException("Ungültige oder widersprüchliche Startargumente.");
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

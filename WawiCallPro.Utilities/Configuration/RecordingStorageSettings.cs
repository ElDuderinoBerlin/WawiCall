using System.Globalization;
using System.Text.RegularExpressions;
using WawiCallPro.Interfaces.Telephony;

namespace WawiCallPro.Utilities.Configuration;

/// <summary>Local archive settings. A requested destination becomes active after verification.</summary>
public sealed record RecordingStorageSettings
{
    public string? Directory { get; init; }
    public string? PendingDirectory { get; init; }
    public string FileNamePattern { get; init; } = RecordingFileName.IdPattern;
}

public static class RecordingFileName
{
    public const string IdPattern = "{AufnahmeId}";
    public const string DescriptivePattern = "{Datum:yyyy-MM-dd}_{Uhrzeit:HH-mm-ss}_{Richtung}_{AufnahmeId}";
    private static readonly Regex Placeholder = new(@"\{([^{}]+)\}", RegexOptions.CultureInvariant);

    /// <summary>Builds a Windows-compatible stem with the full recording ID for collision protection.</summary>
    public static string Format(string pattern, DateTimeOffset time, CallDirection? direction, Guid recordingId)
        => Format(pattern, time, direction, recordingId, null);

    public static string Format(string pattern, DateTimeOffset time, CallDirection? direction, Guid recordingId, CallAddress? remoteAddress)
    {
        if (string.IsNullOrWhiteSpace(pattern) || pattern.Length > 200)
            throw new FormatException("Das Dateinamensmuster muss 1 bis 200 Zeichen enthalten.");
        var identifier = recordingId.ToString("N");
        var stem = Placeholder.Replace(pattern, match => match.Groups[1].Value switch
        {
            "AufnahmeId" => identifier,
            "Datum" or "Datum:yyyy-MM-dd" => time.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            "Datum:yyyyMMdd" => time.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
            "Uhrzeit" or "Uhrzeit:HH-mm-ss" => time.ToString("HH-mm-ss", CultureInfo.InvariantCulture),
            "Uhrzeit:HHmmss" => time.ToString("HHmmss", CultureInfo.InvariantCulture),
            "Richtung" => direction switch { CallDirection.Incoming => "eingehend", CallDirection.Outgoing => "ausgehend", _ => "unbekannt" },
            "Rufnummer" => FormatNumber(remoteAddress),
            _ => throw new FormatException("Das Dateinamensmuster enthält einen unbekannten Platzhalter.")
        });
        if (stem.IndexOfAny(['<', '>', ':', '"', '/', '\\', '|', '?', '*', '{', '}']) >= 0 || stem.Any(char.IsControl)
            || stem[0] is '.' or ' ' || stem[^1] is '.' or ' ')
            throw new FormatException("Dateinamen dürfen keine Pfadtrenner, Sonderzeichen oder Punkte und Leerzeichen am Rand enthalten.");
        var firstSegment = stem.Split('.', 2)[0].TrimEnd().ToUpperInvariant();
        if (firstSegment is "CON" or "PRN" or "AUX" or "NUL" or "CONIN$" or "CONOUT$"
            || firstSegment.Length == 4 && (firstSegment.StartsWith("COM", StringComparison.Ordinal) || firstSegment.StartsWith("LPT", StringComparison.Ordinal))
                && "123456789¹²³".Contains(firstSegment[3]))
            throw new FormatException("Das Dateinamensmuster ergibt einen unter Windows reservierten Namen.");
        if (!pattern.Contains(IdPattern, StringComparison.Ordinal)) stem += "_" + identifier;
        if (stem.Length > 240) throw new FormatException("Der erzeugte Dateiname ist zu lang.");
        return stem;
    }

    private static string FormatNumber(CallAddress? address)
    {
        if (address?.Presentation == NumberPresentation.Withheld) return "unterdrueckt";
        if (address?.Presentation != NumberPresentation.Available) return "unbekannt";
        var number = address.E164 ?? address.OriginalValue;
        if (string.IsNullOrWhiteSpace(number) || number.Length > 40
            || number.Any(character => !(char.IsAsciiDigit(character) || character is '+' or ' ' or '(' or ')' or '-')))
            return "unbekannt";
        var digits = string.Concat(number.Where(char.IsAsciiDigit));
        return digits.Length == 0 ? "unbekannt" : digits;
    }
}

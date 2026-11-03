using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using WawiCallPro.Utilities.Operations;

namespace WawiCallPro.ConsoleApp.Diagnostics;

internal sealed class DiagnosticException(string message) : Exception(message);

internal sealed record LogSnapshot(IReadOnlyList<LogEntry> Entries, int IncompleteTails);

internal sealed record LogEntry(DateTimeOffset TimestampUtc, Guid RunId, int ProcessId, LogLevel Level,
    OperationEvent Event, int EventId, string? ExceptionType);

/// <summary>Reads a byte snapshot of completed records, sharing the file with live writers.</summary>
internal static class DiagnosticLogReader
{
    internal static LogSnapshot Read(ApplicationPaths paths)
    {
        try
        {
            if (!Directory.Exists(paths.LogsDirectory)) return new LogSnapshot([], 0);
            var entries = new List<LogEntry>();
            var incomplete = 0;
            foreach (var file in Directory.EnumerateFiles(paths.LogsDirectory, "operations-*.jsonl").Order(StringComparer.Ordinal))
            {
                using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var length = stream.Length;
                if (length > 4194304) throw InvalidLog();
                var bytes = new byte[(int)length];
                stream.ReadExactly(bytes);
                var completedLength = Array.LastIndexOf(bytes, (byte)'\n') + 1;
                if (completedLength != bytes.Length) incomplete++;
                using var reader = new StringReader(new UTF8Encoding(false, true).GetString(bytes, 0, completedLength));
                while (reader.ReadLine() is { } line) entries.Add(Parse(line));
            }
            return new LogSnapshot(entries.OrderBy(entry => entry.TimestampUtc).ToArray(), incomplete);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException
            or InvalidOperationException or FormatException or DecoderFallbackException or ArgumentException or OverflowException)
        {
            throw InvalidLog();
        }
    }

    private static LogEntry Parse(string line)
    {
        using var json = JsonDocument.Parse(line, new JsonDocumentOptions { MaxDepth = 4 });
        var root = json.RootElement;
        var properties = root.EnumerateObject().ToArray();
        string[] expected = ["TimestampUtc", "RunId", "ProcessId", "Level", "Event", "EventId", "ExceptionType"];
        if (properties.Length != expected.Length || properties.Select(property => property.Name).Distinct().Count() != expected.Length
            || expected.Any(name => !root.TryGetProperty(name, out _))) throw InvalidLog();
        var timestamp = root.GetProperty("TimestampUtc").GetDateTimeOffset();
        var run = root.GetProperty("RunId").GetGuid();
        var process = root.GetProperty("ProcessId").GetInt32();
        var severity = root.GetProperty("Level").GetString();
        var code = root.GetProperty("Event").GetString();
        var type = root.GetProperty("ExceptionType").GetString();
        if (timestamp.Offset != TimeSpan.Zero || run == Guid.Empty || process <= 0
            || !Enum.TryParse<LogLevel>(severity, out var level) || !Enum.IsDefined(level) || level.ToString() != severity
            || !Enum.TryParse<OperationEvent>(code, out var operation) || !Enum.IsDefined(operation) || operation.ToString() != code
            || type is not null && (type.Length is 0 or > 128 || type.Any(character => !char.IsLetterOrDigit(character) && character is not '_' and not '`')))
            throw InvalidLog();
        return new LogEntry(timestamp, run, process, level, operation, root.GetProperty("EventId").GetInt32(), type);
    }

    private static DiagnosticException InvalidLog() => new("Betriebsprotokoll konnte nicht vollständig gelesen werden. Format und Dateizugriff prüfen.");
}

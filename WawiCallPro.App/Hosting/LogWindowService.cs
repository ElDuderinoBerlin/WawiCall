using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using WawiCallPro.Utilities.Operations;

namespace WawiCallPro.App.Hosting;

/// <summary>One transient display entry, independent of persisted structured logs.</summary>
public sealed record UiLogEntry(DateTimeOffset Timestamp, LogLevel Level, string Message);

/// <summary>UI log messages and a bounded startup queue; callers supply display-safe text.</summary>
public interface ILogWindowService
{
    void LogNormal(string message);
    void LogWarning(string message);
    void LogError(string message);
    IReadOnlyList<UiLogEntry> Drain();
}

/// <summary>Bounded startup-safe queue. It holds no controls and never opens a window from a worker thread.</summary>
internal sealed class LogWindowService : ILogWindowService
{
    private readonly ConcurrentQueue<UiLogEntry> _pending = new();
    public void LogNormal(string message) => Append(LogLevel.Information, message);
    public void LogWarning(string message) => Append(LogLevel.Warning, message);
    public void LogError(string message) => Append(LogLevel.Error, message);
    internal void Append(LogLevel level, string message)
    {
        _pending.Enqueue(new(DateTimeOffset.Now, level, message));
        while (_pending.Count > 2000) _pending.TryDequeue(out _);
    }
    public IReadOnlyList<UiLogEntry> Drain()
    {
        var entries = new List<UiLogEntry>();
        while (entries.Count < 256 && _pending.TryDequeue(out var entry)) entries.Add(entry);
        return entries;
    }
}

/// <summary>UI counterpart of the safe file provider; arbitrary messages, formatters and exception text stay excluded.</summary>
internal sealed class UiLogProvider(LogWindowService logs) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new Logger(logs);
    public void Dispose() { }
    private sealed class Logger(LogWindowService logs) : ILogger
    {
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            var operation = OperationEvents.TryGetEvent(state, out var value) ? value : OperationEvent.FrameworkMessage;
            if (SettingsMessages.Get(operation) is { } status)
            { logs.Append(logLevel, status); return; }
            var message = operation switch
            {
                OperationEvent.HostStarting => "Anwendungsdienste werden gestartet.",
                OperationEvent.HostStarted => "Anwendungsdienste sind bereit.",
                OperationEvent.HostStopping => "Anwendungsdienste werden beendet.",
                OperationEvent.HostStopped => "Anwendungsdienste wurden beendet.",
                OperationEvent.HostFailed => "Der Anwendungsstart ist fehlgeschlagen.",
                OperationEvent.BackgroundFailed => "Ein Hintergrundvorgang ist fehlgeschlagen.",
                OperationEvent.UiFailed => "Eine Oberflächenaktion ist fehlgeschlagen.",
                OperationEvent.CancellationRequested => "Abbruch angefordert.",
                OperationEvent.ShutdownDeadlineExceeded => "Das Beenden dauert länger als vorgesehen.",
                OperationEvent.PluginMessage => "Meldung aus der Plugin-Laufzeit.",
                OperationEvent.LexwareMissing => "Lexware Pro und Financial Office wurden nicht erkannt. Lexware-abhängige Funktionen sind deaktiviert.",
                OperationEvent.LexwareCheckFailed => "Die Lexware-Installationsprüfung ist fehlgeschlagen. Lexware-abhängige Funktionen bleiben gesperrt.",
                OperationEvent.GlobalHotkeyUnavailable => "Ein globales Tastenkürzel ist nicht verfügbar und bleibt deaktiviert. Es ist möglicherweise bereits belegt. Unter Einstellungen → Tastenkürzel eine andere Kombination wählen und neu starten.",
                OperationEvent.GlobalHotkeyReleaseFailed => "Ein globales Tastenkürzel konnte nicht abgemeldet werden. Es wird spätestens beim Zerstören des zugehörigen Fensters freigegeben.",
                OperationEvent.RecordingArtifactUnavailable => "Mindestens eine bestätigte Aufnahmedatei fehlt oder wurde verändert. Die Datei muss vor ihrer Verwendung erneut geprüft werden; die ursprüngliche Prüfsumme bleibt erhalten.",
                OperationEvent.RecordingArtifactAuditIncomplete => "Der Abgleich der Aufnahme-Dateien konnte nicht vollständig bestätigt werden. Betroffene Einträge und ausstehende Abschlussaufträge bleiben zur erneuten Prüfung erhalten.",
                OperationEvent.RecordingDirectoryReviewRequired => "Im Aufnahmeordner wurden Dateien ohne Zuordnung im lokalen Datenbankstand, temporäre Dateien oder übersprungene Verknüpfungen gefunden. Sie können zu laufenden Aufnahmen oder anderen Arbeitsplätzen gehören und müssen geprüft werden. Dateien wurden weder übernommen noch gelöscht.",
                OperationEvent.RecordingDirectoryInventoryIncomplete => "Die Bestandsaufnahme des Aufnahmeordners konnte nicht vollständig durchgeführt werden. Eine vollständige Zuordnung der Dateien ist damit nicht bestätigt. Die Bestandsaufnahme verändert keine Dateien oder Datenbankeinträge.",
                OperationEvent.RecordingConversionRecoveryIncomplete => "Ausstehende Konvertierungsabschlüsse konnten nicht vollständig bestätigt werden. Aufträge und Dateien bleiben für eine erneute Prüfung erhalten.",
                OperationEvent.RecordingConversionInterrupted => "Unterbrochene Aufnahmekonvertierungen wurden als fehlgeschlagen markiert. Die WAV-Aufnahmen und vorhandene temporäre Dateien bleiben erhalten.",
                OperationEvent.RecordingConversionQueueIncomplete => "Die Konvertierungswarteschlange konnte nicht alle Aufträge abschließen. Nicht gestartete Aufträge und geschützte Abschlüsse bleiben erhalten; fehlgeschlagene Aufträge werden nicht automatisch erneut eingereiht.",
                OperationEvent.LiveRecordingSignalMissing => LiveRecordingMessages.Failure(WawiCallPro.Interfaces.Audio.LiveRecordingFailure.SignalMissing),
                OperationEvent.LiveRecordingInputFailed => LiveRecordingMessages.Failure(WawiCallPro.Interfaces.Audio.LiveRecordingFailure.InputFailed),
                OperationEvent.LiveRecordingStorageFailed => LiveRecordingMessages.Failure(WawiCallPro.Interfaces.Audio.LiveRecordingFailure.StorageFailed),
                OperationEvent.LiveRecordingUnavailable => LiveRecordingMessages.Failure(WawiCallPro.Interfaces.Audio.LiveRecordingFailure.Unavailable),
                OperationEvent.LiveRecordingFailed => LiveRecordingMessages.Failure(null),
                _ => exception is null ? "Anwendungsmeldung." : "Ein Anwendungsvorgang ist fehlgeschlagen."
            };
            logs.Append(logLevel, message);
        }
    }
}

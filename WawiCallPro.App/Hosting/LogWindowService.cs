using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using WawiCallPro.Utilities.Operations;

namespace WawiCallPro.App.Hosting;

/// <summary>One transient display entry with an optional reference to its persisted diagnosis.</summary>
public sealed record UiLogEntry(DateTimeOffset Timestamp, LogLevel Level, string Message)
{
    public FileLogReference? FileRecord { get; init; }
}

/// <summary>UI log messages and a bounded startup queue; callers supply display-safe text.</summary>
public interface ILogWindowService
{
    void LogNormal(string message);
    void LogWarning(string message);
    void LogWarning(string message, Exception error) => LogWarning(message);
    void LogError(string message);
    IReadOnlyList<UiLogEntry> Drain();
}

/// <summary>Bounded startup-safe queue. It holds no controls and never opens a window from a worker thread.</summary>
internal sealed class LogWindowService : ILogWindowService
{
    private readonly ConcurrentQueue<UiLogEntry> _pending = new();
    private readonly SafeFileLoggerProvider? _file;

    public LogWindowService() { }
    public LogWindowService(SafeFileLoggerProvider file) => _file = file;
    public void LogNormal(string message) => Append(LogLevel.Information, message);
    public void LogWarning(string message) => LogWarningCore(message, null);
    public void LogWarning(string message, Exception error) => LogWarningCore(message, error);
    private void LogWarningCore(string message, Exception? error) =>
        Append(LogLevel.Warning, message, _file?.WriteDisplayWarning(LogLevel.Warning, message, error));
    public void LogError(string message) => Append(LogLevel.Error, message, _file?.WriteDisplayWarning(LogLevel.Error, message, null));

    internal void Append(LogLevel level, string message) => Append(level, message, null);
    internal void Append(LogLevel level, string message, FileLogReference? reference)
    {
        _pending.Enqueue(new(DateTimeOffset.Now, level, message) { FileRecord = reference });
        while (_pending.Count > 2000) _pending.TryDequeue(out _);
    }
    public IReadOnlyList<UiLogEntry> Drain()
    {
        var entries = new List<UiLogEntry>();
        while (entries.Count < 256 && _pending.TryDequeue(out var entry)) entries.Add(entry);
        return entries;
    }
}

/// <summary>Writes app logger events once and supplies their corresponding short display entries.</summary>
internal sealed class UiLogProvider(LogWindowService logs) : ILoggerProvider
{
    private readonly SafeFileLoggerProvider? _file;

    public UiLogProvider(LogWindowService logs, SafeFileLoggerProvider file) : this(logs) => _file = file;

    public ILogger CreateLogger(string categoryName) => new Logger(logs, _file, categoryName);
    public void Dispose() { }
    private sealed class Logger(LogWindowService logs, SafeFileLoggerProvider? file, string category) : ILogger
    {
        public bool IsEnabled(LogLevel logLevel) => logLevel is >= LogLevel.Information and < LogLevel.None
            || file?.Accepts(logLevel, category) == true;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            var reference = file?.WriteWithReference(logLevel, eventId, state, exception, category);
            if (logLevel < LogLevel.Information) return;
            var operation = OperationEvents.TryGetEvent(state, out var value) ? value : OperationEvent.FrameworkMessage;
            if (operation is OperationEvent.FrameworkMessage or OperationEvent.PluginMessage && logLevel < LogLevel.Warning) return;
            if ((SettingsMessages.Get(operation) ?? RecordingConversionMessages.Get(operation) ?? SipRegistrationMessages.Get(operation)) is { } status)
            { logs.Append(logLevel, status, reference); return; }
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
                OperationEvent.PluginMessage when logLevel >= LogLevel.Error => "Die Plugin-Laufzeit meldet einen Fehler.",
                OperationEvent.PluginMessage => "Die Plugin-Laufzeit meldet eine Warnung.",
                OperationEvent.LexwareMissing => "Lexware Pro und Financial Office wurden nicht erkannt. Lexware-abhängige Funktionen sind deaktiviert.",
                OperationEvent.LexwareCheckFailed => "Die Lexware-Installationsprüfung ist fehlgeschlagen. Lexware-abhängige Funktionen bleiben gesperrt.",
                OperationEvent.GlobalHotkeyUnavailable => "Ein globales Tastenkürzel ist nicht verfügbar und bleibt deaktiviert. Es ist möglicherweise bereits belegt. Unter Einstellungen → Tastenkürzel eine andere Kombination wählen und neu starten.",
                OperationEvent.GlobalHotkeyReleaseFailed => "Ein globales Tastenkürzel konnte nicht abgemeldet werden. Es wird spätestens beim Zerstören des zugehörigen Fensters freigegeben.",
                OperationEvent.RecordingArtifactUnavailable => "Mindestens eine bestätigte Aufnahmedatei fehlt oder wurde verändert. Die Datei muss vor ihrer Verwendung erneut geprüft werden; die ursprüngliche Prüfsumme bleibt erhalten.",
                OperationEvent.RecordingArtifactAuditIncomplete => "Der Abgleich der Aufnahme-Dateien konnte nicht vollständig bestätigt werden. Betroffene Einträge und ausstehende Abschlussaufträge bleiben zur erneuten Prüfung erhalten.",
                OperationEvent.RecordingArchiveSwitchPending => "Der Aufnahmeordner konnte noch nicht gewechselt werden. WawiCallPro verwendet weiterhin den bisherigen Ordner. Weitere WawiCallPro-Prozesse schließen und Zielordner sowie vorhandene Dateien prüfen.",
                OperationEvent.RecordingDirectoryInventoryIncomplete => "Die Bestandsaufnahme des Aufnahmeordners konnte nicht vollständig durchgeführt werden. Eine vollständige Zuordnung der Dateien ist damit nicht bestätigt. Die Bestandsaufnahme verändert keine Dateien oder Datenbankeinträge.",
                OperationEvent.RecordingConversionInterrupted => "Unterbrochene Aufnahmekonvertierungen wurden als fehlgeschlagen markiert. Die WAV-Aufnahmen und vorhandene temporäre Dateien bleiben erhalten.",
                OperationEvent.RecordingConversionQueueIncomplete => "Konvertierungswarteschlange: Aufträge stehen noch aus oder sind fehlgeschlagen.",
                OperationEvent.LiveRecordingSignalMissing => LiveRecordingMessages.Failure(WawiCallPro.Interfaces.Audio.LiveRecordingFailure.SignalMissing),
                OperationEvent.LiveRecordingInputFailed => LiveRecordingMessages.Failure(WawiCallPro.Interfaces.Audio.LiveRecordingFailure.InputFailed),
                OperationEvent.LiveRecordingStorageFailed => LiveRecordingMessages.Failure(WawiCallPro.Interfaces.Audio.LiveRecordingFailure.StorageFailed),
                OperationEvent.LiveRecordingUnavailable => LiveRecordingMessages.Failure(WawiCallPro.Interfaces.Audio.LiveRecordingFailure.Unavailable),
                OperationEvent.LiveRecordingFailed => LiveRecordingMessages.Failure(null),
                OperationEvent.ConversationMicrophoneUnavailable => "Das Mikrofon konnte für das Gespräch nicht gestartet werden. WawiCallPro versucht, es erneut zu verbinden.",
                OperationEvent.ConversationPlaybackUnavailable => "Die Gesprächsausgabe konnte nicht gestartet werden. WawiCallPro versucht, sie erneut zu verbinden.",
                _ => exception is null ? "Anwendungsmeldung." : "Ein Anwendungsvorgang ist fehlgeschlagen."
            };
            logs.Append(logLevel, message, reference);
        }
    }
}

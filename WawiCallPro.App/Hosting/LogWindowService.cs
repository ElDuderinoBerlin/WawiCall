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
                _ => exception is null ? "Anwendungsmeldung." : "Ein Anwendungsvorgang ist fehlgeschlagen."
            };
            logs.Append(logLevel, message);
        }
    }
}

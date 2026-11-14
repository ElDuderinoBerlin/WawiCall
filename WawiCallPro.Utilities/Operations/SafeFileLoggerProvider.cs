using System.Text.Json;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;

namespace WawiCallPro.Utilities.Operations;

/// <summary>
/// Structured JSONL logging. Foreign messages, formatter output, scopes and exception contents are intentionally
/// excluded. Each append closes its stream; no log handle survives container disposal or a failed service dispose.
/// </summary>
public sealed class SafeFileLoggerProvider : ILoggerProvider
{
    private readonly object _gate = new();
    private readonly RuntimeLoggingSettings _settings;
    private readonly Logger _serilog;
    private readonly RuntimeFailureState _failures;
    private readonly long _maximumBytes;
    private readonly Guid _runId = Guid.NewGuid();
    private static readonly MessageTemplate Template = new MessageTemplateParser().Parse("{Event}");

    public SafeFileLoggerProvider(ApplicationPaths paths, RuntimeFailureState failures, long maximumBytes = 4194304)
        : this(paths, failures, new RuntimeLoggingSettings(paths), maximumBytes) { }

    public SafeFileLoggerProvider(ApplicationPaths paths, RuntimeFailureState failures, RuntimeLoggingSettings settings, long maximumBytes = 4194304)
    {
        _settings = settings;
        _failures = failures;
        _maximumBytes = maximumBytes;
        _serilog = new LoggerConfiguration().MinimumLevel.ControlledBy(settings.LevelSwitch)
            .WriteTo.Sink(new SafeJsonSink(this)).CreateLogger();
    }

    public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName) => new SafeLogger(this, categoryName);

    public void Dispose() => _serilog.Dispose();

    private void Append(LogLevel level, EventId id, object? state, Exception? exception, string category)
    {
        var backgroundFailure = category == "Microsoft.Extensions.Hosting.Internal.Host" && exception is not null;
        var operation = state is OperationEvents.SafeOperationRecord safe ? safe.Event
            : backgroundFailure ? OperationEvent.BackgroundFailed
            : category == "Quantarix.Plugin" ? OperationEvent.PluginMessage : OperationEvent.FrameworkMessage;
        if (_settings.IsEnabled(level))
        {
            // Only allowlisted metadata enters Serilog. It never receives foreign state, scopes or exceptions.
            _serilog.Write(new LogEvent(DateTimeOffset.UtcNow, RuntimeLoggingSettings.ToSerilog(level), null, Template,
            [
                new LogEventProperty("Event", new ScalarValue(operation.ToString())),
                new LogEventProperty("EventId", new ScalarValue(id.Id)),
                new LogEventProperty("Level", new ScalarValue(level.ToString())),
                new LogEventProperty("ExceptionType", new ScalarValue(exception?.GetType().Name))
            ]));
        }
        // Failure propagation is independent of the selected display/file threshold, including None.
        if (backgroundFailure) _failures.Report(exception!);
    }

    private void Persist(LogEvent logEvent)
    {
        var entry = new
        {
            TimestampUtc = logEvent.Timestamp.ToUniversalTime(),
            RunId = _runId,
            ProcessId = Environment.ProcessId,
            Level = ((ScalarValue)logEvent.Properties["Level"]).Value,
            Event = ((ScalarValue)logEvent.Properties["Event"]).Value,
            EventId = ((ScalarValue)logEvent.Properties["EventId"]).Value,
            ExceptionType = ((ScalarValue)logEvent.Properties["ExceptionType"]).Value
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(entry);
        Exception? logFailure = null;
        lock (_gate)
        {
            try
            {
                var directory = _settings.Directory;
                Directory.CreateDirectory(directory);
                var filePath = Path.Combine(directory, $"operations-{Environment.ProcessId}-{_runId:N}.jsonl");
                using var stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read);
                if (stream.Length + bytes.Length + 1 > _maximumBytes) throw OperationException.LogUnavailable();
                stream.Write(bytes);
                stream.WriteByte((byte)'\n');
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or OperationException)
            {
                logFailure = OperationException.LogUnavailable();
            }
        }
        if (logFailure is not null) _failures.Report(logFailure);
    }

    private sealed class SafeJsonSink(SafeFileLoggerProvider provider) : ILogEventSink
    {
        public void Emit(LogEvent logEvent) => provider.Persist(logEvent);
    }

    private sealed class SafeLogger(SafeFileLoggerProvider provider, string category) : Microsoft.Extensions.Logging.ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => provider._settings.IsEnabled(logLevel)
            || category == "Microsoft.Extensions.Hosting.Internal.Host" && logLevel is >= LogLevel.Error and < LogLevel.None;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel)) provider.Append(logLevel, eventId, state, exception, category);
        }
    }
}

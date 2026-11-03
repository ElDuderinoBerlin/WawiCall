using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace WawiCallPro.Utilities.Operations;

/// <summary>
/// Structured JSONL logging. Foreign messages, formatter output, scopes and exception contents are intentionally
/// excluded. Each append closes its stream; no log handle survives container disposal or a failed service dispose.
/// </summary>
public sealed class SafeFileLoggerProvider : ILoggerProvider
{
    private readonly object _gate = new();
    private readonly ApplicationPaths _paths;
    private readonly RuntimeFailureState _failures;
    private readonly long _maximumBytes;
    private readonly Guid _runId = Guid.NewGuid();
    private readonly string _filePath;

    public SafeFileLoggerProvider(ApplicationPaths paths, RuntimeFailureState failures, long maximumBytes = 4194304)
    {
        _paths = paths;
        _failures = failures;
        _maximumBytes = maximumBytes;
        _filePath = Path.Combine(paths.LogsDirectory, $"operations-{Environment.ProcessId}-{_runId:N}.jsonl");
    }

    public ILogger CreateLogger(string categoryName) => new SafeLogger(this, categoryName);

    // There are no retained streams, queues or registrations to release.
    public void Dispose() { }

    private void Append(LogLevel level, EventId id, object? state, Exception? exception, string category)
    {
        var backgroundFailure = category == "Microsoft.Extensions.Hosting.Internal.Host" && exception is not null;
        var operation = state is OperationEvents.SafeOperationRecord safe ? safe.Event
            : backgroundFailure ? OperationEvent.BackgroundFailed
            : category == "Quantarix.Plugin" ? OperationEvent.PluginMessage : OperationEvent.FrameworkMessage;
        var entry = new
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            RunId = _runId,
            ProcessId = Environment.ProcessId,
            Level = level.ToString(),
            Event = operation.ToString(),
            EventId = id.Id,
            ExceptionType = exception?.GetType().Name
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(entry);
        Exception? logFailure = null;
        lock (_gate)
        {
            try
            {
                Directory.CreateDirectory(_paths.LogsDirectory);
                using var stream = new FileStream(_filePath, FileMode.Append, FileAccess.Write, FileShare.Read);
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
        else if (backgroundFailure) _failures.Report(exception!);
    }

    private sealed class SafeLogger(SafeFileLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel is >= LogLevel.Information and < LogLevel.None;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel)) provider.Append(logLevel, eventId, state, exception, category);
        }
    }
}

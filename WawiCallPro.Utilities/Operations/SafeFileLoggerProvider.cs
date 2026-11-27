using System.Text.Json;
using System.Runtime.CompilerServices;
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
    private readonly ConditionalWeakTable<LogEvent, WriteReceipt> _receipts = new();
    private long _sequence;
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

    public bool Accepts(LogLevel level, string category) => _settings.IsEnabledForCategory(level, category)
        || category == "Microsoft.Extensions.Hosting.Internal.Host" && level is >= LogLevel.Error and < LogLevel.None;

    private void Append(LogLevel level, EventId id, object? state, Exception? exception, string category) =>
        WriteWithReference(level, id, state, exception, category);

    /// <summary>Writes one logger event and provides an exact file reference for warning and error entries.</summary>
    public FileLogReference? WriteWithReference(LogLevel level, EventId id, object? state, Exception? exception, string category)
    {
        var backgroundFailure = category == "Microsoft.Extensions.Hosting.Internal.Host" && exception is not null;
        var operation = state is OperationEvents.SafeOperationRecord safe ? safe.Event
            : backgroundFailure ? OperationEvent.BackgroundFailed
            : category == "Quantarix.Plugin" ? OperationEvent.PluginMessage : OperationEvent.FrameworkMessage;
        var summary = SettingsMessages.Get(operation) ?? RecordingConversionMessages.Get(operation) ?? SipRegistrationMessages.Get(operation);
        var reference = WriteEntry(level, id, exception, category, operation, summary,
            (state as OperationEvents.SafeOperationRecord)?.Diagnostic);
        if (backgroundFailure) _failures.Report(exception!);
        return reference;
    }

    /// <summary>Persists a warning whose text is already safe for display, plus diagnostic exception metadata.</summary>
    public FileLogReference? WriteDisplayWarning(LogLevel level, string message, Exception? exception)
    {
        var operation = OperationEvent.UiFailed;
        return WriteEntry(level, new((int)operation), exception, "WawiCallPro.App.LogWindow",
            operation, message.Length <= 1024 ? message : message[..1024]);
    }

    private FileLogReference? WriteEntry(LogLevel level, EventId id, Exception? exception, string category,
        OperationEvent operation, string? statusMessage, string? diagnostic = null)
    {
        if (!_settings.IsEnabledForCategory(level, category)) return null;
        var details = level >= LogLevel.Warning ? SafeLogDiagnostics.Describe(operation, id, category, statusMessage, exception) : null;
        if (details is not null && diagnostic is not null) details += Environment.NewLine + diagnostic;
        var sequence = Interlocked.Increment(ref _sequence);
        var logEvent = new LogEvent(DateTimeOffset.Now, RuntimeLoggingSettings.ToSerilog(level), null,
            statusMessage is null ? Template : new MessageTemplateParser().Parse("{StatusMessage}"),
        [
            new LogEventProperty("Event", new ScalarValue(operation.ToString())),
            new LogEventProperty("EventId", new ScalarValue(id.Id)),
            new LogEventProperty("Level", new ScalarValue(level.ToString())),
            new LogEventProperty("ExceptionType", new ScalarValue(exception?.GetType().Name)),
            new LogEventProperty("StatusMessage", new ScalarValue(statusMessage)),
            new LogEventProperty("Sequence", new ScalarValue(sequence)),
            new LogEventProperty("Details", new ScalarValue(details))
        ]);
        if (details is null) { _serilog.Write(logEvent); return null; }
        var receipt = new WriteReceipt();
        _receipts.Add(logEvent, receipt);
        try { _serilog.Write(logEvent); return receipt.Reference; }
        finally { _receipts.Remove(logEvent); }
    }

    private sealed class WriteReceipt
    {
        internal FileLogReference? Reference { get; set; }
    }

    private void Persist(LogEvent logEvent)
    {
        var entry = new
        {
            TimestampUtc = logEvent.Timestamp.ToUniversalTime(),
            Sequence = (long)((ScalarValue)logEvent.Properties["Sequence"]).Value!,
            RunId = _runId,
            ProcessId = Environment.ProcessId,
            Level = ((ScalarValue)logEvent.Properties["Level"]).Value,
            Event = ((ScalarValue)logEvent.Properties["Event"]).Value,
            EventId = ((ScalarValue)logEvent.Properties["EventId"]).Value,
            ExceptionType = ((ScalarValue)logEvent.Properties["ExceptionType"]).Value,
            Details = (string?)((ScalarValue)logEvent.Properties["Details"]).Value
        };
        byte[] bytes;
        if (_settings.OutputTemplate is { } template)
        {
            using var writer = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);
            new Serilog.Formatting.Display.MessageTemplateTextFormatter(template).Format(logEvent, writer);
            if (entry.Details is { } details)
            {
                writer.WriteLine();
                writer.WriteLine(details);
            }
            bytes = System.Text.Encoding.UTF8.GetBytes(writer.ToString());
        }
        else bytes = JsonSerializer.SerializeToUtf8Bytes(entry);
        Exception? logFailure = null;
        lock (_gate)
        {
            try
            {
                var directory = _settings.Directory;
                Directory.CreateDirectory(directory);
                var configured = _settings.FileName;
                var prefix = configured is null ? null : Path.GetFileNameWithoutExtension(configured);
                var extension = configured is null ? null : Path.GetExtension(configured);
                var fileName = configured is null ? $"operations-{Environment.ProcessId}-{_runId:N}.jsonl"
                    : $"{prefix}{DateTime.Now:yyyyMMdd}-{Environment.ProcessId}-{_runId:N}{extension}";
                var filePath = Path.Combine(directory, fileName);
                if (configured is not null)
                {
                    var pattern = "^" + System.Text.RegularExpressions.Regex.Escape(prefix!) + @"\d{8}-\d+-[a-f0-9]{32}" + System.Text.RegularExpressions.Regex.Escape(extension!) + "$";
                    var own = new DirectoryInfo(directory).EnumerateFiles().Where(file => System.Text.RegularExpressions.Regex.IsMatch(file.Name, pattern)
                        && (file.Attributes & FileAttributes.ReparsePoint) == 0).OrderByDescending(file => file.LastWriteTimeUtc).ToArray();
                    var keep = _settings.RetainedFileCountLimit - 1;
                    foreach (var stale in own.Where(file => file.FullName != filePath).Skip(keep)) stale.Delete();
                }
                using (var stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read))
                    AppendRecord(stream, filePath, bytes, configured is null, logEvent, entry.Sequence);
                if (configured is not null)
                {
                    var structured = JsonSerializer.SerializeToUtf8Bytes(entry);
                    var metadataPath = Path.Combine(directory, $"operations-{Environment.ProcessId}-{_runId:N}.jsonl");
                    using var metadata = new FileStream(metadataPath, FileMode.Append, FileAccess.Write, FileShare.Read);
                    AppendRecord(metadata, metadataPath, structured, true, logEvent, entry.Sequence);
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or OperationException)
            {
                logFailure = OperationException.LogUnavailable();
            }
        }
        if (logFailure is not null) _failures.Report(logFailure);
    }

    private void AppendRecord(FileStream stream, string path, byte[] bytes, bool structured, LogEvent logEvent, long sequence)
    {
        var offset = stream.Length;
        if (offset + bytes.Length + 1 > _maximumBytes) throw OperationException.LogUnavailable();
        stream.Write(bytes);
        stream.WriteByte((byte)'\n');
        if (!structured || !_receipts.TryGetValue(logEvent, out var receipt)) return;
        stream.Flush();
        receipt.Reference = new FileLogReference(path, offset, bytes.Length, _runId, sequence);
    }

    private sealed class SafeJsonSink(SafeFileLoggerProvider provider) : ILogEventSink
    {
        public void Emit(LogEvent logEvent) => provider.Persist(logEvent);
    }

    private sealed class SafeLogger(SafeFileLoggerProvider provider, string category) : Microsoft.Extensions.Logging.ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => provider.Accepts(logLevel, category);
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel)) provider.Append(logLevel, eventId, state, exception, category);
        }
    }
}

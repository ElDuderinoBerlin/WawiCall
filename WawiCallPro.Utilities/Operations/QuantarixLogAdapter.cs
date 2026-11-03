using Microsoft.Extensions.Logging;

namespace WawiCallPro.Utilities.Operations;

/// <summary>Implements the real Quantarix plugin logger contract; free-form plugin text is not persisted.</summary>
public sealed class QuantarixLogAdapter(ILoggerFactory factory) : Quantarix.Plugin.Abstractions.ILogger
{
    private readonly ILogger _logger = factory.CreateLogger("Quantarix.Plugin");
    public void LogInfo(string message) => Write(LogLevel.Information, null);
    public void LogWarning(string message) => Write(LogLevel.Warning, null);
    public void LogError(string message, Exception? exception = null) => Write(LogLevel.Error, exception);

    private void Write(LogLevel level, Exception? exception) => _logger.Log(level, new EventId((int)OperationEvent.PluginMessage),
        new OperationEvents.SafeOperationRecord(OperationEvent.PluginMessage), exception, static (_, _) => "PluginMessage");
}

using Microsoft.Extensions.Logging;

namespace WawiCallPro.Utilities.Operations;

public enum OperationEvent
{
    HostStarting,
    HostStarted,
    HostStopping,
    HostStopped,
    HostFailed,
    CancellationRequested,
    ShutdownDeadlineExceeded,
    BackgroundFailed,
    UiFailed,
    FrameworkMessage,
    PluginMessage,
    LexwareMissing,
    LexwareCheckFailed,
    GlobalHotkeyUnavailable,
    GlobalHotkeyReleaseFailed
}

/// <summary>Closed event vocabulary. No user-provided message or state is included.</summary>
public static class OperationEvents
{
    public static void Write(ILogger logger, OperationEvent operation, Exception? exception = null)
    {
        if (!Enum.IsDefined(operation)) throw new ArgumentOutOfRangeException(nameof(operation));
        var level = operation is OperationEvent.LexwareMissing or OperationEvent.LexwareCheckFailed or OperationEvent.GlobalHotkeyUnavailable or OperationEvent.GlobalHotkeyReleaseFailed ? LogLevel.Warning
            : exception is null ? LogLevel.Information : LogLevel.Error;
        logger.Log(level, new EventId((int)operation), new SafeOperationRecord(operation), exception, static (record, _) => record.Event.ToString());
    }

    /// <summary>Reads only the closed event vocabulary without formatting arbitrary logger state.</summary>
    public static bool TryGetEvent(object? state, out OperationEvent operation)
    {
        if (state is SafeOperationRecord record && Enum.IsDefined(record.Event)) { operation = record.Event; return true; }
        operation = OperationEvent.FrameworkMessage;
        return false;
    }

    internal sealed record SafeOperationRecord(OperationEvent Event);
}

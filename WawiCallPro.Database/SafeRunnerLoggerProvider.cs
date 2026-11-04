using Microsoft.Extensions.Logging;

namespace WawiCallPro.Database;

/// <summary>Forwards severity only. SQL, scopes, exception details and migration text never cross this boundary.</summary>
internal sealed class SafeRunnerLoggerProvider(ILogger destination) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new SafeLogger(destination);
    public void Dispose() { } // The host owns the destination and its factory.

    private sealed class SafeLogger(ILogger destination) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None && destination.IsEnabled(logLevel);

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
                destination.Log(logLevel, new EventId(3200 + (int)logLevel), "Database migration runner reported {Level}", logLevel);
        }
    }
}

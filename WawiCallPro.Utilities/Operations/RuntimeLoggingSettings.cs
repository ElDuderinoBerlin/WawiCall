using Microsoft.Extensions.Logging;
using Serilog.Core;
using Serilog.Events;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.Utilities.Operations;

/// <summary>Safe bootstrap defaults, replaced by validated settings before business workers start.</summary>
public sealed class RuntimeLoggingSettings(ApplicationPaths paths)
{
    private string _directory = paths.LogsDirectory;
    private int _minimumLevel = (int)LogLevel.Information;
    internal LoggingLevelSwitch LevelSwitch { get; } = new(LogEventLevel.Information);
    public string Directory => Volatile.Read(ref _directory);
    public bool IsEnabled(LogLevel level) => level is >= LogLevel.Trace and < LogLevel.None && (int)level >= Volatile.Read(ref _minimumLevel);

    internal void Apply(UserSettings settings)
    {
        Volatile.Write(ref _directory, settings.LogDirectory is null ? paths.LogsDirectory : Path.GetFullPath(settings.LogDirectory));
        Volatile.Write(ref _minimumLevel, (int)settings.LogLevel);
        // None is filtered before events enter Serilog; its switch accepts the six actual Serilog levels.
        LevelSwitch.MinimumLevel = ToSerilog(settings.LogLevel == LogLevel.None ? LogLevel.Critical : settings.LogLevel);
    }

    internal static LogEventLevel ToSerilog(LogLevel level) => level switch
    {
        LogLevel.Trace => LogEventLevel.Verbose,
        LogLevel.Debug => LogEventLevel.Debug,
        LogLevel.Information => LogEventLevel.Information,
        LogLevel.Warning => LogEventLevel.Warning,
        LogLevel.Error => LogEventLevel.Error,
        _ => LogEventLevel.Fatal
    };
}

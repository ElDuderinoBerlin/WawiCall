using Microsoft.Extensions.Logging;
using Serilog.Core;
using Serilog.Events;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.Utilities.Operations;

/// <summary>Safe bootstrap defaults, replaced by validated settings before business workers start.</summary>
public sealed class RuntimeLoggingSettings(ApplicationPaths paths)
{
    private string _directory = paths.LogsDirectory;
    private int _enabled = 1;
    private int _minimumLevel = (int)LogLevel.Information;
    private Dictionary<string, LogLevel> _overrides = new(StringComparer.Ordinal);
    internal string? FileName { get; private set; }
    internal int RetainedFileCountLimit { get; private set; } = 14;
    internal string? OutputTemplate { get; private set; }
    internal LoggingLevelSwitch LevelSwitch { get; } = new(LogEventLevel.Information);
    public string Directory => Volatile.Read(ref _directory);
    public bool IsEnabled(LogLevel level) => Volatile.Read(ref _enabled) != 0 && level is >= LogLevel.Trace and < LogLevel.None && (int)level >= Volatile.Read(ref _minimumLevel);
    internal bool IsEnabledForCategory(LogLevel level, string category)
    {
        var minimum = _overrides.Where(pair => category == pair.Key || category.StartsWith(pair.Key + ".", StringComparison.Ordinal))
            .OrderByDescending(pair => pair.Key.Length).Select(pair => (int?)pair.Value).FirstOrDefault() ?? Volatile.Read(ref _minimumLevel);
        return Volatile.Read(ref _enabled) != 0 && level is >= LogLevel.Trace and < LogLevel.None && (int)level >= minimum;
    }
    internal void ApplyProduct(System.Text.Json.Nodes.JsonObject root, UserSettings user)
    {
        try
        {
            var serilog = root["Serilog"]; if (serilog is null) return;
            if (serilog["MinimumLevel"]?["Override"] is System.Text.Json.Nodes.JsonObject overrides)
                _overrides = overrides.ToDictionary(pair => pair.Key, pair => ReadLevel(pair.Value!.GetValue<string>()), StringComparer.Ordinal);
            var sink = (serilog["WriteTo"] as System.Text.Json.Nodes.JsonArray)?.SingleOrDefault(node => node?["Name"]?.GetValue<string>() == "File");
            if (sink?["Args"] is { } args)
            {
                var path = args["path"]?.GetValue<string>() ?? "logs/WawiCallPro-.log";
                var resolved = Path.GetFullPath(Path.IsPathFullyQualified(path) ? path : Path.Combine(paths.RootDirectory, path));
                FileName = Path.GetFileName(resolved);
                if (FileName.Length == 0 || FileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) throw new InvalidDataException();
                if (user.LogDirectory is null) Volatile.Write(ref _directory, Path.GetDirectoryName(resolved)!);
                RetainedFileCountLimit = args["retainedFileCountLimit"]?.GetValue<int>() ?? 14;
                if (RetainedFileCountLimit is < 1 or > 3650 || args["rollingInterval"]?.GetValue<string>() is { } interval && interval != "Day") throw new InvalidDataException();
                OutputTemplate = args["outputTemplate"]?.GetValue<string>() ?? "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}";
                if (OutputTemplate.Length > 4096) throw new InvalidDataException();
            }
            var minimum = _overrides.Values.Select(value => (int)value).Append((int)user.LogLevel).Min();
            LevelSwitch.MinimumLevel = ToSerilog(minimum == (int)LogLevel.None ? LogLevel.Critical : (LogLevel)minimum);
        }
        catch { throw ConfigurationException.FileError("Die Serilog-Einstellungen sind ungültig."); }
    }
    private static LogLevel ReadLevel(string value) => value switch
    {
        "Verbose" => LogLevel.Trace, "Fatal" => LogLevel.Critical,
        _ => Enum.TryParse<LogLevel>(value, out var level) && Enum.IsDefined(level) ? level : throw new InvalidDataException()
    };

    internal void Apply(UserSettings settings)
    {
        Volatile.Write(ref _enabled, settings.LoggingEnabled ? 1 : 0);
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

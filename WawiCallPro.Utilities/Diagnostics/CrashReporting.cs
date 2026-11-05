using System.Diagnostics;
using System.Text.Json;
using WawiCallPro.Utilities.Hosting;
using WawiCallPro.Utilities.Operations;

namespace WawiCallPro.Utilities.Diagnostics;

public enum CrashOrigin { Startup, UiThread, BackgroundTask, Process, Host }

public sealed record CrashReceipt(Guid ReportId, bool Stored);
public sealed record CrashApplicationInfo(string Version, string Build);

/// <summary>Durable, bounded diagnostic queue. Never stores exception messages, arguments or source paths.</summary>
public interface ICrashReporter
{
    CrashReceipt Capture(Exception exception, CrashOrigin origin);
}

public sealed record CrashReport(Guid ReportId, DateTimeOffset TimestampUtc, string Application,
    string SoftwareVersion, string BuildNumber, string OperatingSystem, string DotNetVersion,
    string FunctionName, string ExceptionType, string Message, string? StackTrace,
    string? InnerExceptionType, string? InnerExceptionMessage, string? InnerExceptionStackTrace, string MetadataJson);

public sealed class CrashReportQueue(ApplicationPaths paths)
{
    private readonly object _gate = new();
    public string DirectoryPath { get; } = Path.Combine(paths.RootDirectory, "crash-reports");
    public const int MaximumReports = 64;
    public const int MaximumReportBytes = 128 * 1024;
    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web);

    public void Store(CrashReport report)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(report, JsonOptions);
        if (bytes.Length > MaximumReportBytes) throw new InvalidDataException("Crash report exceeds size limit.");
        lock (_gate)
        {
            Directory.CreateDirectory(DirectoryPath);
            var file = Path.Combine(DirectoryPath, $"{report.TimestampUtc:yyyyMMddHHmmssfffffff}-{report.ReportId:N}.json");
            var temporary = file + ".partial";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes);
                    stream.Flush(true);
                }
                File.Move(temporary, file);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            var pending = GetPendingFiles();
            foreach (var stale in pending.Take(Math.Max(0, pending.Length - MaximumReports))) File.Delete(stale);
        }
    }

    public string[] GetPendingFiles() => !Directory.Exists(DirectoryPath) ? []
        : Directory.GetFiles(DirectoryPath, "*.json").Where(file => (File.GetAttributes(file) & FileAttributes.ReparsePoint) == 0)
            .Order(StringComparer.Ordinal).ToArray();
}

public sealed class CrashReporter(CrashReportQueue queue, ApplicationDescriptor application, CrashApplicationInfo version, TimeProvider clock) : ICrashReporter
{
    public CrashReceipt Capture(Exception exception, CrashOrigin origin)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var id = Guid.NewGuid();
        try
        {
            queue.Store(new CrashReport(id, clock.GetUtcNow(), application.Name, version.Version,
                version.Build, Environment.OSVersion.VersionString, Environment.Version.ToString(),
                origin.ToString(), exception.GetType().FullName ?? "Exception", "Exception details omitted; see method stack.",
                GetMethodStack(exception), exception.InnerException?.GetType().FullName, null,
                exception.InnerException is { } inner ? GetMethodStack(inner) : null,
                JsonSerializer.Serialize(new { productLine = application.ProductLine })));
            return new CrashReceipt(id, true);
        }
        catch { return new CrashReceipt(id, false); }
    }

    private static string GetMethodStack(Exception exception) => string.Join(Environment.NewLine,
        new StackTrace(exception, false).GetFrames().Take(64).Select(frame => frame.GetMethod())
            .Where(method => method is not null).Select(method => $"{method!.DeclaringType?.FullName}.{method.Name}"));
}

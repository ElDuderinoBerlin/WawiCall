using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using WawiCallPro.Database;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Interfaces.Office;

namespace WawiCallPro.Utilities.Operations;

internal static class SafeLogDiagnostics
{
    internal static string Describe(OperationEvent operation, EventId id, string category, string? summary, Exception? error)
    {
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(summary)) lines.Add(summary);
        lines.Add($"Ereignis: {operation} ({id.Id})");
        if (IsKnownCategory(category)) lines.Add($"Quelle: {category}");
        if (error is not null) lines.Add(DescribeException(error));
        return string.Join(Environment.NewLine, lines);
    }

    internal static string DescribeException(Exception? error)
    {
        var lines = new List<string>();

        for (var depth = 0; error is not null && depth < 6; depth++, error = error.InnerException)
        {
            var reason = error switch
            {
                RepositoryConflictException conflict => conflict.Kind.ToString(),
                DatabaseOperationException database => database.Code.ToString(),
                Mp3ConversionException audio => audio.Failure.ToString(),
                MailException mail => mail.Failure.ToString(),
                SocketException socket => socket.SocketErrorCode.ToString(),
                COMException com => $"COM 0x{com.ErrorCode:X8}",
                _ => null
            };
            var kind = error.GetType().FullName;
            lines.Add($"{(depth == 0 ? "Ausnahme" : "Innere Ausnahme")}: {kind} (HRESULT 0x{error.HResult:X8})"
                + (reason is null ? "" : $" · {reason}"));

            // Method names and offsets identify the source without copying paths, argument values or exception text.
            foreach (var frame in new StackTrace(error, fNeedFileInfo: false).GetFrames().Take(16))
            {
                var method = frame.GetMethod();
                if (method?.DeclaringType?.FullName is not { } type) continue;
                lines.Add($"  {type}.{method.Name} +0x{frame.GetILOffset():X}");
            }
        }
        return string.Join(Environment.NewLine, lines);
    }

    private static bool IsKnownCategory(string category)
    {
        if (category is "Quantarix.Plugin" or "SIPSorcery" or "Microsoft.Hosting.Lifetime" or "WawiCallPro.App.LogWindow") return true;
        if (category.Length > 180 || !category.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '.' or '_')) return false;
        return AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetType(category, throwOnError: false) is not null);
    }
}
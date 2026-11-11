using System.Diagnostics;
using System.IO;

namespace WawiCallPro.App.Hosting;

/// <summary>Called only after the host, forms and single-instance guard have been disposed.</summary>
internal static class AppRestartLauncher
{
    internal const int RestartRequestedExitCode = 42;
    internal static void Start()
    {
        var start = CreateStartInfo(Environment.ProcessPath ?? throw new InvalidOperationException("Application path unavailable."), typeof(Program).Assembly.Location);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Restart did not create a process.");
    }
    internal static ProcessStartInfo CreateStartInfo(string executable, string assembly)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, WorkingDirectory = AppContext.BaseDirectory };
        if (string.Equals(Path.GetFileNameWithoutExtension(executable), "dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(assembly);
        return start;
    }
}

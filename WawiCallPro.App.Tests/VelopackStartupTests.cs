using System.Diagnostics;

namespace WawiCallPro.App.Tests;

[TestClass]
public sealed class VelopackStartupTests
{
    [TestMethod]
    public async Task FastExitHook_DoesNotLoadApplicationDependenciesOrReadAppSettings()
    {
        var applicationAssembly = typeof(Program).Assembly;
        var assemblyName = applicationAssembly.GetName().Name!;
        var sourceDirectory = Path.GetDirectoryName(applicationAssembly.Location)!;
        var directory = Path.Combine(Path.GetTempPath(), "WawiCallPro-Velopack-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            // Deliberately omit DevExpress, DI, licensing and other application DLLs.
            // A fast hook must exit before the normal application's JIT/load path.
            foreach (var fileName in new[]
                     {
                         assemblyName + ".dll",
                         assemblyName + ".deps.json",
                         assemblyName + ".runtimeconfig.json",
                         "Velopack.dll"
                     })
            {
                File.Copy(Path.Combine(sourceDirectory, fileName), Path.Combine(directory, fileName));
            }

            await File.WriteAllTextAsync(Path.Combine(directory, "appsettings.json"), "invalid JSON");
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = directory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            startInfo.ArgumentList.Add(Path.Combine(directory, assemblyName + ".dll"));
            // This real Velopack hook has no registration/cleanup side effects.
            startInfo.ArgumentList.Add("--veloapp-obsolete");
            startInfo.ArgumentList.Add("1.0.0");

            using var process = Process.Start(startInfo)!;
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
                Assert.Fail("The fast hook did not exit within the Velopack update timeout (15 seconds).");
            }

            Assert.AreEqual(0, process.ExitCode, await error + await output);
            Assert.IsFalse(File.Exists(Path.Combine(directory, "velopack-bootstrap.log")));
            Assert.IsFalse(Directory.Exists(Path.Combine(directory, "logs")));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}

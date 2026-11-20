using System.Security.Principal;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Quantarix.Interop.COM;
using Quantarix.Interop.Dispatcher;
using Quantarix.Interop.Interfaces;

namespace WawiCallPro.ComPrototype;

internal sealed record PrototypeOptions(string Profile, Guid ClassId, string ProgId, int StartupDelayMs, bool Legacy)
{
    internal static PrototypeOptions Read(string profile)
    {
        if (!Path.IsPathFullyQualified(profile)) throw new ArgumentException("Absolute fixture profile required.");
        var options = JsonSerializer.Deserialize<PrototypeOptions>(File.ReadAllText(Path.Combine(profile, "fixture.json")))
            ?? throw new ArgumentException("Fixture profile missing.");
        if (!string.Equals(Path.GetFullPath(profile), options.Profile, StringComparison.OrdinalIgnoreCase)
            || options.ClassId == Guid.Empty || options.ProgId != "WCP.p" + options.ClassId.ToString("N")
            || options.StartupDelayMs is < 0 or > 10000)
            throw new ArgumentException("Invalid fixture identity.");
        return options;
    }
}

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args is ["--self-test", var resultFile]) return LifetimeChecks.Run(resultFile);
        PrototypeOptions? options = null;
        try
        {
            if (args.Length is < 2 or > 3 || args[0] != "--profile"
                || args.Length == 3 && args[2] is not ("-Embedding" or "--normal")) return 2;
            options = PrototypeOptions.Read(args[1]);
            using var user = WindowsIdentity.GetCurrent();
            using var mutex = new Mutex(false, $"Local\\WawiCallPro.ComProbe.{user.User?.Value}.{options.ClassId:N}");
            bool primary;
            try { primary = mutex.WaitOne(0); } catch (AbandonedMutexException) { primary = true; }
            if (!primary)
            {
                // Fixture-only handoff. This is not the product's IPC protocol.
                if (args.Length == 3 && args[2] == "--normal") File.WriteAllText(Path.Combine(options.Profile, "user-on.request"), "");
                if (!SpinWait.SpinUntil(() => File.Exists(Path.Combine(options.Profile, "factory.json")), TimeSpan.FromSeconds(15))) return 3;
                return 0;
            }
            try
            {
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
                Application.OleRequired();
                if (options.Legacy)
                {
                    var code = RunLegacy(options);
                    File.WriteAllText(Path.Combine(options.Profile, "exit.json"), JsonSerializer.Serialize(new { ExitCode = code }));
                    return code;
                }
                using var dispatcherControl = new Control();
                _ = dispatcherControl.Handle;
                var services = new ServiceCollection();
                services.AddSingleton<IUiDispatcher>(new WinFormsUiDispatcher(dispatcherControl));
                services.AddSingleton(options);
                services.AddSingleton(TimeProvider.System);
                services.AddSingleton(provider => new ServerLifetime(provider.GetRequiredService<TimeProvider>(), TimeSpan.FromSeconds(2)));
                services.AddSingleton<ProbeDependency>();
                services.AddTransient<PrototypeAutomation>(provider => new PrototypeAutomation(
                    provider.GetRequiredService<ProbeDependency>(), provider.GetRequiredService<ServerLifetime>(), provider.GetRequiredService<IUiDispatcher>()));
                using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
                // Only the composition root resolves services. The factory itself receives a typed delegate.
                var lifetime = provider.GetRequiredService<ServerLifetime>();
                if (args.Length == 3 && args[2] == "--normal") lifetime.SetUserOwned(true);
                using var context = new PrototypeContext(options, lifetime, provider.GetRequiredService<ProbeDependency>(),
                    () => provider.GetRequiredService<PrototypeAutomation>());
                Application.Run(context);
                context.Dispose();
                provider.Dispose();
                File.WriteAllText(Path.Combine(options.Profile, "exit.json"), JsonSerializer.Serialize(new { ExitCode = 0 }));
                return 0;
            }
            finally { mutex.ReleaseMutex(); }
        }
        catch (Exception error)
        {
            if (options is not null)
            {
                File.WriteAllText(Path.Combine(options.Profile, "failure.json"),
                    JsonSerializer.Serialize(new { Type = error.GetType().FullName, error.HResult, error.Message }));
                File.WriteAllText(Path.Combine(options.Profile, "exit.json"), JsonSerializer.Serialize(new { ExitCode = 1 }));
            }
            return 1;
        }
    }

    private static int RunLegacy(PrototypeOptions options)
    {
        using var context = new ApplicationContext();
        LocalServer.Instance.Register<LegacyAutomation>(options.ClassId);
        File.WriteAllText(Path.Combine(options.Profile, "factory.json"), JsonSerializer.Serialize(new { Pid = Environment.ProcessId, Legacy = true }));
        LocalServer.Instance.Run(context);
        File.WriteAllText(Path.Combine(options.Profile, "stopped.json"), JsonSerializer.Serialize(new { Pid = Environment.ProcessId, Legacy = true }));
        return 0;
    }
}

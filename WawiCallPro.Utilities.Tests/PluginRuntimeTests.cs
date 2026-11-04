using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Quantarix.Plugin.Hosting;
using WawiCallPro.Core.Threading;
using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Hosting;
using WawiCallPro.Utilities.Plugins;
using WawiCallPro.Interfaces.Companies;

namespace WawiCallPro.Utilities.Tests;

[TestClass]
public sealed class PluginRuntimeTests
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WawiCallPro-plugin-runtime-" + Guid.NewGuid().ToString("N"));
    [TestCleanup]
    public void Cleanup()
    {
        // Quantarix requests collectible-ALC unloading; Windows releases mapped DLLs after collection.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            GC.Collect(); GC.WaitForPendingFinalizers();
        }
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    [TestMethod]
    public async Task SharedHostFactoryCreatesCallerOwnedQuantarixManagersWithoutStartupDiscovery()
    {
        var host = CreateHost();
        var factory = host.Services.GetRequiredService<IPluginManagerFactory>();
        Assert.AreSame(factory, host.Services.GetRequiredService<IPluginManagerFactory>());
        Assert.IsFalse(Directory.Exists(_root));
        await HostExecution.RunAsync(host, _ =>
        {
            using var first = factory.Create(new HashSet<string>());
            using var second = factory.Create(new HashSet<string>());
            Assert.IsInstanceOfType<PluginManager>(first);
            Assert.AreNotSame(first, second);
            Assert.AreSame(host.Services.GetRequiredService<Quantarix.Plugin.Abstractions.ILogger>(), first.Logger);
            Assert.AreEqual(0, first.Plugins.Count);
            Assert.ThrowsExactly<ArgumentNullException>(() => factory.Create(null!));
            return Task.CompletedTask;
        });
        Assert.IsFalse(Directory.Exists(Path.Combine(_root, "plugins")));
        Assert.IsFalse(File.Exists(Path.Combine(_root, "communications.db")));
    }

    [TestMethod]
    public async Task RealQuantarixRuntimeLoadsExecutesAndShutsDownIsolatedFixtureWithSafeLogging()
    {
        var plugins = DeployFixture(); var host = CreateHost();
        await HostExecution.RunAsync(host, _ =>
        {
            using var manager = host.Services.GetRequiredService<IPluginManagerFactory>().Create(new HashSet<string>());
            manager.LoadFromDirectory(plugins);
            Assert.AreEqual(1, manager.Plugins.Count);
            var plugin = manager.Plugins.Single();
            var company = new CompanyKey(LexwareProductLine.Pro, "shared", "0001");
            var echo = plugin.GetType().GetMethod("EchoCompany")!;
            Assert.AreSame(typeof(CompanyKey), echo.GetParameters()[0].ParameterType);
            Assert.AreSame(company, echo.Invoke(plugin, [company]));
            Assert.AreEqual(AssemblyLoadContext.Default, AssemblyLoadContext.GetLoadContext(typeof(CompanyKey).Assembly));
            Assert.AreEqual("private-plugin-sentinel", plugin.Name);
            Assert.AreEqual(new Version(1, 0), plugin.Version);
            Assert.IsTrue(AssemblyLoadContext.GetLoadContext(plugin.GetType().Assembly)!.IsCollectible);
            manager.ExecuteAll(); manager.UnloadAll();
            Assert.AreEqual(1, plugin.GetType().GetProperty("Initializations")!.GetValue(plugin));
            Assert.AreEqual(1, plugin.GetType().GetProperty("Executions")!.GetValue(plugin));
            Assert.AreEqual(1, plugin.GetType().GetProperty("Shutdowns")!.GetValue(plugin));
            Assert.AreEqual(0, manager.Plugins.Count);
            return Task.CompletedTask;
        });
        var logs = string.Join('\n', Directory.GetFiles(Path.Combine(_root, "logs")).Select(File.ReadAllText));
        Assert.DoesNotContain("private-plugin-sentinel", logs);
        Assert.IsTrue(logs.Split("PluginMessage", StringSplitOptions.None).Length >= 5);
    }

    [TestMethod]
    public async Task CallerCannotDisableManagerTokenCheckByMutatingItsOriginalSet()
    {
        var plugins = DeployFixture(); var host = CreateHost();
        await HostExecution.RunAsync(host, _ =>
        {
            var tokens = new HashSet<string> { "0000000000000000" };
            using var manager = host.Services.GetRequiredService<IPluginManagerFactory>().Create(tokens);
            tokens.Clear();
            manager.LoadFromDirectory(plugins);
            Assert.AreEqual(0, manager.Plugins.Count);
            return Task.CompletedTask;
        });
    }

    private IHost CreateHost() => WawiCallHost.CreateBuilder(Assembly.GetExecutingAssembly()).ConfigureServices(services =>
    {
        services.AddSingleton(new ConfigurationLocation(_root));
        services.AddSingleton<IUiDispatcher, InlineUiDispatcher>();
    }).Build();

    private string DeployFixture([CallerFilePath] string source = "")
    {
        var variant = ApplicationIdentity.GetProductLine(Assembly.GetExecutingAssembly());
        var output = Path.Combine(Path.GetDirectoryName(source)!, "Fixtures", "RuntimePlugin", "bin", "Debug", variant, "net10.0");
        var plugins = Path.Combine(_root, "plugins"); var destination = Path.Combine(plugins, "RuntimePlugin");
        Directory.CreateDirectory(destination);
        // Only the fixture and its dependency manifest; abstractions must resolve to the host's loaded contract.
        foreach (var name in new[] { "RuntimePlugin.dll", "RuntimePlugin.deps.json", "WawiCallPro.Interfaces.dll", "Quantarix.Plugin.Abstractions.dll" }) File.Copy(Path.Combine(output, name), Path.Combine(destination, name));
        return plugins;
    }
}

using System.Reflection;
using WawiCallPro.ConsoleApp.Diagnostics;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Security;

namespace WawiCallPro.Console.Tests;

[TestClass]
public sealed class ErpSetupTests
{
    [TestMethod]
    public async Task InteractiveSetupProtectsSecretsAndPreservesOtherSettings()
    {
        var root = Path.Combine(Path.GetTempPath(), "WawiCallPro-erp-setup-" + Guid.NewGuid().ToString("N"));
        var location = new ConfigurationLocation(root);
        var secrets = new WindowsUserSecretProtector();
        var store = new ConfigurationStore(location, new ApplicationOptionsValidator(secrets), null);
        const string password = "synthetic-erp-password";
        const string key = "{33333333-3333-3333-3333-333333333333}";
        try
        {
            await store.SaveAsync(new ApplicationOptions { Erp = new() { FinOfficeInstallationId = "keep-fin-id" } });
            var input = new Input(true, ["https://isolated.invalid", "persistent-pro-id", "test-user", password, key]);
            var table = await ErpSetupCommands.ExecuteAsync("Pro", store, secrets, input, default);
            var loaded = await store.LoadAsync();
            Assert.AreEqual("keep-fin-id", loaded.Options.Erp.FinOfficeInstallationId);
            Assert.AreEqual("persistent-pro-id", loaded.Options.Erp.Pro!.InstallationId);
            using var decrypted = secrets.Unprotect(loaded.Options.Erp.Pro.Password!, ProErpSettings.PasswordPurpose);
            Assert.AreEqual(password, decrypted.Reveal());
            Assert.IsTrue(input.Secrecy.SequenceEqual(new[] { false, false, false, true, true }));
            var text = await File.ReadAllTextAsync(location.FilePath);
            Assert.DoesNotContain(password, text); Assert.DoesNotContain(key, text);
            Assert.IsFalse(table.Rows.Any(row => row.Value.Contains(password) || row.Value.Contains(key)));
            await ErpSetupCommands.ExecuteAsync("FinOffice", store, secrets, new Input(true, ["new-fin-id"]), default);
            var changed = await store.LoadAsync();
            Assert.AreEqual("test-user", changed.Options.Erp.Pro!.Username);
            Assert.AreEqual("new-fin-id", changed.Options.Erp.FinOfficeInstallationId);
            if (LexwareClientDefaults.GetProClientKey() is not null)
            {
                // An empty key adopts the local build default into DPAPI settings, usable by a later Release build.
                await ErpSetupCommands.ExecuteAsync("Pro", store, secrets,
                    new Input(true, ["https://isolated.invalid", "persistent-pro-id", "test-user", password, ""]), default);
                using var adopted = secrets.Unprotect((await store.LoadAsync()).Options.Erp.Pro!.ClientKey!, ProErpSettings.ClientKeyPurpose);
                Assert.IsTrue(adopted.Reveal() == LexwareClientDefaults.GetProClientKey());
                Assert.IsFalse((await File.ReadAllTextAsync(location.FilePath)).Contains(adopted.Reveal(), StringComparison.Ordinal));
            }
            // Verify the real host/container/collectible-plugin boundary with only synthetic credentials.
            var repository = new DirectoryInfo(AppContext.BaseDirectory);
            while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "WawiCallProSolution.sln"))) repository = repository.Parent;
            Assert.IsNotNull(repository);
            var descriptor = WawiCallPro.Utilities.Hosting.ApplicationDescriptor.FromAssembly(typeof(ConsoleCommand).Assembly);
            var configuration = typeof(ConsoleCommand).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
            var hostPath = Path.Combine(repository.FullName, "WawiCallPro.Console", "bin", configuration,
                "net11.0-windows10.0.19041.0", "WawiCallPro.Console." + descriptor.ProductLine + ".dll");
            var start = new System.Diagnostics.ProcessStartInfo("dotnet")
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
            };
            start.ArgumentList.Add(hostPath); start.ArgumentList.Add("--diagnostics");
            start.Environment["WAWICALLPRO_CONFIG_DIR"] = root;
            using var process = System.Diagnostics.Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try { await process.WaitForExitAsync(timeout.Token); }
            finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            Assert.AreEqual(0, process.ExitCode, await error);
            var json = await output;
            Assert.DoesNotContain(password, json); Assert.DoesNotContain(key, json);
            using var diagnostic = System.Text.Json.JsonDocument.Parse(json);
            Assert.IsTrue(diagnostic.RootElement.GetProperty("PluginLoaded").GetBoolean());
            Assert.IsTrue(diagnostic.RootElement.GetProperty("ErpConnectionConfigured").GetBoolean());
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task RedirectedOrCancelledSetupDoesNotCreateFilesAndCommandRejectsSecretArguments()
    {
        var root = Path.Combine(Path.GetTempPath(), "WawiCallPro-erp-setup-" + Guid.NewGuid().ToString("N"));
        var secrets = new WindowsUserSecretProtector();
        var store = new ConfigurationStore(new ConfigurationLocation(root), new ApplicationOptionsValidator(secrets), null);
        await Assert.ThrowsExactlyAsync<DiagnosticException>(() => ErpSetupCommands.ExecuteAsync("Pro", store, secrets, new Input(false, []), default));
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => ErpSetupCommands.ExecuteAsync("Pro", store, secrets, new Input(true, []), cancellation.Token));
        Assert.IsFalse(Directory.Exists(root));
        Assert.AreEqual("--erp-configure", ConsoleCommand.Parse(["--erp-configure"]).Name);
        var error = Assert.ThrowsExactly<DiagnosticException>(() => ConsoleCommand.Parse(["--erp-configure", "synthetic-secret"]));
        Assert.DoesNotContain("synthetic-secret", error.ToString());
    }

    private sealed class Input(bool interactive, string[] values) : IErpSetupInput
    {
        private int _index;
        public bool IsInteractive => interactive;
        public List<bool> Secrecy { get; } = [];
        public Task<string> ReadAsync(string prompt, bool secret, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested(); Secrecy.Add(secret);
            return Task.FromResult(values[_index++]);
        }
    }
}

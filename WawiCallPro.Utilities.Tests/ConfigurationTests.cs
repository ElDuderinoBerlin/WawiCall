using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WawiCallPro.Core.Threading;
using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Hosting;
using WawiCallPro.Utilities.Security;

namespace WawiCallPro.Utilities.Tests;

[TestClass]
public sealed class ConfigurationTests
{
    private const string Sentinel = "synthetic-password-Ä-🔐";

    private static ApplicationOptions ValidOptions(ISecretProtector protector) => new()
    {
        SipAccount = new SipAccountOptions
        {
            Server = "test.invalid",
            Username = "synthetic-user",
            Password = protector.Protect(Sentinel, SipAccountOptions.PasswordPurpose)
        }
    };

    [TestMethod]
    public async Task MissingFileProducesAnUnconfiguredSnapshotWithoutCreatingFiles()
    {
        using var directory = new TestDirectory();
        var snapshot = await directory.Store.LoadAsync();
        Assert.IsFalse(snapshot.FileFound);
        Assert.IsFalse(snapshot.IsSipConfigured);
        Assert.IsFalse(Directory.Exists(directory.Path));
    }

    [TestMethod]
    public async Task ProtectedSettingsRoundTripWithoutPersistingPlaintext()
    {
        RequireWindows();
        using var directory = new TestDirectory();
        var options = ValidOptions(directory.Protector);
        await directory.Store.SaveAsync(options);
        var persisted = await File.ReadAllTextAsync(directory.Location.FilePath);
        Assert.DoesNotContain(Sentinel, persisted);
        Assert.Contains("dpapi-user-v1:", persisted);
        var loaded = await directory.Store.LoadAsync();
        Assert.IsTrue(loaded.FileFound);
        Assert.IsTrue(loaded.IsSipConfigured);
        Assert.AreEqual("test.invalid", loaded.Options.SipAccount!.Server);
        using var password = directory.Protector.Unprotect(loaded.Options.SipAccount.Password!, SipAccountOptions.PasswordPurpose);
        Assert.AreEqual(Sentinel, password.Reveal());
        Assert.AreEqual("[redacted]", password.ToString());
        Assert.AreEqual("\"[redacted]\"", JsonSerializer.Serialize(password));
        Assert.AreEqual("[protected]", options.SipAccount!.Password!.ToString());
        Assert.DoesNotContain(Sentinel, options.ToString()!);
        password.Dispose();
        Assert.ThrowsExactly<ObjectDisposedException>(() => password.Reveal());
    }

    [TestMethod]
    public void WrongPurposeAndTamperedCiphertextAreRejectedWithoutPayloadInErrors()
    {
        RequireWindows();
        var protector = new WindowsUserSecretProtector();
        var secret = protector.Protect(Sentinel, SipAccountOptions.PasswordPurpose);
        var wrongPurpose = Assert.ThrowsExactly<SecretProtectionException>(() => protector.Unprotect(secret, "another-purpose"));
        Assert.DoesNotContain(Sentinel, wrongPurpose.ToString());
        var envelope = JsonSerializer.Deserialize<string>(JsonSerializer.Serialize(secret))!;
        const string prefix = "dpapi-user-v1:";
        var bytes = Convert.FromBase64String(envelope[prefix.Length..]);
        bytes[bytes.Length / 2] ^= 0x40;
        var corrupted = JsonSerializer.Deserialize<ProtectedSecret>(JsonSerializer.Serialize(prefix + Convert.ToBase64String(bytes)))!;
        var failure = Assert.ThrowsExactly<SecretProtectionException>(() => protector.Unprotect(corrupted, SipAccountOptions.PasswordPurpose));
        Assert.IsNull(failure.InnerException);
        Assert.DoesNotContain(envelope, failure.ToString());
    }

    [TestMethod]
    public async Task StrictJsonRejectsPlaintextUnknownDuplicateAndMalformedFieldsSafely()
    {
        using var directory = new TestDirectory();
        string[] rejected =
        [
            "{invalid-json-synthetic-password}",
            "null",
            "{\"sipAccount\":null,\"unexpected-synthetic-password\":true}",
            "{\"sipAccount\":null,\"sipAccount\":null}",
            "{\"sipAccount\":{\"password\":\"synthetic-password\"}}",
            "{\"sipAccount\":{\"password\":\"dpapi-user-v1:not-base64-synthetic-password\"}}",
            "{\"sipAccount\":{\"transport\":\"synthetic-password\"}}",
            "{\"sipAccount\":{\"transport\":1}}"
        ];
        Directory.CreateDirectory(directory.Path);
        foreach (var input in rejected)
        {
            await File.WriteAllTextAsync(directory.Location.FilePath, input);
            var failure = await Assert.ThrowsExactlyAsync<ConfigurationException>(() => directory.Store.LoadAsync());
            Assert.DoesNotContain("synthetic-password", failure.ToString());
            Assert.IsNull(failure.InnerException);
            Assert.AreEqual(input, await File.ReadAllTextAsync(directory.Location.FilePath));
        }
    }

    [TestMethod]
    public void PartialInvalidAccountReportsFieldsWithoutEchoingValues()
    {
        var options = new ApplicationOptions
        {
            SipAccount = new SipAccountOptions
            {
                Server = "user:synthetic-password@host.invalid",
                Username = "synthetic-password\r\n",
                Port = 0,
                Transport = (SipTransport)999
            }
        };
        var failure = Assert.ThrowsExactly<ConfigurationException>(() =>
            new ApplicationOptionsValidator(new WindowsUserSecretProtector()).Validate(options));
        Assert.HasCount(5, failure.Issues);
        Assert.IsTrue(failure.Issues.Any(issue => issue.Field == "sipAccount.server"));
        Assert.IsTrue(failure.Issues.Any(issue => issue.Field == "sipAccount.password"));
        Assert.DoesNotContain("synthetic-password", StartupFailureMessage.Format(failure));
    }

    [TestMethod]
    public async Task InvalidAndCancelledSavesPreserveThePreviousFile()
    {
        RequireWindows();
        using var directory = new TestDirectory();
        var valid = ValidOptions(directory.Protector);
        await directory.Store.SaveAsync(valid);
        var before = await File.ReadAllBytesAsync(directory.Location.FilePath);
        await Assert.ThrowsExactlyAsync<ConfigurationException>(() => directory.Store.SaveAsync(new ApplicationOptions { SipAccount = new() }));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => directory.Store.SaveAsync(valid, cancellation.Token));
        CollectionAssert.AreEqual(before, await File.ReadAllBytesAsync(directory.Location.FilePath));
        Assert.IsEmpty(Directory.GetFiles(directory.Path, "*.tmp"));
    }

    [TestMethod]
    public async Task AnUnusableProtectedPasswordBlocksLoading()
    {
        RequireWindows();
        using var directory = new TestDirectory();
        Directory.CreateDirectory(directory.Path);
        var wrongPurpose = directory.Protector.Protect(Sentinel, "another-purpose");
        var input = JsonSerializer.Serialize(new
        {
            sipAccount = new { server = "test.invalid", username = "synthetic-user", password = wrongPurpose }
        });
        await File.WriteAllTextAsync(directory.Location.FilePath, input);
        var failure = await Assert.ThrowsExactlyAsync<ConfigurationException>(() => directory.Store.LoadAsync());
        Assert.AreEqual("sipAccount.password", failure.Issues.Single().Field);
        Assert.DoesNotContain(Sentinel, failure.ToString());
    }

    [TestMethod]
    public async Task ValidatedAccountChangesOnlySafeDiagnosticFlags()
    {
        RequireWindows();
        using var directory = new TestDirectory();
        await directory.Store.SaveAsync(ValidOptions(directory.Protector));
        var host = CreateHost(directory.Location).Build();
        await HostExecution.RunAsync(host, async token =>
        {
            var snapshot = await host.Services.GetRequiredService<HostDiagnostics>().CaptureAsync(token);
            Assert.IsTrue(snapshot.ConfigurationFound);
            Assert.IsTrue(snapshot.ConnectionConfigured);
            var json = JsonSerializer.Serialize(snapshot);
            Assert.DoesNotContain(Sentinel, json);
            Assert.DoesNotContain("synthetic-user", json);
            Assert.DoesNotContain("test.invalid", json);
            Assert.DoesNotContain("dpapi-user-v1:", json);
        });
    }

    [TestMethod]
    public async Task InvalidConfigurationStopsStartupBeforeBusinessWorkers()
    {
        using var directory = new TestDirectory();
        Directory.CreateDirectory(directory.Path);
        await File.WriteAllTextAsync(directory.Location.FilePath, "{\"sipAccount\":{\"server\":\"synthetic-password@invalid\"}}");
        var worker = new StartupProbe();
        var host = CreateHost(directory.Location).ConfigureServices(services => services.AddSingleton<IHostedService>(worker)).Build();
        var bodyCalled = false;
        var failure = await Assert.ThrowsExactlyAsync<ConfigurationException>(() => HostExecution.RunAsync(host, _ =>
        {
            bodyCalled = true;
            return Task.CompletedTask;
        }));
        Assert.IsFalse(worker.Started);
        Assert.IsFalse(bodyCalled);
        Assert.DoesNotContain("synthetic-password", StartupFailureMessage.Format(new AggregateException(failure)));
    }

    [TestMethod]
    public async Task CancelledMissingLoadAndOversizedFilesAreNotAcceptedAsDefaults()
    {
        using var directory = new TestDirectory();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => directory.Store.LoadAsync(cancellation.Token));
        Directory.CreateDirectory(directory.Path);
        await File.WriteAllBytesAsync(directory.Location.FilePath, new byte[131073]);
        await Assert.ThrowsExactlyAsync<ConfigurationException>(() => directory.Store.LoadAsync());
    }

    [TestMethod]
    public void UnknownStartupFailuresNeverExposeRawExceptionMessages()
    {
        var failure = new InvalidOperationException(Sentinel);
        Assert.DoesNotContain(Sentinel, StartupFailureMessage.Format(failure));
        Assert.DoesNotContain(Sentinel, StartupFailureMessage.Format(new AggregateException(failure)));
    }

    [TestMethod]
    public async Task ErpCredentialsStayProtectedAndPluginReadsOnlyStartupSnapshot()
    {
        RequireWindows();
        using var directory = new TestDirectory();
        var key = "{22222222-2222-2222-2222-222222222222}";
        var erp = new ErpConnectionSettings { Pro = new ProErpSettings
        {
            Username = "synthetic-erp-user", ApiUrl = new Uri("https://isolated.invalid:9710"),
            InstallationId = "isolated-installation", Password = directory.Protector.Protect(Sentinel, ProErpSettings.PasswordPurpose),
            ClientKey = directory.Protector.Protect(key, ProErpSettings.ClientKeyPurpose)
        }, FinOfficeInstallationId = "isolated-fin" };
        await directory.Store.SaveAsync(new ApplicationOptions { Erp = erp });
        var text = await File.ReadAllTextAsync(directory.Location.FilePath);
        Assert.DoesNotContain(Sentinel, text); Assert.DoesNotContain(key, text);
        var configuration = new ApplicationConfigurationService(directory.Store);
        await configuration.StartAsync(default);
        var bridge = new PluginConnectionConfiguration(configuration, directory.Protector);
        var connection = bridge.ReadPro()!;
        Assert.AreEqual(Sentinel, connection.Password);
        Assert.AreEqual(key, connection.ClientKey);
        Assert.AreEqual("isolated-installation", connection.InstallationId);
        Assert.AreEqual("isolated-fin", bridge.ReadFinOfficeInstallationId());
        Assert.DoesNotContain(Sentinel, connection.ToString());
        Assert.DoesNotContain(key, JsonSerializer.Serialize(connection));
        var snapshot = await directory.Store.LoadAsync();
        await directory.Store.SaveAsync(snapshot.Options with { Erp = new() }, snapshot);
        Assert.AreEqual(Sentinel, bridge.ReadPro()!.Password);
        await configuration.StartAsync(default);
        Assert.IsNull(bridge.ReadPro());
    }

    [TestMethod]
    public void ErpValidationRejectsWrongPurposeMissingIdentityAndUnsafeUrlsWithoutLeakingInputs()
    {
        RequireWindows();
        using var directory = new TestDirectory();
        var valid = new ProErpSettings
        {
            Username = "synthetic-user", Password = directory.Protector.Protect(Sentinel, ProErpSettings.PasswordPurpose),
            ClientKey = directory.Protector.Protect("{22222222-2222-2222-2222-222222222222}", ProErpSettings.ClientKeyPurpose)
        };
        var validator = new ApplicationOptionsValidator(directory.Protector);
        validator.Validate(new ApplicationOptions { Erp = new() { Pro = valid } });
        foreach (var invalid in new[] { valid with { ClientKey = null }, valid with { Password = valid.ClientKey },
            valid with { ClientKey = directory.Protector.Protect(Sentinel, ProErpSettings.ClientKeyPurpose) },
            valid with { ApiUrl = new Uri("https://user:synthetic-password@isolated.invalid") },
            valid with { ApiUrl = new Uri("relative", UriKind.Relative) }, valid with { TimeoutSeconds = 0 } })
        {
            var error = Assert.ThrowsExactly<ConfigurationException>(() => validator.Validate(new ApplicationOptions { Erp = new() { Pro = invalid } }));
            Assert.DoesNotContain(Sentinel, error.ToString());
            Assert.DoesNotContain("synthetic-password", error.ToString());
        }
    }

    [TestMethod]
    public async Task VersionTwoSettingsUpgradePreservesExistingValuesWithoutCreatingErpCredentials()
    {
        using var directory = new TestDirectory();
        Directory.CreateDirectory(directory.Path);
        await File.WriteAllTextAsync(directory.Location.FilePath, """{"formatVersion":2,"settings":{"user":{"preferredCompany":null}}}""");
        var snapshot = await directory.Store.LoadAsync();
        Assert.IsTrue(snapshot.RequiresUpgrade);
        Assert.IsNull(snapshot.Options.Erp.Pro);
        await directory.Store.SaveAsync(snapshot.Options, snapshot);
        Assert.AreEqual(3, (await directory.Store.LoadAsync()).FormatVersion);
    }

    private static IHostBuilder CreateHost(ConfigurationLocation location) =>
        WawiCallHost.CreateBuilder(Assembly.GetExecutingAssembly()).ConfigureServices(services =>
        {
            services.AddSingleton(location);
            services.AddSingleton<IUiDispatcher, InlineUiDispatcher>();
        });

    private static void RequireWindows()
    {
        if (!OperatingSystem.IsWindows()) Assert.Inconclusive("This integration test requires Windows DPAPI.");
    }

    private sealed class StartupProbe : IHostedService
    {
        internal bool Started { get; private set; }
        public Task StartAsync(CancellationToken cancellationToken) { Started = true; return Task.CompletedTask; }
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class TestDirectory : IDisposable
    {
        internal string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "WawiCallPro.ConfigurationTests-" + Guid.NewGuid().ToString("N"));
        internal WindowsUserSecretProtector Protector { get; } = new();
        internal ConfigurationLocation Location { get; }
        internal ConfigurationStore Store { get; }

        internal TestDirectory()
        {
            Location = new ConfigurationLocation(Path);
            Store = new ConfigurationStore(Location, new ApplicationOptionsValidator(Protector));
        }

        public void Dispose()
        {
            var resolved = System.IO.Path.GetFullPath(Path);
            var temporaryRoot = System.IO.Path.GetFullPath(System.IO.Path.GetTempPath());
            if (!resolved.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase) ||
                !System.IO.Path.GetFileName(resolved).StartsWith("WawiCallPro.ConfigurationTests-", StringComparison.Ordinal))
                throw new InvalidOperationException("Unexpected test cleanup path.");
            if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
        }
    }
}

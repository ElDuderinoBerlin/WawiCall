using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WawiCallPro.Core.Threading;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Hosting;
using WawiCallPro.Utilities.Security;

namespace WawiCallPro.Utilities.Tests;

[TestClass]
public sealed class SettingsVersionTests
{
    [TestMethod]
    public async Task VersionFiveDefaultsToDirectXAndExplicitRenderingPreferenceRoundTrips()
    {
        using var directory = new SettingsDirectory();
        const string json = "{\"formatVersion\":5,\"settings\":{\"user\":{\"startMinimized\":true}}}";
        await File.WriteAllTextAsync(directory.Location.FilePath, json);
        var old = await directory.Store.LoadAsync();
        Assert.IsTrue(old.Options.User.UseDirectXRendering); Assert.IsTrue(old.Options.User.StartMinimized);
        Assert.AreEqual(json, await File.ReadAllTextAsync(directory.Location.FilePath));
        await directory.Store.SaveAsync(old.Options with { User = old.Options.User with { UseDirectXRendering = true } }, old);
        var enabled = await directory.Store.LoadAsync();
        Assert.AreEqual(6, enabled.FormatVersion); Assert.IsTrue(enabled.Options.User.UseDirectXRendering); Assert.IsTrue(enabled.Options.User.StartMinimized);
        await directory.Store.SaveAsync(enabled.Options with { User = enabled.Options.User with { UseDirectXRendering = false } }, enabled);
        Assert.IsFalse((await directory.Store.LoadAsync()).Options.User.UseDirectXRendering);
        Assert.IsTrue(new ApplicationOptions().User.UseDirectXRendering);
    }
    [TestMethod]
    public async Task VersionOneNumberContextsDoNotAutomaticallyEnablePhoneCacheSources()
    {
        RequireWindows();
        using var directory = new SettingsDirectory();
        const string json = "{\"formatVersion\":1,\"settings\":{\"companies\":[{\"company\":{\"productLine\":\"Pro\",\"installationId\":\"fixture\",\"externalId\":\"01\"},\"defaultPhoneRegion\":\"DE\"}]}}";
        await File.WriteAllTextAsync(directory.Location.FilePath, json);
        var snapshot = await directory.Store.LoadAsync();
        Assert.AreEqual(1, snapshot.FormatVersion);
        Assert.IsTrue(snapshot.RequiresUpgrade);
        Assert.IsFalse(snapshot.Options.Companies.Single().IncludeInPhoneCache);
        Assert.AreEqual(json, await File.ReadAllTextAsync(directory.Location.FilePath));
    }
    private static CompanyKey Company(string installation = "installation-A", LexwareProductLine product = LexwareProductLine.Pro) =>
        new(product, installation, "0007");

    [TestMethod]
    public async Task LegacyBomFileIsUpgradedOnlyOnExplicitSaveWithoutChangingCiphertext()
    {
        RequireWindows();
        using var directory = new SettingsDirectory();
        var password = directory.Protector.Protect("synthetic-migration-password", SipAccountOptions.PasswordPurpose);
        var legacyJson = JsonSerializer.Serialize(new
        {
            sipAccount = new { server = "migration.invalid", username = "synthetic-user", password }
        });
        var originalBytes = new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(legacyJson)).ToArray();
        await File.WriteAllBytesAsync(directory.Location.FilePath, originalBytes);
        var legacy = await directory.Store.LoadAsync();

        Assert.AreEqual(0, legacy.FormatVersion);
        Assert.IsTrue(legacy.RequiresUpgrade);
        Assert.IsTrue(legacy.IsSipConfigured);
        Assert.IsTrue(legacy.Options.User.ShowCallNotifications);
        Assert.AreEqual(100, legacy.Options.Devices.PlaybackVolumePercent);
        Assert.AreEqual(CommunicationDatabaseProvider.Sqlite, legacy.Options.Database.Provider);
        CollectionAssert.AreEqual(originalBytes, await File.ReadAllBytesAsync(directory.Location.FilePath));
        Assert.IsFalse(File.Exists(directory.Location.FilePath + ".lock"));

        await directory.Store.SaveAsync(legacy.Options, legacy);
        var current = await directory.Store.LoadAsync();
        Assert.AreEqual(ConfigurationFormat.CurrentVersion, current.FormatVersion);
        Assert.IsFalse(current.RequiresUpgrade);
        var savedJson = await File.ReadAllTextAsync(directory.Location.FilePath);
        Assert.Contains("\"formatVersion\": 6", savedJson);
        Assert.DoesNotContain("synthetic-migration-password", savedJson);
        Assert.AreEqual(JsonSerializer.Serialize(password), JsonSerializer.Serialize(current.Options.SipAccount!.Password));
    }

    [TestMethod]
    public async Task ScopedEditRoundTripsAllOtherSettingsAndProtectedDatabaseCredentials()
    {
        RequireWindows();
        using var directory = new SettingsDirectory();
        var key = Company();
        const string connection = "Server=synthetic.invalid;Database=communications;User Id=test;Password=synthetic-db-password";
        var options = new ApplicationOptions
        {
            User = new UserSettings { PreferredCompany = key },
            Devices = new AudioDeviceSettings { CaptureDeviceId = "capture-ä-001", RingDeviceId = "ring-002", RingVolumePercent = 70 },
            Database = new CommunicationDatabaseSettings
            {
                Provider = CommunicationDatabaseProvider.SqlServer,
                ConnectionString = directory.Protector.Protect(connection, CommunicationDatabaseSettings.ConnectionStringPurpose)
            },
            Companies = [new CompanySettings { Company = key, DefaultPhoneRegion = "DE", LocalAreaCode = "030" }]
        };
        await directory.Store.SaveAsync(options);
        var first = await directory.Store.LoadAsync();
        await directory.Store.SaveAsync(first.Options with { User = first.Options.User with { StartMinimized = true } }, first);
        var second = await directory.Store.LoadAsync();

        Assert.IsFalse(first.Options.User.StartMinimized);
        Assert.IsTrue(second.Options.User.StartMinimized);
        Assert.AreEqual(key, second.Options.User.PreferredCompany);
        Assert.AreEqual("capture-ä-001", second.Options.Devices.CaptureDeviceId);
        Assert.AreEqual(70, second.Options.Devices.RingVolumePercent);
        Assert.AreEqual("030", second.Options.FindCompanySettings(key)!.LocalAreaCode);
        Assert.AreEqual(CommunicationDatabaseProvider.SqlServer, second.Options.Database.Provider);
        using var revealed = directory.Protector.Unprotect(second.Options.Database.ConnectionString!, CommunicationDatabaseSettings.ConnectionStringPurpose);
        Assert.AreEqual(connection, revealed.Reveal());
        var saved = await File.ReadAllTextAsync(directory.Location.FilePath);
        Assert.DoesNotContain(connection, saved);
        Assert.DoesNotContain("synthetic-db-password", saved);
        Assert.DoesNotContain("synthetic.invalid", saved);
    }

    [TestMethod]
    public async Task FutureVersionsAndUnversionedNewFieldsRemainUntouched()
    {
        using var directory = new SettingsDirectory();
        string[] invalid =
        [
            "{\"formatVersion\":7,\"settings\":{}}",
            "{\"formatVersion\":0,\"settings\":{}}",
            "{\"formatVersion\":-1,\"settings\":{}}",
            "{\"formatVersion\":\"synthetic-value\",\"settings\":{}}",
            "{\"formatVersion\":1}",
            "{\"formatVersion\":1,\"settings\":null}",
            "{\"formatVersion\":1,\"settings\":{},\"unexpected\":\"synthetic-value\"}",
            "{\"formatVersion\":1,\"settings\":{\"unexpected\":\"synthetic-value\"}}",
            "{\"user\":{}}",
            "{\"formatVersion\":1,\"formatVersion\":1,\"settings\":{}}"
        ];
        foreach (var json in invalid)
        {
            await File.WriteAllTextAsync(directory.Location.FilePath, json);
            var failure = await Assert.ThrowsExactlyAsync<ConfigurationException>(() => directory.Store.LoadAsync());
            Assert.DoesNotContain("synthetic-value", failure.ToString());
            Assert.AreEqual(json, await File.ReadAllTextAsync(directory.Location.FilePath));
        }
    }

    [TestMethod]
    public async Task CompanyOverridesUseFullSourceIdentityAndKeepLeadingZeros()
    {
        using var directory = new SettingsDirectory();
        var first = Company();
        var otherInstallation = Company("installation-B");
        var otherProduct = Company(product: LexwareProductLine.FinOffice);
        var options = new ApplicationOptions
        {
            Companies =
            [
                new CompanySettings { Company = first, DefaultPhoneRegion = "DE", LocalAreaCode = "030" },
                new CompanySettings { Company = otherInstallation, DefaultPhoneRegion = "AT", LocalAreaCode = "01" },
                new CompanySettings { Company = otherProduct, DefaultPhoneRegion = "CH", LocalAreaCode = "044" }
            ]
        };
        await directory.Store.SaveAsync(options);
        var loaded = (await directory.Store.LoadAsync()).Options;
        Assert.AreEqual("030", loaded.FindCompanySettings(first)!.LocalAreaCode);
        Assert.AreEqual("AT", loaded.FindCompanySettings(otherInstallation)!.DefaultPhoneRegion);
        Assert.AreEqual("CH", loaded.FindCompanySettings(otherProduct)!.DefaultPhoneRegion);
        Assert.IsNull(loaded.FindCompanySettings(new CompanyKey(LexwareProductLine.Pro, "installation-A", "7")));
        Assert.IsNull(loaded.User.PreferredCompany);
    }

    [TestMethod]
    public void InvalidDeviceCompanyAndDatabaseScopesFailEvenWithoutSipAccount()
    {
        using var directory = new SettingsDirectory();
        var key = Company();
        ApplicationOptions[] invalid =
        [
            new() { User = null! },
            new() { Devices = null! },
            new() { Devices = new() { CaptureDeviceId = "synthetic-value\r\n" } },
            new() { Devices = new() { PlaybackVolumePercent = 101, RingVolumePercent = -1 } },
            new() { Database = null! },
            new() { Database = new() { Provider = (CommunicationDatabaseProvider)999 } },
            new() { Database = new() { SqliteFileName = "../synthetic-value.db" } },
            new() { Database = new() { SqliteFileName = "NUL.db" } },
            new() { Companies = default },
            new() { Companies = [null!] },
            new() { Companies = [new CompanySettings { Company = null! }] },
            new() { Companies = [new CompanySettings { Company = key }, new CompanySettings { Company = key }] },
            new() { Companies = [new CompanySettings { Company = key, DefaultPhoneRegion = "de" }] },
            new() { Companies = [new CompanySettings { Company = key, LocalAreaCode = "synthetic-value" }] }
        ];
        foreach (var options in invalid)
        {
            var failure = Assert.ThrowsExactly<ConfigurationException>(() => directory.Validator.Validate(options));
            Assert.DoesNotContain("synthetic-value", failure.ToString());
        }
    }

    [TestMethod]
    public async Task DatabaseCredentialRulesDoNotPermitPlaintextOrSipPurpose()
    {
        RequireWindows();
        using var directory = new SettingsDirectory();
        var wrongPurpose = directory.Protector.Protect("synthetic-database-password", SipAccountOptions.PasswordPurpose);
        foreach (var provider in new[] { CommunicationDatabaseProvider.SqlServer, CommunicationDatabaseProvider.MySql, CommunicationDatabaseProvider.PostgreSql })
        {
            var options = new ApplicationOptions { Database = new() { Provider = provider, ConnectionString = wrongPurpose } };
            var failure = Assert.ThrowsExactly<ConfigurationException>(() => directory.Validator.Validate(options));
            Assert.AreEqual("database.connectionString", failure.Issues.Single().Field);
        }
        var sqlite = new ApplicationOptions { Database = new() { ConnectionString = wrongPurpose } };
        Assert.ThrowsExactly<ConfigurationException>(() => directory.Validator.Validate(sqlite));
        var json = "{\"formatVersion\":1,\"settings\":{\"database\":{\"provider\":\"SqlServer\",\"connectionString\":\"synthetic-database-password\"}}}";
        await File.WriteAllTextAsync(directory.Location.FilePath, json);
        var parseFailure = await Assert.ThrowsExactlyAsync<ConfigurationException>(() => directory.Store.LoadAsync());
        Assert.DoesNotContain("synthetic-database-password", parseFailure.ToString());
    }

    [TestMethod]
    public async Task StaleWriterCannotDropAnAlreadySavedUserPreference()
    {
        using var directory = new SettingsDirectory();
        await directory.Store.SaveAsync(new ApplicationOptions());
        var firstEditor = await directory.Store.LoadAsync();
        var secondEditor = await directory.Store.LoadAsync();
        await directory.Store.SaveAsync(secondEditor.Options with { User = new UserSettings { StartMinimized = true } }, secondEditor);
        var latestBytes = await File.ReadAllBytesAsync(directory.Location.FilePath);

        await Assert.ThrowsExactlyAsync<ConfigurationException>(() => directory.Store.SaveAsync(
            firstEditor.Options with { Devices = new AudioDeviceSettings { MicrophoneMuted = true } }, firstEditor));

        CollectionAssert.AreEqual(latestBytes, await File.ReadAllBytesAsync(directory.Location.FilePath));
        var loaded = await directory.Store.LoadAsync();
        Assert.IsTrue(loaded.Options.User.StartMinimized);
        Assert.IsFalse(loaded.Options.Devices.MicrophoneMuted);
    }

    [TestMethod]
    public async Task MissingAndForeignSnapshotsCannotOverwriteAnotherFile()
    {
        using var directory = new SettingsDirectory();
        using var otherDirectory = new SettingsDirectory();
        var missing = await directory.Store.LoadAsync();
        await directory.Store.SaveAsync(new ApplicationOptions());
        await otherDirectory.Store.SaveAsync(new ApplicationOptions());
        var foreign = await otherDirectory.Store.LoadAsync();
        await Assert.ThrowsExactlyAsync<ConfigurationException>(() => directory.Store.SaveAsync(new ApplicationOptions(), missing));
        await Assert.ThrowsExactlyAsync<ConfigurationException>(() => directory.Store.SaveAsync(new ApplicationOptions()));
        Assert.ThrowsExactly<ConfigurationException>(() => directory.Store.SaveAsync(new ApplicationOptions(), foreign));
    }

    [TestMethod]
    public async Task ConcurrentStoresAcceptAtMostOneEditFromTheSameSnapshot()
    {
        using var directory = new SettingsDirectory();
        await directory.Store.SaveAsync(new ApplicationOptions());
        var snapshot = await directory.Store.LoadAsync();
        var secondStore = new ConfigurationStore(directory.Location, directory.Validator);
        async Task<bool> Save(ConfigurationStore store, ApplicationOptions options)
        {
            try { await store.SaveAsync(options, snapshot); return true; }
            catch (ConfigurationException) { return false; }
        }
        var results = await Task.WhenAll(
            Task.Run(() => Save(directory.Store, snapshot.Options with { User = new UserSettings { StartMinimized = true } })),
            Task.Run(() => Save(secondStore, snapshot.Options with { Devices = new AudioDeviceSettings { MicrophoneMuted = true } })));
        Assert.AreEqual(1, results.Count(result => result));
        var latest = await directory.Store.LoadAsync();
        Assert.IsTrue(latest.Options.User.StartMinimized ^ latest.Options.Devices.MicrophoneMuted);
        Assert.IsEmpty(Directory.GetFiles(directory.Path, "*.tmp"));
    }

    [TestMethod]
    public async Task HeldWriteLockFailsWithoutChangingTheFile()
    {
        using var directory = new SettingsDirectory();
        await directory.Store.SaveAsync(new ApplicationOptions());
        var snapshot = await directory.Store.LoadAsync();
        var before = await File.ReadAllBytesAsync(directory.Location.FilePath);
        using (var writeLock = new FileStream(directory.Location.FilePath + ".lock", FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            await Assert.ThrowsExactlyAsync<ConfigurationException>(() => directory.Store.SaveAsync(snapshot.Options, snapshot));
        }
        CollectionAssert.AreEqual(before, await File.ReadAllBytesAsync(directory.Location.FilePath));
    }

    [TestMethod]
    public async Task HostReportsFormatStatusWithoutRestoringPreferencesOrExposingCompanySettings()
    {
        using var directory = new SettingsDirectory();
        foreach (var legacy in new[] { true, false })
        {
            var json = legacy ? "{}" : "{\"formatVersion\":1,\"settings\":{\"user\":{\"preferredCompany\":{\"productLine\":\"Pro\",\"installationId\":\"synthetic-tenant-marker\",\"externalId\":\"0007\"}}}}";
            await File.WriteAllTextAsync(directory.Location.FilePath, json);
            var host = WawiCallHost.CreateBuilder(Assembly.GetExecutingAssembly()).ConfigureServices(services =>
            {
                services.AddSingleton(directory.Location);
                services.AddSingleton<IUiDispatcher, InlineUiDispatcher>();
            }).Build();
            await HostExecution.RunAsync(host, async token =>
            {
                var diagnostic = await host.Services.GetRequiredService<HostDiagnostics>().CaptureAsync(token);
                Assert.AreEqual(legacy ? 0 : 1, diagnostic.SettingsFormatVersion);
                Assert.IsTrue(diagnostic.SettingsUpgradeRequired);
                Assert.IsFalse(diagnostic.CompanySelected);
                Assert.DoesNotContain("synthetic-tenant-marker", JsonSerializer.Serialize(diagnostic));
            });
        }
    }

    private static void RequireWindows()
    {
        if (!OperatingSystem.IsWindows()) Assert.Inconclusive("This integration test requires Windows DPAPI.");
    }

    private sealed class SettingsDirectory : IDisposable
    {
        internal string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "WawiCallPro.SettingsTests-" + Guid.NewGuid().ToString("N"));
        internal WindowsUserSecretProtector Protector { get; } = new();
        internal ConfigurationLocation Location { get; }
        internal ApplicationOptionsValidator Validator { get; }
        internal ConfigurationStore Store { get; }

        internal SettingsDirectory()
        {
            Location = new ConfigurationLocation(Path);
            Validator = new ApplicationOptionsValidator(Protector);
            Store = new ConfigurationStore(Location, Validator);
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            var resolved = System.IO.Path.GetFullPath(Path);
            var temporaryRoot = System.IO.Path.GetFullPath(System.IO.Path.GetTempPath());
            if (!resolved.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase) ||
                !System.IO.Path.GetFileName(resolved).StartsWith("WawiCallPro.SettingsTests-", StringComparison.Ordinal))
                throw new InvalidOperationException("Unexpected test cleanup path.");
            Directory.Delete(resolved, true);
        }
    }
}

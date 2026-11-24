using System.Collections.Immutable;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Security;

namespace WawiCallPro.Utilities.Tests;

[TestClass]
public sealed class PortableSettingsTests
{
    [TestMethod]
    public async Task ExportUsesSavedPreferencesAndContainsNoConnectionsCredentialsOrLocalIdentities()
    {
        using var files = new Files(); var snapshot = await files.Store.LoadAsync();
        var options = LocalOptions(files.Protector) with { User = new() { LogLevel = LogLevel.Debug, ActivePluginId = "local-plugin", PreferredCompany = Company, DialHotkey = "Ctrl+Alt+F3" },
            Devices = new() { RecordConnectedCallsAutomatically = true } };
        await files.Store.SaveAsync(options, snapshot); await files.Store.ExportPortableAsync(files.Export);
        var json = await File.ReadAllTextAsync(files.Export);
        foreach (var forbidden in new[] { "synthetic-secret", "synthetic-user", "private.invalid", "local-device", "local-plugin", "private-installation", "sipAccount", "password", "connectionString", "companies", "callerLineAssignment", "activePluginId", "preferredCompany", "recordConnectedCallsAutomatically" })
            Assert.DoesNotContain(forbidden, json);
        var document = JsonNode.Parse(json)!; Assert.AreEqual(2, (int)document["formatVersion"]!); Assert.AreEqual("WawiCallPro", (string)document["application"]!);
        Assert.AreEqual("Debug", (string)document["user"]!["logLevel"]!); Assert.AreEqual("Ctrl+Alt+F3", (string)document["user"]!["dialHotkey"]!);
        Assert.IsFalse(Directory.GetFiles(files.Root).Any(path => path.EndsWith(".tmp", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ImportPreservesAllLocalScopesWhileUpdatingPortablePreferencesAndPersistsThem()
    {
        using var source = new Files(); using var target = new Files();
        await source.Store.SaveAsync(new() { User = new() { LogLevel = LogLevel.Trace, StartMinimized = true,
            ShowCallNotifications = false, ShowWindowHotkey = "Ctrl+Alt+F1", DialHotkey = "Ctrl+Alt+F2", LogDirectory = Path.Combine(source.Root, "logs") },
            Devices = new() { PlaybackVolumePercent = 35, RingVolumePercent = 65, MicrophoneMuted = true } });
        await source.Store.ExportPortableAsync(source.Export);
        var portable = JsonNode.Parse(await File.ReadAllTextAsync(source.Export))!;
        Assert.IsNull(portable["user"]!["useDirectXRendering"]);
        portable["user"]!["useDirectXRendering"] = false;
        await File.WriteAllTextAsync(source.Export, portable.ToJsonString());
        var local = LocalOptions(target.Protector);
        await target.Store.SaveAsync(local with { Devices = local.Devices with { RecordConnectedCallsAutomatically = true } });
        var before = await target.Store.LoadAsync();
        var after = await target.Store.ImportPortableAsync(source.Export, before); var options = after.Options;
        Assert.AreEqual(LogLevel.Trace, options.User.LogLevel); Assert.IsTrue(options.User.StartMinimized);
        Assert.IsFalse(options.User.ShowCallNotifications); Assert.AreEqual(35, options.Devices.PlaybackVolumePercent); Assert.AreEqual(65, options.Devices.RingVolumePercent); Assert.IsTrue(options.Devices.MicrophoneMuted);
        Assert.AreEqual(before.Options.User.ActivePluginId, options.User.ActivePluginId); Assert.AreEqual(before.Options.User.PreferredCompany, options.User.PreferredCompany);
        Assert.IsTrue(options.Devices.RecordConnectedCallsAutomatically);
        Assert.AreEqual(before.Options.Devices.CaptureDeviceId, options.Devices.CaptureDeviceId); Assert.AreEqual(before.Options.Devices.PlaybackDeviceId, options.Devices.PlaybackDeviceId); Assert.AreEqual(before.Options.Devices.RingDeviceId, options.Devices.RingDeviceId);
        Assert.AreEqual(before.Options.Database.SqliteFileName, options.Database.SqliteFileName); Assert.AreEqual(before.Options.Erp.FinOfficeInstallationId, options.Erp.FinOfficeInstallationId);
        Assert.AreEqual(before.Options.SipAccount!.Server, options.SipAccount!.Server); Assert.AreEqual(before.Options.SipAccount.Username, options.SipAccount.Username);
        using var password = target.Protector.Unprotect(options.SipAccount.Password!, SipAccountOptions.PasswordPurpose); Assert.AreEqual("synthetic-secret", password.Reveal());
        CollectionAssert.AreEqual(before.Options.Companies.ToArray(), options.Companies.ToArray()); Assert.AreEqual(before.Options.CallerLineAssignment, options.CallerLineAssignment);
        Assert.AreEqual(LogLevel.Trace, (await target.Store.LoadAsync()).Options.User.LogLevel);
    }

    [TestMethod]
    [DataRow("version")]
    [DataRow("application")]
    [DataRow("unknown")]
    [DataRow("missing")]
    [DataRow("null")]
    [DataRow("invalid-volume")]
    [DataRow("invalid-hotkeys")]
    [DataRow("integer-enum")]
    [DataRow("truncated")]
    [DataRow("duplicate")]
    public async Task MalformedOrInvalidPortableJsonNeverReplacesTheLocalConfiguration(string mode)
    {
        using var files = new Files(); await files.Store.SaveAsync(new()); var snapshot = await files.Store.LoadAsync();
        await files.Store.ExportPortableAsync(files.Export); var node = JsonNode.Parse(await File.ReadAllTextAsync(files.Export))!;
        switch (mode)
        {
            case "version": node["formatVersion"] = 3; break;
            case "application": node["application"] = "Other"; break;
            case "unknown": node["password"] = "private-detail"; break;
            case "missing": node.AsObject().Remove("audio"); break;
            case "null": node["user"] = null; break;
            case "invalid-volume": node["audio"]!["ringVolumePercent"] = 101; break;
            case "invalid-hotkeys": node["user"]!["dialHotkey"] = "F1"; break;
            case "integer-enum": node["user"]!["logLevel"] = 2; break;
        }
        var json = mode == "truncated" ? "{" : node.ToJsonString();
        if (mode == "duplicate") json = json.Replace("\"formatVersion\":2", "\"formatVersion\":2,\"formatVersion\":2", StringComparison.Ordinal);
        await File.WriteAllTextAsync(files.Export, json); var before = await File.ReadAllBytesAsync(files.Location.FilePath);
        var error = await Assert.ThrowsExactlyAsync<ConfigurationException>(() => files.Store.ImportPortableAsync(files.Export, snapshot));
        Assert.DoesNotContain("private-detail", error.ToString()); CollectionAssert.AreEqual(before, await File.ReadAllBytesAsync(files.Location.FilePath));
    }

    [TestMethod]
    public async Task StaleImportCannotOverwriteAConcurrentSettingsChange()
    {
        using var files = new Files(); await files.Store.SaveAsync(new()); var stale = await files.Store.LoadAsync(); await files.Store.ExportPortableAsync(files.Export);
        await files.Store.SaveAsync(stale.Options with { User = stale.Options.User with { StartMinimized = true } }, stale);
        await Assert.ThrowsExactlyAsync<ConfigurationException>(() => files.Store.ImportPortableAsync(files.Export, stale));
        Assert.IsTrue((await files.Store.LoadAsync()).Options.User.StartMinimized);
    }

    [TestMethod]
    public async Task CancellationSizeAndReservedPathsLeaveExistingFilesUnchanged()
    {
        using var files = new Files(); await files.Store.SaveAsync(new()); var snapshot = await files.Store.LoadAsync();
        await File.WriteAllTextAsync(files.Export, "existing-export");
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => files.Store.ExportPortableAsync(files.Export, cancellation.Token));
        Assert.AreEqual("existing-export", await File.ReadAllTextAsync(files.Export));
        foreach (var path in new[] { files.Location.FilePath, Path.Combine(files.Root, "other.txt"), "relative.json" })
        {
            await Assert.ThrowsExactlyAsync<ConfigurationException>(() => files.Store.ExportPortableAsync(path));
            await Assert.ThrowsExactlyAsync<ConfigurationException>(() => files.Store.ImportPortableAsync(path, snapshot));
        }
        await File.WriteAllBytesAsync(files.Export, new byte[131073]);
        await Assert.ThrowsExactlyAsync<ConfigurationException>(() => files.Store.ImportPortableAsync(files.Export, snapshot));
        Assert.IsFalse((await files.Store.LoadAsync()).Options.User.StartMinimized);
    }

    [TestMethod]
    public async Task EmptyStoreCanExportDefaultsAndFileFailuresRemainSafe()
    {
        using var files = new Files(); await files.Store.ExportPortableAsync(files.Export); Assert.IsFalse(File.Exists(files.Location.FilePath));
        var imported = await files.Store.ImportPortableAsync(files.Export, await files.Store.LoadAsync()); Assert.IsTrue(imported.FileFound);
        var missing = Path.Combine(files.Root, "missing", "private-path.json");
        var readError = await Assert.ThrowsExactlyAsync<ConfigurationException>(() => files.Store.ImportPortableAsync(missing, imported));
        var writeError = await Assert.ThrowsExactlyAsync<ConfigurationException>(() => files.Store.ExportPortableAsync(missing));
        Assert.DoesNotContain("private-path", readError.ToString()); Assert.DoesNotContain("private-path", writeError.ToString());
    }

    private static CompanyKey Company { get; } = new(LexwareProductLine.FinOffice, "private-installation", "01");
    private static ApplicationOptions LocalOptions(ISecretProtector protector)
    {
        var account = new SipAccountOptions { Server = "private.invalid", Username = "synthetic-user", Password = protector.Protect("synthetic-secret", SipAccountOptions.PasswordPurpose) };
        return new() { SipAccount = account, Erp = new() { FinOfficeInstallationId = "private-installation" },
            User = new() { ActivePluginId = "local-plugin", PreferredCompany = Company },
            Devices = new() { CaptureDeviceId = "local-device-capture", PlaybackDeviceId = "local-device-playback", RingDeviceId = "local-device-ring" },
            Database = new() { SqliteFileName = "local-database.db" }, Companies = ImmutableArray.Create(new CompanySettings { Company = Company, IncludeInPhoneCache = true }),
            CallerLineAssignment = CallerLineConfiguration.Create(account, Company) };
    }
    private sealed class Files : IDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "WawiCallPro-portable-" + Guid.NewGuid().ToString("N"));
        internal string Export => Path.Combine(Root, "preferences.json");
        internal WindowsUserSecretProtector Protector { get; } = new();
        internal ConfigurationLocation Location { get; }
        internal ConfigurationStore Store { get; }
        internal Files() { Directory.CreateDirectory(Root); Location = new(Root); Store = new(Location, new(Protector)); }
        public void Dispose() { foreach (var file in Directory.GetFiles(Root)) File.Delete(file); Directory.Delete(Root); }
    }
}

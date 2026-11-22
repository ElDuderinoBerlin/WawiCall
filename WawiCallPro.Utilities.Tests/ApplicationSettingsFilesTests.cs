using System.Text.Json;
using System.Text.Json.Nodes;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Security;

namespace WawiCallPro.Utilities.Tests;

[TestClass]
public sealed class ApplicationSettingsFilesTests
{
    [TestMethod]
    public async Task DefaultsAndLocalOverridesUseTwoAppsettingsFilesAndLegacyMigrationKeepsOriginal()
    {
        using var files = new Files();
        await File.WriteAllTextAsync(files.Product, """{"settings":{"user":{"showCallNotifications":false,"startMinimized":true}},"LexwareApi":{"TimeoutSeconds":42}}""");
        var first = await files.Store.LoadAsync();
        Assert.IsFalse(first.FileFound); Assert.IsTrue(first.Options.User.StartMinimized);
        Assert.IsFalse(first.Options.User.ShowCallNotifications);
        var legacy = Path.Combine(files.Profile, "connection.local.json");
        await files.Store.SaveAsync(first.Options with { User = first.Options.User with { StartMinimized = false } });
        File.Move(files.Location.FilePath, legacy);
        var original = await File.ReadAllBytesAsync(legacy);
        var loaded = await files.Store.LoadAsync(); Assert.IsFalse(loaded.Options.User.StartMinimized);
        Assert.AreEqual("appsettings.json", Path.GetFileName(files.Location.FilePath));
        await files.Store.SaveAsync(loaded.Options, loaded);
        Assert.IsTrue(File.Exists(files.Location.FilePath));
        CollectionAssert.AreEqual(original, await File.ReadAllBytesAsync(legacy));
        var current = await files.Store.LoadAsync();
        await files.Store.SaveAsync(current.Options with { User = current.Options.User with { StartMinimized = true } }, current);
        var stale = loaded;
        await Assert.ThrowsExactlyAsync<ConfigurationException>(() => files.Store.SaveAsync(stale.Options, stale));
    }
    [TestMethod]
    public async Task DraftPreviewUsesProtectedSavedSecretsWithoutWritingOrPublishing()
    {
        using var files = new Files();
        var key = Guid.NewGuid().ToString("B");
        await File.WriteAllTextAsync(files.Product, JsonSerializer.Serialize(new { LexwareApi = new { ClientKey = ClientKeyEncryptor.Encrypt(key) } }));
        var snapshot = await files.Store.LoadAsync();
        var edit = files.Store.BeginPluginConnectionEdit(snapshot, files.Secrets);
        var preview = edit.ReadProPreview(new() { Username = "test-api", Password = "synthetic-preview-secret", UseDefaultClientKey = true });
        Assert.AreEqual("synthetic-preview-secret", preview!.Password); Assert.AreEqual(key, preview.ClientKey);
        Assert.IsFalse(File.Exists(files.Location.FilePath));
        Assert.IsNull((await files.Store.LoadAsync()).Options.Erp.Pro);
    }
    [TestMethod]
    public async Task ClientKeyEncryptionIsPortableIdempotentAndPreservesOtherValuesAndInvalidInputs()
    {
        using var files = new Files(); var key = Guid.NewGuid().ToString("B");
        await File.WriteAllTextAsync(files.Product, JsonSerializer.Serialize(new { LexwareApi = new { ClientKey = key, TimeoutSeconds = 47 }, Other = "keep" }));
        var settings = new ProductAppSettings(files.Product);
        Assert.IsTrue(settings.HasPlaintextClientKey()); Assert.IsTrue(settings.EncryptClientKey());
        var encrypted = await File.ReadAllTextAsync(files.Product);
        Assert.DoesNotContain(key, encrypted); Assert.AreEqual(key, settings.ReadClientKey());
        Assert.IsFalse(settings.HasPlaintextClientKey()); Assert.IsFalse(settings.EncryptClientKey());
        Assert.AreEqual(encrypted, await File.ReadAllTextAsync(files.Product));
        Assert.AreEqual(47, settings.Read()["LexwareApi"]!["TimeoutSeconds"]!.GetValue<int>());
        Assert.AreEqual("keep", settings.Read()["Other"]!.GetValue<string>());
        var node = JsonNode.Parse(encrypted)!; node["LexwareApi"]!["ClientKey"] = "invalid-synthetic-payload";
        await File.WriteAllTextAsync(files.Product, node.ToJsonString()); var before = await File.ReadAllBytesAsync(files.Product);
        Assert.ThrowsExactly<ConfigurationException>(() => settings.EncryptClientKey());
        CollectionAssert.AreEqual(before, await File.ReadAllBytesAsync(files.Product));
    }
    [TestMethod]
    public async Task ProductKeyDraftWritesOnlyOnSaveAndAStaleProductFileCannotBeOverwritten()
    {
        using var files = new Files(); var original = Guid.NewGuid().ToString("B"); var replacement = Guid.NewGuid().ToString("B");
        await File.WriteAllTextAsync(files.Product, JsonSerializer.Serialize(new { settings = new { }, LexwareApi = new { ClientKey = ClientKeyEncryptor.Encrypt(original) } }));
        var snapshot = await files.Store.LoadAsync();
        var edit = files.Store.BeginPluginConnectionEdit(snapshot, files.Secrets);
        edit.StagePro(new() { Username = "synthetic-user", Password = "synthetic-password", ClientKey = replacement });
        var options = edit.ApplyTo(snapshot.Options);
        Assert.AreEqual(original, new ProductAppSettings(files.Product).ReadClientKey());
        Assert.IsNull(options.Erp.Pro!.ClientKey);
        await files.Store.SaveAsync(options, snapshot);
        Assert.AreEqual(replacement, new ProductAppSettings(files.Product).ReadClientKey());
        var saved = await files.Store.LoadAsync(); Assert.IsNull(saved.Options.Erp.Pro!.ClientKey);
        Assert.DoesNotContain(replacement, await File.ReadAllTextAsync(files.Location.FilePath));
        var stale = files.Store.BeginPluginConnectionEdit(saved, files.Secrets);
        stale.StagePro(new() { Username = "synthetic-user", ClientKey = Guid.NewGuid().ToString("B") });
        new ProductAppSettings(files.Product).EncryptClientKey();
        var bytes = await File.ReadAllBytesAsync(files.Location.FilePath);
        await Assert.ThrowsExactlyAsync<ConfigurationException>(() => files.Store.SaveAsync(stale.ApplyTo(saved.Options), saved));
        CollectionAssert.AreEqual(bytes, await File.ReadAllBytesAsync(files.Location.FilePath));
        Assert.AreEqual(replacement, new ProductAppSettings(files.Product).ReadClientKey());
    }
    [TestMethod]
    public async Task DebugDefaultsPopulateOnlyAnEmptyEditorAndNeverReplaceStoredCredentials()
    {
        using var files = new Files();
        var encrypted = JsonSerializer.Serialize(files.Secrets.Protect("synthetic-debug-password", ProErpSettings.PasswordPurpose));
        await File.WriteAllTextAsync(files.Product, "{\"DebugLexware\":{\"Username\":\"synthetic-debug-user\",\"Password\":" + encrypted + "}}");
        var snapshot = await files.Store.LoadAsync(); var edit = files.Store.BeginPluginConnectionEdit(snapshot, files.Secrets);
#if DEBUG
        Assert.AreEqual("synthetic-debug-user", edit.ReadPro().Username);
        Assert.AreEqual("synthetic-debug-password", edit.ReadPro().InitialPassword);
#else
        Assert.IsNull(edit.ReadPro().InitialPassword);
#endif
        await files.Store.SaveAsync(snapshot.Options with { Erp = new() { Pro = new()
        { Username = "existing-user", Password = files.Secrets.Protect("existing-password", ProErpSettings.PasswordPurpose),
            ClientKey = files.Secrets.Protect(Guid.NewGuid().ToString("B"), ProErpSettings.ClientKeyPurpose) } } }, snapshot);
        var reopened = files.Store.BeginPluginConnectionEdit(await files.Store.LoadAsync(), files.Secrets).ReadPro();
        Assert.AreEqual("existing-user", reopened.Username); Assert.IsNull(reopened.InitialPassword);
    }
    [TestMethod]
    public async Task ProductTechnicalSettingsOverrideLegacyProfileValuesInEditorAndRuntime()
    {
        using var files = new Files();
        var key = Guid.NewGuid().ToString("B");
        await File.WriteAllTextAsync(files.Product, JsonSerializer.Serialize(new { LexwareApi = new { ClientKey = ClientKeyEncryptor.Encrypt(key), InstallationId = "product", TimeoutSeconds = 47, TokenTimeoutSeconds = 17 } }));
        var initial = await files.Store.LoadAsync();
        await files.Store.SaveAsync(initial.Options with { Erp = new() { Pro = new()
        { Username = "synthetic-user", Password = files.Secrets.Protect("synthetic-password", ProErpSettings.PasswordPurpose),
            InstallationId = "old-profile", TimeoutSeconds = 12, TokenTimeoutSeconds = 9 } } }, initial);
        var snapshot = await files.Store.LoadAsync();
        var editor = files.Store.BeginPluginConnectionEdit(snapshot, files.Secrets).ReadPro();
        Assert.AreEqual("product", editor.InstallationId); Assert.AreEqual(47, editor.TimeoutSeconds); Assert.AreEqual(17, editor.TokenTimeoutSeconds);
        var configuration = new ApplicationConfigurationService(files.Store);
        await configuration.StartAsync(default);
        var runtime = new PluginConnectionConfiguration(configuration, files.Secrets).ReadPro()!;
        Assert.AreEqual("product", runtime.InstallationId); Assert.AreEqual(47, runtime.TimeoutSeconds); Assert.AreEqual(17, runtime.TokenTimeoutSeconds);
    }
    [TestMethod]
    public async Task SparseProfilePreservesGlobalDefaultsAndOverridesOnlyExplicitLoggingValues()
    {
        using var files = new Files();
        await File.WriteAllTextAsync(files.Product, """{"settings":{"user":{"startMinimized":true}},"Logging":{"Enabled":false,"LogLevel":"Warning"}}""");
        await File.WriteAllTextAsync(files.Location.FilePath, """{"formatVersion":14,"settings":{"user":{"showCallNotifications":false}},"Logging":{"LogLevel":"Error"}}""");
        var loaded = await files.Store.LoadAsync();
        Assert.IsTrue(loaded.Options.User.StartMinimized);
        Assert.IsFalse(loaded.Options.User.ShowCallNotifications);
        Assert.IsFalse(loaded.Options.User.LoggingEnabled);
        Assert.AreEqual(Microsoft.Extensions.Logging.LogLevel.Error, loaded.Options.User.LogLevel);
    }
    [TestMethod]
    public async Task LoggingDefaultsAndLocalPreferencesUseTheSameValuesAsSerilogAndPortableExport()
    {
        using var files = new Files();
        await File.WriteAllTextAsync(files.Product, """{"Logging":{"Enabled":false,"LogLevel":"Warning","LogDirectory":null}}""");
        var defaults = await files.Store.LoadAsync();
        Assert.IsFalse(defaults.Options.User.LoggingEnabled);
        Assert.AreEqual(Microsoft.Extensions.Logging.LogLevel.Warning, defaults.Options.User.LogLevel);
        var runtime = new WawiCallPro.Utilities.Operations.RuntimeLoggingSettings(new(files.Location));
        var configuration = new ApplicationConfigurationService(files.Store, runtime);
        await configuration.StartAsync(default);
        Assert.IsFalse(runtime.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Critical));
        await files.Store.SaveAsync(defaults.Options with { User = defaults.Options.User with { LoggingEnabled = true, LogLevel = Microsoft.Extensions.Logging.LogLevel.Debug } }, defaults);
        var saved = await files.Store.LoadAsync();
        Assert.IsTrue(saved.Options.User.LoggingEnabled); Assert.AreEqual(Microsoft.Extensions.Logging.LogLevel.Debug, saved.Options.User.LogLevel);
        var json = JsonNode.Parse(await File.ReadAllTextAsync(files.Location.FilePath))!;
        Assert.IsTrue(json["Logging"]!["Enabled"]!.GetValue<bool>());
        Assert.AreEqual("Debug", json["Logging"]!["LogLevel"]!.GetValue<string>());
        await configuration.StartAsync(default); Assert.IsTrue(runtime.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Debug));
        var portable = Path.Combine(files.Root, "portable.json");
        await files.Store.ExportPortableAsync(portable);
        Assert.IsTrue(JsonNode.Parse(await File.ReadAllTextAsync(portable))!["user"]!["loggingEnabled"]!.GetValue<bool>());
    }
    private sealed class Files : IDisposable
    {
        internal readonly string Root = Path.Combine(Path.GetTempPath(), "Wcp-AppSettings-" + Guid.NewGuid().ToString("N"));
        internal string Product => Path.Combine(Root, "product", "appsettings.json");
        internal string Profile => Path.Combine(Root, "profile");
        internal WindowsUserSecretProtector Secrets = new();
        internal ConfigurationLocation Location;
        internal ConfigurationStore Store;
        internal Files()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Product)!); Directory.CreateDirectory(Profile);
            Location = new(Profile, Path.GetDirectoryName(Product)); Store = new(Location, new(Secrets));
        }
        public void Dispose() => Directory.Delete(Root, true);
    }
}

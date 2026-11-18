using System.Collections.Immutable;
using System.Text.Json;
using WawiCallPro.Interfaces.Office;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Security;

namespace WawiCallPro.Office.Tests;

[TestClass]
public sealed class MailConfigurationTests
{
    [TestMethod]
    public async Task VersionNineKeepsMailConnectionsLocalAndPortableImportPreservesThem()
    {
        var root = Path.Combine(Path.GetTempPath(), "wcp-mail-config-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var secrets = new WindowsUserSecretProtector(); var location = new ConfigurationLocation(root);
            var store = new ConfigurationStore(location, new(secrets));
            var account = MailConnectorTests.GraphAccount() with { ApplicationAuthentication = true, Tenant = Guid.NewGuid().ToString(), Mailbox = "owner@example.test",
                MicrosoftClientSecret = secrets.Protect("synthetic-m365-secret", MailAccountSettings.MicrosoftSecretPurpose), FolderIds = ["inbox"] };
            await store.SaveAsync(new() { MailAccounts = [account] }); var snapshot = await store.LoadAsync();
            Assert.AreEqual(ConfigurationFormat.CurrentVersion, snapshot.FormatVersion); Assert.AreEqual(account.Id, snapshot.Options.MailAccounts.Single().Id);
            using var value = secrets.Unprotect(snapshot.Options.MailAccounts.Single().MicrosoftClientSecret!, MailAccountSettings.MicrosoftSecretPurpose); Assert.AreEqual("synthetic-m365-secret", value.Reveal());
            var portable = Path.Combine(root, "portable.json"); await store.ExportPortableAsync(portable);
            var text = await File.ReadAllTextAsync(portable); Assert.DoesNotContain("mailAccounts", text); Assert.DoesNotContain("synthetic-m365-secret", text); Assert.DoesNotContain(account.ClientId!, text);
            var restored = await store.ImportPortableAsync(portable, snapshot); Assert.AreEqual(account.Id, restored.Options.MailAccounts.Single().Id);
            var original = await File.ReadAllTextAsync(location.FilePath); await File.WriteAllTextAsync(location.FilePath, original.Replace("\"formatVersion\": " + ConfigurationFormat.CurrentVersion, "\"formatVersion\": 8"));
            Assert.IsTrue((await store.LoadAsync()).RequiresUpgrade);
        }
        finally { Directory.Delete(root, true); }
    }
    [TestMethod]
    [DataRow("id")][DataRow("provider")][DataRow("name")][DataRow("limit")][DataRow("days")][DataRow("folders")]
    [DataRow("client")][DataRow("tenant")][DataRow("application-secret")][DataRow("application-mailbox")][DataRow("google")][DataRow("duplicate")]
    public void InvalidMailSettingsFailBeforeAnyProviderIsOpened(string invalid)
    {
        var secrets = new WindowsUserSecretProtector(); var account = MailConnectorTests.GraphAccount();
        account = invalid switch
        {
            "id" => account with { Id = Guid.Empty }, "provider" => account with { Provider = (MailProviderKind)77 },
            "name" => account with { DisplayName = "\n" }, "limit" => account with { MaximumMessages = 0 }, "days" => account with { ImportDays = -1 },
            "folders" => account with { FolderIds = ["inbox", "inbox"] }, "client" => account with { ClientId = "invalid" }, "tenant" => account with { Tenant = "https://attacker.invalid" },
            "application-secret" => account with { ApplicationAuthentication = true, Tenant = Guid.NewGuid().ToString(), Mailbox = "owner@example.test" },
            "application-mailbox" => account with { ApplicationAuthentication = true, Tenant = Guid.NewGuid().ToString(), Mailbox = "invalid", MicrosoftClientSecret = secrets.Protect("synthetic", MailAccountSettings.MicrosoftSecretPurpose) },
            "google" => account with { Provider = MailProviderKind.Gmail, ClientId = "client.apps.googleusercontent.com" }, _ => account
        };
        Assert.ThrowsExactly<ConfigurationException>(() => new ApplicationOptionsValidator(secrets).Validate(new() { MailAccounts = invalid == "duplicate" ? [account, account] : [account] }));
    }
    [TestMethod]
    public void DisabledDraftCanBeSavedBeforeOAuthRegistrationAndSecretsAreNotDiagnostics()
    {
        var draft = new MailAccountSettings { Id = Guid.NewGuid(), Provider = MailProviderKind.Gmail, Enabled = false };
        new ApplicationOptionsValidator(new WindowsUserSecretProtector()).Validate(new() { MailAccounts = [draft] });
        Assert.AreEqual(nameof(MailAccountSettings), draft.ToString()); Assert.AreEqual("[mail-access-token]", new MailAccessToken("private", "identity").ToString());
        Assert.DoesNotContain("private", JsonSerializer.Serialize(new MailAccessToken("private", "identity")));
    }
}

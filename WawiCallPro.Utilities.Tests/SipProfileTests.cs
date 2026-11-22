using System.Collections.Immutable;
using System.Text.Json.Nodes;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Security;

namespace WawiCallPro.Utilities.Tests;

[TestClass]
public sealed class SipProfileTests
{
    [TestMethod]
    public async Task ProfilesRoundTripSeparateSecretsDiscardLegacyLinesAndKeepSelectedAccountAtRoot()
    {
        using var files = new Files();
        var drafts = files.Profiles();
        var first = SipProfileCatalog.Select(new(), drafts, drafts[0].Id);
        await files.Store.SaveAsync(first);
        var before = await files.Store.LoadAsync();
        Assert.AreEqual(13, before.FormatVersion);
        Assert.IsNull(before.Options.SipProfiles[0].Account);
        Assert.IsNull(before.Options.SipProfiles[0].LineAssignment);
        var next = SipProfileCatalog.Select(before.Options, SipProfileCatalog.Read(before.Options), drafts[1].Id);
        await files.Store.SaveAsync(next, before);
        var after = (await files.Store.LoadAsync()).Options;
        Assert.AreEqual(drafts[1].Id, after.ActiveSipProfileId);
        Assert.AreEqual("second.invalid", after.SipAccount!.Server);
        Assert.IsNull(after.CallerLineAssignment);
        var restored = SipProfileCatalog.Read(after);
        for (var index = 0; index < 2; index++)
        {
            Assert.IsNull(restored[index].LineAssignment);
            using var secret = files.Protector.Unprotect(restored[index].Account!.Password!, SipAccountOptions.PasswordPurpose);
            Assert.AreEqual("synthetic-profile-secret-" + index, secret.Reveal());
        }
        var json = await File.ReadAllTextAsync(files.Location.FilePath);
        Assert.DoesNotContain("synthetic-profile-secret", json);
        Assert.DoesNotContain("second.invalid", drafts[1].ToString());
        Assert.DoesNotContain("synthetic-profile-secret", after.ToString());
    }

    [TestMethod]
    public async Task LegacyVersionTwelveAccountIsPreservedUntilExplicitSaveAndEmptyDefaultsStayUnconfigured()
    {
        using var files = new Files();
        var account = files.Profiles()[0].Account;
        await files.Store.SaveAsync(new() { SipAccount = account });
        var node = JsonNode.Parse(await File.ReadAllTextAsync(files.Location.FilePath))!;
        node["formatVersion"] = 12;
        node["settings"]!.AsObject().Remove("sipProfiles");
        node["settings"]!.AsObject().Remove("activeSipProfileId");
        await File.WriteAllTextAsync(files.Location.FilePath, node.ToJsonString());
        var bytes = await File.ReadAllBytesAsync(files.Location.FilePath);
        var legacy = await files.Store.LoadAsync();
        Assert.IsTrue(legacy.RequiresUpgrade);
        var drafts = SipProfileCatalog.Read(legacy.Options);
        Assert.AreEqual("Standard", drafts[0].Name);
        Assert.AreSame(legacy.Options.SipAccount, drafts[0].Account);
        CollectionAssert.AreEqual(bytes, await File.ReadAllBytesAsync(files.Location.FilePath));
        await files.Store.SaveAsync(SipProfileCatalog.Select(legacy.Options, drafts, drafts[0].Id), legacy);
        var saved = await files.Store.LoadAsync();
        Assert.AreEqual(13, saved.FormatVersion);
        using var secret = files.Protector.Unprotect(saved.Options.SipAccount!.Password!, SipAccountOptions.PasswordPurpose);
        Assert.AreEqual("synthetic-profile-secret-0", secret.Reveal());
        var empty = SipProfileCatalog.Read(new());
        var unconfigured = SipProfileCatalog.Select(new(), empty, empty[0].Id);
        Assert.IsNull(unconfigured.SipAccount);
        Assert.IsNull(unconfigured.ActiveSipProfileId);
        Assert.IsTrue(unconfigured.SipProfiles.IsEmpty);
        new ApplicationOptionsValidator(files.Protector).Validate(unconfigured);
    }

    [TestMethod]
    public async Task PortableExportOmitsAllProfilesAndImportPreservesLocalAccountsAndSelection()
    {
        using var files = new Files();
        var profiles = files.Profiles();
        await files.Store.SaveAsync(SipProfileCatalog.Select(new(), profiles, profiles[1].Id));
        var export = Path.Combine(files.Root, "portable.json");
        await files.Store.ExportPortableAsync(export);
        var json = await File.ReadAllTextAsync(export);
        foreach (var forbidden in new[] { "sipProfiles", "sipAccount", "activeSipProfileId", "first.invalid", "second.invalid", "synthetic-profile-secret", "Testprofil" })
            Assert.DoesNotContain(forbidden, json);
        var before = await files.Store.LoadAsync();
        var after = await files.Store.ImportPortableAsync(export, before);
        Assert.AreEqual(before.Options.ActiveSipProfileId, after.Options.ActiveSipProfileId);
        using var first = files.Protector.Unprotect(after.Options.SipProfiles[0].Account!.Password!, SipAccountOptions.PasswordPurpose);
        using var second = files.Protector.Unprotect(after.Options.SipAccount!.Password!, SipAccountOptions.PasswordPurpose);
        Assert.AreEqual("synthetic-profile-secret-0", first.Reveal());
        Assert.AreEqual("synthetic-profile-secret-1", second.Reveal());
    }

    [TestMethod]
    public async Task InvalidProfileCatalogsCannotReplaceSavedSettingsAndStaleEditorsCannotOverwrite()
    {
        using var files = new Files();
        var profiles = files.Profiles();
        var valid = SipProfileCatalog.Select(new(), profiles, profiles[0].Id);
        await files.Store.SaveAsync(valid);
        var snapshot = await files.Store.LoadAsync();
        var invalid = new[]
        {
            valid with { ActiveSipProfileId = null },
            valid with { ActiveSipProfileId = Guid.NewGuid() },
            valid with { SipAccount = null },
            valid with { SipProfiles = default },
            valid with { SipProfiles = [] },
            valid with { SipProfiles = [valid.SipProfiles[0], valid.SipProfiles[0]] },
            valid with { SipProfiles = valid.SipProfiles.SetItem(1, valid.SipProfiles[1] with { Name = "testPROFIL 1" }) },
            valid with { SipProfiles = valid.SipProfiles.SetItem(1, valid.SipProfiles[1] with { Name = " " }) },
            valid with { SipProfiles = valid.SipProfiles.SetItem(1, valid.SipProfiles[1] with { Name = new string('a', 81) }) },
            valid with { SipProfiles = valid.SipProfiles.SetItem(1, valid.SipProfiles[1] with { Name = "a\nb" }) },
            valid with { SipProfiles = valid.SipProfiles.SetItem(1, valid.SipProfiles[1] with { Id = Guid.Empty }) },
            valid with { SipProfiles = valid.SipProfiles.SetItem(1, valid.SipProfiles[1] with { Account = null }) },
            valid with { SipProfiles = valid.SipProfiles.SetItem(0, profiles[0]) },
            valid with { SipProfiles = valid.SipProfiles.SetItem(1, valid.SipProfiles[1] with { Account = new() { Server = "invalid uri", Username = "test" } }) },
            valid with { SipProfiles = Enumerable.Range(0, 17).Select(i => new SipAccountProfile(Guid.NewGuid(), "P" + i, profiles[0].Account)).ToImmutableArray() }
        };
        foreach (var options in invalid)
        {
            await Assert.ThrowsExactlyAsync<ConfigurationException>(() => files.Store.SaveAsync(options, snapshot));
            Assert.AreEqual(snapshot.Options.ActiveSipProfileId, (await files.Store.LoadAsync()).Options.ActiveSipProfileId);
        }
        await files.Store.SaveAsync(SipProfileCatalog.Select(valid, profiles, profiles[1].Id), snapshot);
        await Assert.ThrowsExactlyAsync<ConfigurationException>(() => files.Store.SaveAsync(valid, snapshot));
        Assert.AreEqual(profiles[1].Id, (await files.Store.LoadAsync()).Options.ActiveSipProfileId);
    }

    private sealed class Files : IDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "WawiCallPro.SipProfiles-" + Guid.NewGuid().ToString("N"));
        internal WindowsUserSecretProtector Protector { get; } = new();
        internal ConfigurationLocation Location { get; }
        internal ConfigurationStore Store { get; }
        internal Files()
        {
            Location = new(Root);
            Store = new(Location, new(Protector), null);
        }
        internal ImmutableArray<SipAccountProfile> Profiles() => Enumerable.Range(0, 2).Select(index =>
        {
            var account = new SipAccountOptions
            {
                Server = index == 0 ? "first.invalid" : "second.invalid", Username = "test" + index,
                Password = Protector.Protect("synthetic-profile-secret-" + index, SipAccountOptions.PasswordPurpose)
            };
            return new SipAccountProfile(Guid.NewGuid(), "Testprofil " + (index + 1), account,
                CallerLineConfiguration.Create(account, new(LexwareProductLine.Pro, "fixture", index.ToString())));
        }).ToImmutableArray();
        public void Dispose()
        {
            var resolved = Path.GetFullPath(Root);
            if (!resolved.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase)
                || !Path.GetFileName(resolved).StartsWith("WawiCallPro.SipProfiles-", StringComparison.Ordinal))
                throw new InvalidOperationException("Unexpected fixture path.");
            if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
        }
    }
}

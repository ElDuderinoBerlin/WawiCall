using System.Collections.Immutable;
using WawiCallPro.Core.Parties;
using WawiCallPro.Database;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Database.Repositories;
using WawiCallPro.Interfaces.Capabilities;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Office;
using WawiCallPro.Interfaces.Parties;

namespace WawiCallPro.Office.Tests;

public sealed partial class MailCacheTests
{
    [TestMethod]
    [DataRow(true, PartyKind.Customer, LexwareProductLine.Pro)]
    [DataRow(false, PartyKind.Customer, LexwareProductLine.Pro)]
    [DataRow(true, PartyKind.Supplier, LexwareProductLine.Pro)]
    [DataRow(false, PartyKind.Supplier, LexwareProductLine.Pro)]
    [DataRow(true, PartyKind.Customer, LexwareProductLine.FinOffice)]
    [DataRow(false, PartyKind.Customer, LexwareProductLine.FinOffice)]
    [DataRow(true, PartyKind.Supplier, LexwareProductLine.FinOffice)]
    [DataRow(false, PartyKind.Supplier, LexwareProductLine.FinOffice)]
    public async Task IncomingContactMobileSelectsTheOwnerAndFindsSecondMailFieldsInBothModes(bool cached, PartyKind role, LexwareProductLine line)
    {
        await using var scope = await ScopedFixture.Create(cached, role, line); var fixture = scope.Mail;
        if (cached) await fixture.Cache.SynchronizeAsync(fixture.Account.Id);
        var match = new CompanyContactIndex(scope.Owner.Key.Company, scope.Rows).FindPhone("+491711234567").Single();
        Assert.AreEqual(scope.Owner.Key, match.Owner); Assert.AreEqual(scope.Contact.Key, match.SourceParty);
        scope.Directory.Unavailable = true;
        foreach (var selected in new[] { scope.Owner, scope.Contact })
        {
            var source = scope.Application.ForParty(selected);
            var catalog = await source.GetFoldersAsync(); Assert.IsFalse(catalog.Folders.IsEmpty);
            Assert.Contains((role == PartyKind.Customer ? "Kunde " : "Lieferant ") + "00042", catalog.Status);
            var list = await source.GetMessagesAsync(fixture.Folder, ""); Assert.AreEqual(1, list.Length);
            Assert.AreEqual("private body", (await source.GetMessageAsync(list[0].Key))!.Text);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, await scope.Application.DownloadForSourceAsync(source, list[0].Key, "file", default));
        }
        Assert.IsTrue((await scope.Application.ForParty(scope.Unrelated).GetMessagesAsync(fixture.Folder, "")).IsEmpty);
        Assert.AreEqual(0, scope.Directory.Reads); Assert.AreEqual(cached, File.Exists(fixture.CachePath));
    }

    [TestMethod]
    [DataRow("to")]
    [DataRow("cc")]
    [DataRow("bcc")]
    public async Task OutgoingRecipientsMatchTheCompleteOwnerGroup(string kind)
    {
        await using var scope = await ScopedFixture.Create(true, PartyKind.Customer, LexwareProductLine.Pro);
        scope.Mail.Provider.Message = scope.Mail.Message with { SenderAddress = "our-mailbox@example.test", Recipients = [new("customer@example.test", "Synthetic", kind)] };
        await scope.Mail.Cache.SynchronizeAsync(scope.Mail.Account.Id);
        Assert.AreEqual(1, (await scope.Application.ForParty(scope.Contact).GetMessagesAsync(scope.Mail.Folder, "")).Length);
    }

    [TestMethod]
    public async Task InvalidMailPartsAreReportedWithoutReturningTheirPrivateValuesOrLosingValidMatches()
    {
        await using var scope = await ScopedFixture.Create(false, PartyKind.Customer, LexwareProductLine.Pro);
        scope.Directory.Rows = [scope.Owner, scope.Unrelated, scope.Contact with
        { Emails = [new("customer@example.test;broken-private-value", new("contact", "native-contact", "email2"))] }];
        var source = scope.Application.ForParty(scope.Contact); var catalog = await source.GetFoldersAsync();
        Assert.Contains("1 ungültige Lexware-Mailfelder", catalog.Status); Assert.DoesNotContain("broken-private-value", catalog.Status);
        Assert.AreEqual(1, (await source.GetMessagesAsync(scope.Mail.Folder, "")).Length);
    }
    [TestMethod]
    public async Task AnOpenFolderDoesNotMixMailFromDifferentOwnerGroupVersions()
    {
        await using var scope = await ScopedFixture.Create(false, PartyKind.Supplier, LexwareProductLine.FinOffice);
        scope.Directory.Unavailable = true; var source = scope.Application.ForParty(scope.Contact); await source.GetFoldersAsync();
        await scope.Replace([scope.Owner, scope.Unrelated, scope.Contact with { Emails = [] }]);
        var error = await Assert.ThrowsExactlyAsync<MailException>(() => source.GetMessagesAsync(scope.Mail.Folder, ""));
        Assert.AreEqual(MailFailure.Conflict, error.Failure);
    }
    [TestMethod]
    public async Task LargeContactGroupsUseBoundedAddressBatchesAndDoNotDuplicateMailOrItsLimitCount()
    {
        await using var scope = await ScopedFixture.Create(true, PartyKind.Customer, LexwareProductLine.Pro);
        var extra = Enumerable.Range(0, 1100).Select(index => scope.Contact with
        {
            Key = new(scope.Contact.Key.Company, scope.Contact.Key.Kind, "extra-" + index, scope.Owner.Key.ExternalId),
            Emails = [new PartyEmail("other-" + index + "@example.test", new("contact", "extra-" + index, "email1")),
                new PartyEmail("sibling-" + index + "@example.test", new("contact", "extra-" + index, "email2"))]
        }).ToImmutableArray();
        await scope.Replace(scope.Rows.AddRange(extra)); scope.Directory.Unavailable = true;
        var current = await scope.Mail.Settings.LoadAsync();
        await scope.Mail.Settings.SaveAsync(current.Options with { MailAccounts = [current.Options.MailAccounts[0] with { MaximumMessages = 1 }] }, current);
        scope.Mail.Provider.Message = scope.Mail.Message with { Recipients = [new("sibling-999@example.test", "", "to")] };
        await scope.Mail.Cache.SynchronizeAsync(scope.Mail.Account.Id);
        var source = scope.Application.ForParty(scope.Owner); await source.GetFoldersAsync();
        Assert.AreEqual(1, (await source.GetMessagesAsync(scope.Mail.Folder, "")).Length);
    }
    [TestMethod]
    public async Task OpeningMailUsesCurrentLexwareContactFieldsEvenWhenThePhoneCacheHasAnOlderCompleteSnapshot()
    {
        await using var scope = await ScopedFixture.Create(false, PartyKind.Customer, LexwareProductLine.Pro);
        scope.Directory.Rows = [scope.Owner, scope.Unrelated, scope.Contact with { Emails = [] }];
        var source = scope.Application.ForParty(scope.Contact); await source.GetFoldersAsync();
        Assert.IsTrue((await source.GetMessagesAsync(scope.Mail.Folder, "")).IsEmpty); Assert.IsTrue(scope.Directory.Reads > 0);
    }
    [TestMethod]
    public async Task PartialLegacyCacheUsesFreshLexwareFieldsAndDoesNotTreatMissingSecondMailsAsComplete()
    {
        await using var scope = await ScopedFixture.Create(false, PartyKind.Customer, LexwareProductLine.Pro);
        await scope.Replace(scope.Rows.Select(party => party with { ContactFieldsVersion = 0, Emails = [] }).ToImmutableArray());
        var source = scope.Application.ForParty(scope.Contact);
        Assert.AreEqual(1, (await source.GetMessagesAsync(scope.Mail.Folder, "")).Length); Assert.IsTrue(scope.Directory.Reads > 0);
    }

    [TestMethod]
    public async Task RefreshAndContactReassignmentInvalidateTheOldOwnerAndContactScopes()
    {
        await using var scope = await ScopedFixture.Create(true, PartyKind.Customer, LexwareProductLine.FinOffice);
        await scope.Mail.Cache.SynchronizeAsync(scope.Mail.Account.Id);
        var previous = scope.Application.ForParty(scope.Contact); Assert.AreEqual(1, (await previous.GetMessagesAsync(scope.Mail.Folder, "")).Length);
        var moved = scope.Contact with { Key = new(scope.Contact.Key.Company, scope.Contact.Key.Kind, scope.Contact.Key.ExternalId, scope.Unrelated.Key.ExternalId) };
        await scope.Replace([scope.Owner, scope.Unrelated, moved]);
        Assert.IsTrue((await previous.GetMessagesAsync(scope.Mail.Folder, "")).IsEmpty); Assert.IsNull(await previous.GetMessageAsync(scope.Mail.Message.Content.Key));
        Assert.IsTrue((await scope.Application.ForParty(scope.Owner).GetMessagesAsync(scope.Mail.Folder, "")).IsEmpty);
        Assert.AreEqual(1, (await scope.Application.ForParty(scope.Unrelated).GetMessagesAsync(scope.Mail.Folder, "")).Length);
    }

    [TestMethod]
    public async Task LateProviderPreviewIsRejectedAfterTheContactAddressChanges()
    {
        await using var scope = await ScopedFixture.Create(false, PartyKind.Customer, LexwareProductLine.Pro);
        var source = scope.Application.ForParty(scope.Contact); await source.GetFoldersAsync();
        scope.Mail.Provider.BeforeRead = _ => scope.Replace([scope.Owner, scope.Unrelated, scope.Contact with { Emails = [] }]);
        var error = await Assert.ThrowsExactlyAsync<MailException>(() => source.GetMessageAsync(scope.Mail.Message.Content.Key));
        Assert.AreEqual(MailFailure.Conflict, error.Failure);
    }

    [TestMethod]
    public async Task DownloadIsRecheckedAgainstTheOwnerGroupAndRevokedBytesAreZeroed()
    {
        await using var scope = await ScopedFixture.Create(false, PartyKind.Supplier, LexwareProductLine.Pro);
        var source = scope.Application.ForParty(scope.Contact); await source.GetFoldersAsync();
        scope.Mail.Provider.BeforeAttachment = _ => scope.Replace([scope.Owner, scope.Unrelated, scope.Contact with { Emails = [] }]);
        await Assert.ThrowsExactlyAsync<MailException>(() => scope.Application.DownloadForSourceAsync(source, scope.Mail.Message.Content.Key, "file", default));
        Assert.IsNotNull(scope.Mail.Provider.LastAttachment); Assert.IsTrue(scope.Mail.Provider.LastAttachment.All(value => value == 0));
    }

    private sealed class ScopedFixture : IAsyncDisposable
    {
        internal Fixture Mail = null!;
        internal Party Owner = null!, Contact = null!, Unrelated = null!;
        internal ImmutableArray<Party> Rows;
        internal readonly DirectoryDouble Directory = new();
        internal DapperPartyCacheRepository Repository = null!;
        internal MailApplication Application = null!;
        internal static async Task<ScopedFixture> Create(bool cached, PartyKind role, LexwareProductLine line)
        {
            var scope = new ScopedFixture { Mail = await Fixture.Create(cached) };
            var company = new CompanyKey(line, "test", "company");
            var snapshot = await scope.Mail.Settings.LoadAsync();
            await scope.Mail.Settings.SaveAsync(snapshot.Options with { MailAccounts = [scope.Mail.Account with { Company = company }] }, snapshot);
            scope.Owner = new() { Key = new(company, role, "native-owner"), DisplayName = "Synthetic owner", Number = "00042", ContactFieldsVersion = 1,
                Emails = [new("main@example.test", new("owner", "native-owner", "email"))] };
            scope.Contact = new() { Key = new(company, role == PartyKind.Customer ? PartyKind.CustomerContact : PartyKind.SupplierContact, "native-contact", "native-owner"),
                DisplayName = "Synthetic contact", ContactFieldsVersion = 1,
                Emails = [new("first@example.test", new("contact", "native-contact", "email1")), new("customer@example.test", new("contact", "native-contact", "email2"))],
                Phones = [new("0171/1234567", PhoneKind.Mobile, new("contact", "native-contact", "mobile"), "+491711234567")] };
            scope.Unrelated = scope.Owner with { Key = new(company, role, "unrelated-owner"), Emails = [new("other@example.test", new("owner", "unrelated-owner", "email"))] };
            scope.Rows = [scope.Owner, scope.Contact, scope.Unrelated]; scope.Directory.Rows = scope.Rows;
            var target = DatabaseTarget.Sqlite(Path.Combine(scope.Mail.Root, "lexware-scope.db"));
            await new DatabaseProvisioner(new()).CreateAsync(target); await new DatabaseMigrator(new(), new FluentMigratorEngine(DatabaseType.Sqlite)).MigrateAsync(target);
            var context = new DatabaseRepositoryContext(target, new());
            await new DapperCompanyRepository(context).WriteAsync(new(new(company, "Synthetic"), null, DateTimeOffset.UtcNow));
            scope.Repository = new(context); await scope.Replace(scope.Rows);
            var resolver = new PartyMailScopeResolver(scope.Repository, scope.Directory, TimeProvider.System);
            scope.Application = new(scope.Mail.AccountService, scope.Mail.Cache, scope.Mail.Cache, scope.Mail.Factory, resolver);
            return scope;
        }
        internal async Task Replace(ImmutableArray<Party> rows)
        {
            var state = await Repository.GetStateAsync(Owner.Key.Company);
            await Repository.ReplaceAsync(new(Owner.Key.Company, state.Revision, rows, DateTimeOffset.UtcNow));
        }
        public ValueTask DisposeAsync() => Mail.DisposeAsync();
    }
    private sealed class DirectoryDouble : IBusinessDirectory
    {
        internal ImmutableArray<Party> Rows; internal bool Unavailable; internal int Reads;
        public Task<ImmutableArray<Company>> GetCompaniesAsync(CancellationToken cancellationToken = default) => throw new AssertFailedException();
        public Task<CapabilitySet> GetCapabilitiesAsync(CompanyKey company, CancellationToken cancellationToken = default) => throw new AssertFailedException();
        public Task<Party?> GetPartyAsync(PartyKey key, CancellationToken cancellationToken = default) => throw new AssertFailedException();
        public Task<PartyPage> GetPartiesAsync(PartyQuery query, CancellationToken cancellationToken = default)
        {
            if (Unavailable) throw new InvalidOperationException("synthetic-source-unavailable"); Reads++;
            var rows = Rows.Where(party => party.Key.Company == query.Company).Skip(query.Page.Offset).Take(query.Page.Limit + 1).ToArray();
            return Task.FromResult(new PartyPage(query.Company, new(rows.Take(query.Page.Limit).ToImmutableArray(), rows.Length > query.Page.Limit)));
        }
    }
}

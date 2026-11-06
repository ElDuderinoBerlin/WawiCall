using System.Collections.Immutable;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Database.Repositories;
using WawiCallPro.Interfaces.Common;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Interfaces.PhoneNumbers;
using WawiCallPro.Interfaces.Telephony;

namespace WawiCallPro.Database.Tests;

[TestClass]
public sealed class RepositoryIntegrationTests
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WawiCallPro-repositories-" + Guid.NewGuid().ToString("N"));
    [TestCleanup] public void Cleanup() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

    [TestMethod] public async Task CompanyAndCacheRoundTripUnicodeExactKeysPagingAndVersionConflicts() => await RepositoryAcceptance.CompaniesAndCacheAsync(await CreateAsync());
    [TestMethod] public async Task CallHistoryRetainsCandidatesNotesRecordingsAndImmutableEvents() => await RepositoryAcceptance.HistoryAsync(await CreateAsync());
    [TestMethod] public async Task MatchingRulesIncrementAggregateRevisionAndSurviveCacheReplacement() => await RepositoryAcceptance.RulesAsync(await CreateAsync());
    [TestMethod] public async Task ParallelWritersHaveOneWinnerAndCancellationLeavesNoPartialCache() => await RepositoryAcceptance.ConcurrencyAsync(await CreateAsync());
    [TestMethod] public async Task BatchedTransitionsWithIdenticalTimesAndReplayedEventsFollowDomainRules() => await RepositoryAcceptance.BatchedTransitionsAsync(await CreateAsync());

    [TestMethod]
    public async Task SourceHashMismatchAndReadOnlyFilesAreHandledWithoutProviderDetails()
    {
        var target = await CreateAsync();
        var context = new DatabaseRepositoryContext(target, new()); var repository = new DapperCompanyRepository(context);
        var key = new CompanyKey(LexwareProductLine.Pro, "local", "0001");
        await repository.WriteAsync(new(new(key, "Ä"), null, RepositoryAcceptance.Time));
        File.SetAttributes(target.SqlitePath!, FileAttributes.ReadOnly);
        File.SetAttributes(target.SqlitePath! + ".migration.lock", FileAttributes.ReadOnly);
        try
        {
            var page = await repository.GetPageAsync(new(new(0, 10)));
            Assert.AreEqual(1, page.Items.Length);
            Assert.AreEqual(key, page.Items[0].Company.Key);
        }
        finally { File.SetAttributes(target.SqlitePath!, FileAttributes.Normal); File.SetAttributes(target.SqlitePath! + ".migration.lock", FileAttributes.Normal); }
        await using (var db = new DatabaseFactory().CreateConnection(target))
        {
            await db.OpenAsync(); using var command = db.CreateCommand(); command.CommandText = "UPDATE companies SET externalid='0002'"; await command.ExecuteNonQueryAsync();
        }
        var error = await Assert.ThrowsExactlyAsync<RepositoryConflictException>(() => repository.GetAsync(key));
        Assert.AreEqual(RepositoryConflictKind.IdentityCollision, error.Kind);
        await Assert.ThrowsExactlyAsync<RepositoryConflictException>(() => repository.GetPageAsync(new(new(0, 10))));
    }

    [TestMethod]
    public async Task CacheFailureRollsBackAllRowsAndProvenance()
    {
        var target = await CreateAsync(); var context = new DatabaseRepositoryContext(target, new());
        var companies = new DapperCompanyRepository(context); var cache = new DapperPartyCacheRepository(context);
        var key = new CompanyKey(LexwareProductLine.Pro, "i", "c");
        await companies.WriteAsync(new(new(key, "Company"), null, RepositoryAcceptance.Time));
        var original = new Party { Key = new(key, PartyKind.Customer, "a"), DisplayName = "Original" };
        await cache.ReplaceAsync(new(key, 0, [original], RepositoryAcceptance.Time, "parser1", "context1"));
        await using (var db = new DatabaseFactory().CreateConnection(target))
        {
            await db.OpenAsync(); using var command = db.CreateCommand();
            command.CommandText = "CREATE TRIGGER fail_cache BEFORE INSERT ON parties WHEN NEW.externalid='bad' BEGIN SELECT RAISE(ABORT,'private-database-secret'); END";
            await command.ExecuteNonQueryAsync();
        }
        var failed = await Assert.ThrowsExactlyAsync<DatabaseOperationException>(() => cache.ReplaceAsync(new(key, 1, [original with { DisplayName = "Changed" }, new Party { Key = new(key, PartyKind.Customer, "bad"), DisplayName = "Bad" }], RepositoryAcceptance.Time.AddTicks(1), "parser2", "context2")));
        Assert.IsFalse(failed.ToString().Contains("private-database-secret", StringComparison.Ordinal));
        Assert.AreEqual("Original", (await cache.GetAsync(original.Key))!.DisplayName);
        Assert.AreEqual("parser1", (await cache.GetStateAsync(key)).ParserVersion);
        Assert.IsNull(await cache.GetAsync(new(key, PartyKind.Customer, "bad")));
    }

    [TestMethod]
    public async Task AddressAndPartyProvenanceCountryTextAndDistinctPhoneSourcesSurviveCacheReload()
    {
        var target = await CreateAsync();
        var context = new DatabaseRepositoryContext(target, new());
        var company = new CompanyKey(LexwareProductLine.Pro, "address-fixture", "1");
        await new DapperCompanyRepository(context).WriteAsync(new(new(company, "Firma"), null, RepositoryAcceptance.Time));
        var phone = new PartyPhone(" +49 (0)351 / 12-34 ", PhoneKind.Telephone, new SourceField("fk_kunde", "1", "invoiceAddress.telephone"));
        var second = phone with { Source = new SourceField("fk_kunde", "1", "invoiceAddress.telephone2") };
        var address = new PartyAddress
        {
            ExternalId = "1/invoiceAddress", Kind = AddressKind.Billing,
            Source = new SourceField("fk_kunde", "1", "invoiceAddress"), Street = "Hauptstraße", HouseNumber = "3a",
            AddressLine2 = "Haus B", PostalCode = "01234", Country = "Deutschland", CountryCode = "DE", Phones = [phone, second]
        };
        var party = new Party
        {
            Key = new PartyKey(company, PartyKind.Customer, "1"), DisplayName = "Kunde", Source = new SourceRecord("fk_kunde", "1"),
            Addresses = [address]
        };
        var cache = new DapperPartyCacheRepository(context);
        await cache.ReplaceAsync(new(company, 0, [party], RepositoryAcceptance.Time));
        var reloaded = (await cache.GetAsync(party.Key))!;
        Assert.AreEqual(party.Source, reloaded.Source);
        Assert.AreEqual(address.Source, reloaded.Addresses.Single().Source);
        Assert.AreEqual("Deutschland", reloaded.Addresses[0].Country);
        Assert.AreEqual("01234", reloaded.Addresses[0].PostalCode);
        Assert.AreEqual("3a", reloaded.Addresses[0].HouseNumber);
        CollectionAssert.AreEqual(new[] { phone, second }, reloaded.Addresses[0].Phones.ToArray());
        await cache.ReplaceAsync(new(company, 1, [party with { Source = null, Addresses = [address with { Source = null, Country = null }] }], RepositoryAcceptance.Time.AddTicks(1)));
        reloaded = (await cache.GetAsync(party.Key))!;
        Assert.IsNull(reloaded.Source);
        Assert.IsNull(reloaded.Addresses[0].Source);
        Assert.IsNull(reloaded.Addresses[0].Country);
    }

    [TestMethod]
    public async Task PartialStoredAddressSourceIsRejectedInsteadOfLosingItsProvenance()
    {
        var target = await CreateAsync();
        var context = new DatabaseRepositoryContext(target, new());
        var company = new CompanyKey(LexwareProductLine.FinOffice, "address-fixture", "1");
        await new DapperCompanyRepository(context).WriteAsync(new(new(company, "Firma"), null, RepositoryAcceptance.Time));
        var party = new Party { Key = new(company, PartyKind.Customer, "1"), DisplayName = "Kunde",
            Addresses = [new PartyAddress { ExternalId = "1/main", Kind = AddressKind.Billing }] };
        var cache = new DapperPartyCacheRepository(context);
        await cache.ReplaceAsync(new(company, 0, [party], RepositoryAcceptance.Time));
        await using (var db = new DatabaseFactory().CreateConnection(target))
        {
            await db.OpenAsync();
            using var command = db.CreateCommand();
            command.CommandText = "UPDATE partyaddresses SET sourcerecordid='1'";
            await command.ExecuteNonQueryAsync();
        }
        await Assert.ThrowsExactlyAsync<DatabaseOperationException>(() => cache.GetAsync(party.Key));
    }

    private async Task<DatabaseTarget> CreateAsync()
    {
        var target = DatabaseTarget.Sqlite(Path.Combine(_root, "calls.db"));
        await new DatabaseProvisioner(new()).CreateAsync(target);
        await new DatabaseMigrator(new(), new FluentMigratorEngine(target.Type)).MigrateAsync(target);
        return target;
    }
}

/// <summary>The same assertions run on disposable native SQL Server, MySQL and PostgreSQL databases.</summary>
public static class RepositoryAcceptance
{
    public static readonly DateTimeOffset Time = new(638000000000000001, TimeSpan.Zero);
    private static CompanyKey Key(string suffix) => new(LexwareProductLine.Pro, "Inst Ä 电话 ' %_ ", suffix + Guid.NewGuid().ToString("N"));
    private static async Task<CompanyKey> RegisterAsync(DatabaseRepositoryContext context, string suffix)
    {
        var key = Key(suffix); await new DapperCompanyRepository(context).WriteAsync(new(new(key, "Mandant Ä 电话 '"), null, Time)); return key;
    }
    private static async Task Conflict(Func<Task> action, RepositoryConflictKind kind)
    { Assert.AreEqual(kind, (await Assert.ThrowsExactlyAsync<RepositoryConflictException>(action)).Kind); }

    public static async Task CompaniesAndCacheAsync(DatabaseTarget target)
    {
        var context = new DatabaseRepositoryContext(target, new()); var companies = new DapperCompanyRepository(context); var cache = new DapperPartyCacheRepository(context);
        var key = await RegisterAsync(context, "company"); var other = await RegisterAsync(context, "other");
        await Conflict(() => companies.WriteAsync(new(new(key, "overwrite"), null, Time)), RepositoryConflictKind.AlreadyExists);
        var updated = await companies.WriteAsync(new(new(key, "Renamed"), 1, Time.AddTicks(1)));
        Assert.AreEqual(2L, updated.Version); Assert.AreEqual(Time, updated.CreatedAtUtc);
        await Conflict(() => companies.WriteAsync(new(new(key, "stale"), 1, Time)), RepositoryConflictKind.VersionMismatch);
        var party = new Party
        {
            Key = new(key, PartyKind.Customer, "0001 "), DisplayName = "Ä 电话 ' %_[x]", Number = "00_42", Email = "mail@example.invalid", SourceUpdatedAtUtc = Time,
            Phones = [new("030 12345-0", PhoneKind.Telephone, new("customer", "0001 ", "phone"), "+4930123450", "00")],
            Addresses = [new() { ExternalId = " 001", Kind = AddressKind.Shipping, Street = "Straße ' 电话", HouseNumber = "02a", City = "München", CountryCode = "DE", Phones = [new("030123450", PhoneKind.Fax, new("address", " 001", "fax"), "+4930123450")] }]
        };
        var duplicateNumber = new Party { Key = new(key, PartyKind.Supplier, "0001 "), DisplayName = "Other", Phones = party.Phones };
        var state = await cache.ReplaceAsync(new(key, 0, [party, duplicateNumber], Time, "parser1", "DE/rules1"));
        Assert.AreEqual(1L, state.Revision); Assert.AreEqual("DE/rules1", (await cache.GetStateAsync(key)).ContextVersion);
        var stored = (await cache.GetAsync(party.Key))!;
        Assert.AreEqual(party.DisplayName, stored.DisplayName); Assert.AreEqual(party.Addresses[0], stored.Addresses[0] with { Phones = party.Addresses[0].Phones });
        Assert.AreEqual("00", stored.Phones[0].Extension); Assert.AreEqual(Time, stored.SourceUpdatedAtUtc);
        Assert.IsNull(await cache.GetAsync(new(key, PartyKind.Customer, "0001")));
        var matches = await cache.FindByE164Async(key, "+4930123450", new(0, 1)); Assert.AreEqual(1, matches.Page.Items.Length); Assert.IsTrue(matches.Page.HasMore);
        Assert.AreEqual(2, (await cache.FindByE164Async(key, "+4930123450", new(0, 10))).Page.Items.Length);
        Assert.AreEqual(0, (await cache.FindByE164Async(other, "+4930123450", new(0, 10))).Page.Items.Length);
        Assert.AreEqual(1, (await cache.GetPageAsync(new(key, new(0, 10), "%_[x]"))).Page.Items.Length);
        await Conflict(() => cache.ReplaceAsync(new(key, 0, [], Time)), RepositoryConflictKind.VersionMismatch);
        var log = new NumberParseFailure(Guid.NewGuid(), party.Key, new("customer", "0001 ", "phone"), PhoneKind.Telephone, "bad ' ", "invalid", Time);
        var errors = new DapperNumberParseLogRepository(context); await errors.AppendAsync(log); await errors.AppendAsync(log);
        await Conflict(() => errors.AppendAsync(new(log.Id, party.Key, log.Source, log.Kind, "changed", log.Code, Time)), RepositoryConflictKind.AppendConflict);
        await cache.ReplaceAsync(new(key, 1, [], Time.AddTicks(1), "parser2", "context2"));
        Assert.IsNull(await cache.GetAsync(party.Key)); Assert.AreEqual(1, (await errors.GetPageAsync(new(key, new(0, 10), Time, Time.AddTicks(1)))).Items.Length);
        Assert.AreEqual(2L, (await companies.GetAsync(key))!.Version);
        Assert.IsTrue((await companies.GetPageAsync(new(new(0, 1)))).HasMore);
    }

    public static async Task HistoryAsync(DatabaseTarget target)
    {
        var context = new DatabaseRepositoryContext(target, new()); var calls = new DapperCallRepository(context);
        var key = await RegisterAsync(context, "history"); var other = await RegisterAsync(context, "alternative");
        var origin = new CallOrigin(Guid.NewGuid(), "PC-Ä", "DOMAIN\\Nutzer ");
        var call = new CallSnapshot { Id = Guid.NewGuid(), Revision = 0, State = CallState.Ringing, Direction = CallDirection.Incoming, StartedAtUtc = Time, StateChangedAtUtc = Time,
            LocalAddress = new(NumberPresentation.Available, "sip:intern"), RemoteAddress = new(NumberPresentation.Available, "030 123450", "+4930123450") };
        var candidate = new CallPartySnapshot(new(key, PartyKind.Customer, "0001"), "Historisch Ä 电话", "0042", false)
        { CompanyDisplayName = "Firma damals", Match = new() { Kind = PhoneNumberMatchKind.Exact, ParserVersion = "test1", CacheRevision = 1, MatchedE164 = "+4930123450" } };
        var alternative = new CallPartySnapshot(new(other, PartyKind.Supplier, "0001"), "Alternative", "0042", false);
        var lookup = new CallerLookupSnapshot { SearchCompanies = [new(key, 1, 0, true, Time, "test1"), new(other, 0, 0, false, null, null)], ParserVersion = "test1", SearchedAtUtc = Time, IsComplete = false };
        var entry = new CallEvent(Guid.NewGuid(), call.Id, 10, Time, "call.ringing", CallState.Ringing);
        var saved = await calls.WriteAsync(new(call, null, [candidate, alternative], [entry], origin, lookup));
        Assert.AreEqual(1L, saved.Version); Assert.AreEqual(origin, saved.History.Call.Origin); Assert.AreEqual(candidate, saved.History.Parties[0]);
        var notes = new DapperCallNoteRepository(context); var noteId = Guid.NewGuid();
        var note = await notes.WriteAsync(new(call.Id, noteId, "Notiz\r\nÄ 电话 ' ", null, Time));
        var recording = new Recording(Guid.NewGuid(), call.Id, "2026/01/anruf.mp3", "mp3", TimeSpan.FromTicks(123456789), RecordingAvailability.Missing);
        var records = new DapperRecordingRepository(context); await records.WriteAsync(new(recording, null, Time));
        saved = await calls.WriteAsync(new(call with { Company = other }, 1, [alternative with { IsSelected = true }], [entry], origin));
        Assert.AreEqual(2, saved.History.Parties.Length); Assert.AreEqual(candidate, saved.History.Parties[0]); Assert.IsTrue(saved.History.Parties[1].IsSelected);
        Assert.AreEqual(note.Note, saved.History.Notes.Single()); Assert.AreEqual(recording, saved.History.Recordings.Single());
        Assert.AreEqual(lookup.ParserVersion, saved.History.Lookup!.ParserVersion);
        await Conflict(() => calls.WriteAsync(new(call with { Company = other }, 2, [candidate with { DisplayName = "Rewrite" }], [], origin)), RepositoryConflictKind.AppendConflict);
        await Conflict(() => calls.WriteAsync(new(call with { StateChangedAtUtc = Time.AddTicks(1) }, 2, [], [], origin)), RepositoryConflictKind.SnapshotMismatch);
        var connected = call with { Company = other, Revision = 5, State = CallState.Connected, AnsweredAtUtc = Time.AddTicks(1), StateChangedAtUtc = Time.AddTicks(1) };
        saved = await calls.WriteAsync(new(connected, 2, [alternative with { IsSelected = true }], [new(Guid.NewGuid(), call.Id, 11, Time.AddTicks(1), "call.connected", CallState.Connected)], origin));
        Assert.AreEqual(5L, saved.History.Call.Revision);
        await Conflict(() => calls.WriteAsync(new(connected, 3, [], [entry with { Code = "changed" }], origin)), RepositoryConflictKind.AppendConflict);
        Assert.AreEqual(3L, (await calls.GetAsync(call.Id))!.Version);
        await Conflict(() => notes.WriteAsync(new(call.Id, noteId, "stale", null, Time)), RepositoryConflictKind.AlreadyExists);
        await notes.WriteAsync(new(call.Id, noteId, "Edited", 1, Time.AddTicks(2)));
        await Conflict(() => notes.WriteAsync(new(call.Id, noteId, "stale", 1, Time.AddTicks(3))), RepositoryConflictKind.VersionMismatch);
        Assert.AreEqual(Time, (await notes.GetAsync(noteId))!.Note.CreatedAtUtc);
        Assert.AreEqual(1, (await calls.GetPageAsync(new(new(0, 10), Party: candidate.Party) { WorkstationId = origin.WorkstationId, MachineName = "PC-Ä", WindowsUserName = "DOMAIN\\Nutzer " })).Items.Length);
        Assert.AreEqual(0, (await calls.GetPageAsync(new(new(0, 10)) { MachineName = "pc-ä" })).Items.Length);
        Assert.AreEqual(0, (await calls.GetPageAsync(new(new(0, 10)) { WindowsUserName = "DOMAIN\\Nutzer" })).Items.Length);
        Assert.AreEqual(0, (await calls.GetPageAsync(new(new(0, 10), StartedToUtc: Time))).Items.Length);
        var ended = connected with { Revision = 6, State = CallState.Ended, StateChangedAtUtc = Time.AddSeconds(3), EndedAtUtc = Time.AddSeconds(3), EndReason = CallEndReason.Completed };
        await calls.WriteAsync(new(ended, 3, [], [], origin));
        await Conflict(() => calls.WriteAsync(new(connected with { Revision = 7 }, 4, [], [], origin)), RepositoryConflictKind.SnapshotMismatch);
        await records.WriteAsync(new(recording with { Availability = RecordingAvailability.Failed }, 1, Time.AddTicks(2)));
        Assert.AreEqual(RecordingAvailability.Failed, (await calls.GetAsync(call.Id))!.History.Recordings.Single().Availability);
    }

    public static async Task RulesAsync(DatabaseTarget target)
    {
        var context = new DatabaseRepositoryContext(target, new()); var key = await RegisterAsync(context, "rules");
        var repository = new DapperPhoneMatchingRepository(context);
        var rule = new PhoneNumberBlockRule { Id = Guid.NewGuid(), Organisation = new(key, PartyKind.Customer, "0001"), Kind = PhoneNumberBlockKind.Confirmed, CountryCallingCode = 49, DestinationCode = "30", Stem = "12345", MaximumSuffixDigits = 4, Evidence = "Explizit Ä 电话" };
        await repository.WriteRuleAsync(new(rule, null, Time));
        var settings = (await repository.GetSettingsAsync(key))!; Assert.AreEqual(1L, settings.RuleRevision); Assert.IsFalse(settings.Settings.StemMatchingEnabled);
        settings = await repository.WriteSettingsAsync(new(new(key, true, 4), 1, Time.AddTicks(1))); Assert.AreEqual(2L, settings.RuleRevision);
        await repository.WriteRuleAsync(new(rule with { IsEnabled = false }, 1, Time.AddTicks(2)));
        settings = (await repository.GetSettingsAsync(key))!; Assert.AreEqual(2L, settings.Version); Assert.AreEqual(3L, settings.RuleRevision);
        await new DapperPartyCacheRepository(context).ReplaceAsync(new(key, 0, [], Time));
        Assert.AreEqual(rule with { IsEnabled = false }, (await repository.GetRuleAsync(rule.Id))!.Rule);
        await Conflict(() => repository.WriteRuleAsync(new(rule, 1, Time.AddTicks(2))), RepositoryConflictKind.VersionMismatch);
        Assert.AreEqual(1, (await repository.GetRulesAsync(key, new(0, 10))).Items.Length);
    }

    public static async Task ConcurrencyAsync(DatabaseTarget target)
    {
        var context = new DatabaseRepositoryContext(target, new()); var key = await RegisterAsync(context, "parallel");
        async Task<bool> Attempt(string name)
        {
            try { await new DapperCompanyRepository(new(target, new())).WriteAsync(new(new(key, name), 1, Time)); return true; }
            catch (RepositoryConflictException exception) when (exception.Kind == RepositoryConflictKind.VersionMismatch) { return false; }
        }
        var results = await Task.WhenAll(Attempt("one"), Attempt("two")); Assert.AreEqual(1, results.Count(value => value));
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => new DapperPartyCacheRepository(context).ReplaceAsync(new(key, 0, [], Time), canceled.Token));
        Assert.AreEqual(0L, (await new DapperPartyCacheRepository(context).GetStateAsync(key)).Revision);
    }

    public static async Task BatchedTransitionsAsync(DatabaseTarget target)
    {
        var calls = new DapperCallRepository(new(target, new())); var origin = new CallOrigin(Guid.NewGuid(), "BATCH-PC", "DOMAIN\\Batch");
        var call = new CallSnapshot { Id = Guid.NewGuid(), Revision = 0, State = CallState.Ringing, Direction = CallDirection.Incoming, LocalAddress = new(NumberPresentation.Unknown), RemoteAddress = new(NumberPresentation.Unknown), StartedAtUtc = Time, StateChangedAtUtc = Time };
        await calls.WriteAsync(new(call, null, [], [], origin));
        var connected = new CallEvent(Guid.NewGuid(), call.Id, 100, Time.AddTicks(1), "call.connected", CallState.Connected);
        var held = new CallEvent(Guid.NewGuid(), call.Id, 200, Time.AddTicks(1), "call.held", CallState.Held);
        var heldCall = call with { Revision = 2, State = CallState.Held, AnsweredAtUtc = Time.AddTicks(1), StateChangedAtUtc = Time.AddTicks(1) };
        var saved = await calls.WriteAsync(new(heldCall, 1, [], [connected, held], origin));
        Assert.AreEqual(heldCall with { Origin = origin }, saved.History.Call);
        var resumed = new CallEvent(Guid.NewGuid(), call.Id, 300, Time.AddTicks(2), "call.resumed", CallState.Connected);
        saved = await calls.WriteAsync(new(heldCall with { Revision = 3, State = CallState.Connected, StateChangedAtUtc = Time.AddTicks(2) }, 2, [], [connected, held, resumed], origin));
        Assert.AreEqual(3, saved.History.Events.Length); Assert.AreEqual(3L, saved.History.Call.Revision);
        var second = call with { Id = Guid.NewGuid() }; await calls.WriteAsync(new(second, null, [], [], origin));
        var notes = new DapperCallNoteRepository(new(target, new())); var id = Guid.NewGuid(); await notes.WriteAsync(new(call.Id, id, "Owned", null, Time));
        await Conflict(() => notes.WriteAsync(new(second.Id, id, "Moved", 1, Time)), RepositoryConflictKind.OwnershipMismatch);
        var recording = new Recording(Guid.NewGuid(), call.Id, "recordings/missing.wav", "wav", null, RecordingAvailability.Pending);
        var recordings = new DapperRecordingRepository(new(target, new())); await recordings.WriteAsync(new(recording, null, Time));
        await Conflict(() => recordings.WriteAsync(new(recording with { CallId = second.Id }, 1, Time)), RepositoryConflictKind.OwnershipMismatch);
    }
}

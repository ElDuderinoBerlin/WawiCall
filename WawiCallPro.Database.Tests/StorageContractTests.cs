using System.Collections.Immutable;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Interfaces.Telephony;

namespace WawiCallPro.Database.Tests;

[TestClass]
public sealed class StorageContractTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 8, 30, 0, TimeSpan.Zero);
    private static CompanyKey Company() => new(LexwareProductLine.Pro, "Inst-Ä", "0007");
    private static PartyKey Customer(string id = "00042") => new(Company(), PartyKind.Customer, id);

    [TestMethod]
    public void IdentityEncodingMatchesIndependentVersionOneVectors()
    {
        // Independently calculated using Python hashlib/struct: signed big-endian UTF-8 byte lengths.
        var company = Company();
        var contact = new PartyKey(company, PartyKind.CustomerContact, "1", "00042");
        Assert.AreEqual("96455298e3fceadd39101df183b4df42011e75fc92e726f4086307bd64caec21", SourceIdentityHash.Company(company));
        Assert.AreEqual("536fc97fb0b643ddedd711bb8f09b2504aa906e49bbc953a744dafb9ac774159", SourceIdentityHash.Party(contact));
        Assert.AreEqual("ca4f45bfaba1206edbed565a7bde17846707e9e8b5e985ee16cc751f400f0b46", SourceIdentityHash.Address(contact, AddressKind.Shipping, "Adr-01"));
    }

    [TestMethod]
    public void IdentityHashesPreserveCaseWhitespaceUnicodeBoundariesAndOwnerScope()
    {
        CompanyKey[] companies =
        [
            new(LexwareProductLine.Pro, "ab", "c"), new(LexwareProductLine.Pro, "a", "bc"),
            new(LexwareProductLine.Pro, "a", "BC"), new(LexwareProductLine.Pro, "a", "bc "),
            new(LexwareProductLine.FinOffice, "a", "bc"), new(LexwareProductLine.Pro, "a", "00bc"),
            new(LexwareProductLine.Pro, "é", "1"), new(LexwareProductLine.Pro, "e\u0301", "1")
        ];
        Assert.HasCount(companies.Length, companies.Select(SourceIdentityHash.Company).ToHashSet());
        var ownerA = new PartyKey(Company(), PartyKind.CustomerContact, "1", "A");
        var ownerB = new PartyKey(Company(), PartyKind.CustomerContact, "1", "B");
        var supplier = new PartyKey(Company(), PartyKind.SupplierContact, "1", "A");
        Assert.HasCount(3, new[] { ownerA, ownerB, supplier }.Select(SourceIdentityHash.Party).ToHashSet());
    }

    [TestMethod]
    public void NonportableSourceValuesAreRejectedWithoutNormalizingOrEchoingThem()
    {
        foreach (var value in new[] { new string('x', 257), "private-sentinel\0", "private-sentinel\ud800" })
        {
            var failure = Assert.ThrowsExactly<ArgumentException>(() => SourceIdentityHash.Company(new CompanyKey(LexwareProductLine.Pro, value, "1")));
            Assert.DoesNotContain("private-sentinel", failure.Message);
        }
        Assert.AreNotEqual(SourceIdentityHash.Company(new CompanyKey(LexwareProductLine.Pro, "x", "1")),
            SourceIdentityHash.Company(new CompanyKey(LexwareProductLine.Pro, "x ", "1")));
    }

    [TestMethod]
    public void UtcStoragePreservesSingleTicksAndTheInstantAtNonzeroOffsets()
    {
        var values = new[] { DateTimeOffset.MinValue, Now.AddTicks(1), DateTimeOffset.MaxValue };
        foreach (var value in values) Assert.AreEqual(value, DatabaseUtc.Decode(DatabaseUtc.Encode(value)));
        Assert.AreEqual(Now.AddTicks(7), DatabaseUtc.Decode(DatabaseUtc.Encode(Now.AddTicks(7).ToOffset(TimeSpan.FromHours(5.5)))));
        Assert.AreEqual(TimeSpan.Zero, DatabaseUtc.Decode(DatabaseUtc.Encode(Now)).Offset);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => DatabaseUtc.Decode(-1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => DatabaseUtc.Decode(DateTimeOffset.MaxValue.Ticks + 1));
    }

    [TestMethod]
    public void TechnicalIdsAreNonemptyCanonicalAndHaveConsistentOrdinalOrdering()
    {
        var first = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
        Assert.AreEqual("00112233445566778899aabbccddeeff", DatabaseId.Encode(first));
        Assert.AreEqual(first, DatabaseId.Decode(DatabaseId.Encode(first)));
        Assert.ThrowsExactly<ArgumentException>(() => DatabaseId.Encode(Guid.Empty));
        Assert.ThrowsExactly<ArgumentException>(() => DatabaseId.Decode(first.ToString()));
        Assert.ThrowsExactly<ArgumentException>(() => DatabaseId.Decode(first.ToString("N").ToUpperInvariant()));
        Assert.IsTrue(string.CompareOrdinal(DatabaseId.Encode(first), DatabaseId.Encode(Guid.Parse("00112234-0000-0000-0000-000000000001"))) < 0);
    }

    [TestMethod]
    public void CacheReplacementIsExplicitlyCompleteAndRejectsCrossCompanyDuplicatesAndDefaultCollections()
    {
        var first = new Party { Key = Customer(), DisplayName = "Müller", Phones = [new("030 123", PhoneKind.Telephone, new("customer", "00042", "phone"))] };
        var empty = new PartyCacheReplacement(Company(), 0, [], Now);
        Assert.HasCount(0, empty.Parties);
        var full = new PartyCacheReplacement(Company(), 7, [first], Now);
        Assert.AreEqual(7L, full.ExpectedRevision);
        Assert.AreSame(first, full.Parties[0]);
        Assert.ThrowsExactly<ArgumentException>(() => new PartyCacheReplacement(Company(), 0, default, Now));
        Assert.ThrowsExactly<ArgumentException>(() => new PartyCacheReplacement(Company(), 0, [first, first], Now));
        Assert.ThrowsExactly<ArgumentException>(() => new PartyCacheReplacement(new(LexwareProductLine.FinOffice, "Inst-Ä", "0007"), 0, [first], Now));
        Assert.ThrowsExactly<ArgumentException>(() => new PartyCacheReplacement(Company(), 0, [first with { Addresses = default }], Now));
        Assert.ThrowsExactly<ArgumentException>(() => new PartyCacheReplacement(Company(), 0, [first], Now.ToOffset(TimeSpan.FromHours(1))));
    }

    [TestMethod]
    public void SharedPhoneNumbersRemainValidAndAddressIdentityIncludesKind()
    {
        var phone = new PartyPhone("+4930123", PhoneKind.Telephone, new("customer", "00042", "phone"), "+4930123");
        var address = new PartyAddress { ExternalId = "001", Kind = AddressKind.Billing, Phones = [phone] };
        var first = new Party { Key = Customer(), DisplayName = "Firma A", Phones = [phone, phone], Addresses = [address, address with { Kind = AddressKind.Shipping }] };
        var second = first with { Key = Customer("00043"), DisplayName = "Firma B" };
        var replacement = new PartyCacheReplacement(Company(), 0, [first, second], Now);
        Assert.HasCount(2, replacement.Parties);
        Assert.AreNotEqual(SourceIdentityHash.Address(first.Key, AddressKind.Billing, "001"), SourceIdentityHash.Address(first.Key, AddressKind.Shipping, "001"));
        Assert.ThrowsExactly<ArgumentException>(() => new PartyCacheReplacement(Company(), 0, [first with { Addresses = [address, address] }], Now));
        Assert.ThrowsExactly<ArgumentException>(() => new PartyCacheReplacement(Company(), 0, [first with { Phones = [phone with { E164 = "+49 30123" }] }], Now));
    }

    [TestMethod]
    public void CallWritesSeparatePersistenceVersionFromStateRevisionAndRetainHistoricalCandidates()
    {
        var call = Call();
        var parties = ImmutableArray.Create(new CallPartySnapshot(Customer(), "Historischer Name", "030 123", true),
            new CallPartySnapshot(Customer("43"), "Weiterer Treffer", "030 123", false));
        var events = ImmutableArray.Create(new CallEvent(Guid.NewGuid(), call.Id, 10, Now, "call.created", CallState.Created));
        var write = new CallWrite(call, 5, parties, events, new CallOrigin(new Guid("11111111-1111-1111-1111-111111111111"), "TEST-PC", "DOMAIN\\TestUser"));
        Assert.AreEqual(0L, write.Call.Revision);
        Assert.AreEqual(5L, write.ExpectedVersion);
        Assert.AreEqual(10L, write.Events[0].Sequence);
        Assert.HasCount(2, write.Parties);
        Assert.AreEqual("TEST-PC", write.Origin.MachineName);
        Assert.AreEqual(write.Origin, write.Call.Origin);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CallWrite(call, 0, parties, events, new CallOrigin(new Guid("11111111-1111-1111-1111-111111111111"), "TEST-PC", "DOMAIN\\TestUser")));
        Assert.ThrowsExactly<ArgumentException>(() => new CallWrite(call with { State = CallState.Connected }, null, [], [], new CallOrigin(new Guid("11111111-1111-1111-1111-111111111111"), "TEST-PC", "DOMAIN\\TestUser")));
    }

    [TestMethod]
    public void CallWritesRejectForeignParticipantsAmbiguousSelectionAndMisorderedEvents()
    {
        var call = Call();
        var selected = new CallPartySnapshot(Customer(), "A", null, true);
        var other = selected with { Party = Customer("43") };
        var first = new CallEvent(Guid.NewGuid(), call.Id, 1, Now, "call.created", CallState.Created);
        var second = first with { Id = Guid.NewGuid(), Sequence = 2, OccurredAtUtc = Now.AddSeconds(1) };
        Assert.ThrowsExactly<ArgumentException>(() => new CallWrite(call, null, [selected, other], [], new CallOrigin(new Guid("11111111-1111-1111-1111-111111111111"), "TEST-PC", "DOMAIN\\TestUser")));
        Assert.ThrowsExactly<ArgumentException>(() => new CallWrite(call with { Company = null }, null, [selected], [], new CallOrigin(new Guid("11111111-1111-1111-1111-111111111111"), "TEST-PC", "DOMAIN\\TestUser")));
        Assert.ThrowsExactly<ArgumentException>(() => new CallWrite(call, null, [], [second, first], new CallOrigin(new Guid("11111111-1111-1111-1111-111111111111"), "TEST-PC", "DOMAIN\\TestUser")));
        Assert.ThrowsExactly<ArgumentException>(() => new CallWrite(call, null, [], [first with { CallId = Guid.NewGuid() }], new CallOrigin(new Guid("11111111-1111-1111-1111-111111111111"), "TEST-PC", "DOMAIN\\TestUser")));
        Assert.ThrowsExactly<ArgumentException>(() => new CallWrite(call, null, [], [first with { Code = "localized message" }], new CallOrigin(new Guid("11111111-1111-1111-1111-111111111111"), "TEST-PC", "DOMAIN\\TestUser")));
        Assert.ThrowsExactly<ArgumentException>(() => new CallWrite(call with { RemoteAddress = new(NumberPresentation.Withheld, "private") }, null, [], [], new CallOrigin(new Guid("11111111-1111-1111-1111-111111111111"), "TEST-PC", "DOMAIN\\TestUser")));
    }

    [TestMethod]
    public void CallWritesRequireOriginAndCannotReplaceAnAlreadyCapturedPcOrWindowsUser()
    {
        var call = Call();
        var origin = new CallOrigin(Guid.NewGuid(), "PC-01", "DOMAIN\\Benutzer");
        var write = new CallWrite(call, null, [], [], origin);
        Assert.AreEqual(origin, write.Call.Origin);
        Assert.IsNull(call.Origin, "Enriching a write must not mutate the caller's live snapshot.");
        Assert.ThrowsExactly<ArgumentNullException>(() => new CallWrite(call, null, [], [], null!));
        var other = new CallOrigin(origin.WorkstationId, "PC-02", "DOMAIN\\Anderer");
        Assert.ThrowsExactly<ArgumentException>(() => new CallWrite(write.Call, 1, [], [], other));
    }

    [TestMethod]
    public void NoteAndRecordingWritesHaveIndependentVersionsAndBoundedPortableValues()
    {
        var call = Call();
        var note = new CallNoteWrite(call.Id, Guid.NewGuid(), "Notiz\nmit Umlauten ä und 🔐", null, Now);
        Assert.IsNull(note.ExpectedVersion);
        Assert.ThrowsExactly<ArgumentException>(() => new CallNoteWrite(call.Id, note.NoteId, new string('x', 32769), 1, Now));
        var recording = new Recording(Guid.NewGuid(), call.Id, "2026/11/gespräch.wav", "wav-pcm16", TimeSpan.FromTicks(1), RecordingAvailability.Pending);
        Assert.AreEqual(recording, new RecordingWrite(recording, 3, Now).Recording);
        foreach (var invalid in new[] { "../outside.wav", "C:/secret.wav", "/root.wav", "a\\b.wav", "a//b.wav", "CON.wav", "COM¹.wav", "CONOUT$", "name./b.wav", "a/../b.wav" })
            Assert.ThrowsExactly<ArgumentException>(() => new RecordingWrite(recording with { RelativePath = invalid }, 1, Now));
        Assert.ThrowsExactly<ArgumentException>(() => new RecordingWrite(recording with { Duration = TimeSpan.FromTicks(-1) }, 1, Now));
    }

    [TestMethod]
    public void SourceParseErrorsCarryIndependentFullIdentityAndFixedCodes()
    {
        var source = new SourceField("additional-address", "00042-A", "phone2");
        var failure = new NumberParseFailure(Guid.NewGuid(), Customer(), source, PhoneKind.Telephone, "unknown source value", "number.invalid", Now);
        Assert.AreEqual(Customer(), failure.Party);
        Assert.AreEqual(source, failure.Source);
        Assert.AreEqual(" ", new NumberParseFailure(Guid.NewGuid(), Customer(), source, PhoneKind.Telephone, " ", "number.empty", Now).OriginalValue);
        Assert.ThrowsExactly<ArgumentException>(() => new NumberParseFailure(Guid.NewGuid(), Customer(), source, PhoneKind.Telephone, "raw", "free message", Now));
        Assert.ThrowsExactly<ArgumentException>(() => new NumberParseFailure(Guid.NewGuid(), Customer(), source, PhoneKind.Telephone, "raw", "number.invalid", Now.ToOffset(TimeSpan.FromHours(-1))));
    }

    [TestMethod]
    public void ConflictCodesCannotCarryProviderMessagesOrRejectedSourceValues()
    {
        foreach (var kind in Enum.GetValues<RepositoryConflictKind>())
        {
            var conflict = new RepositoryConflictException(kind);
            Assert.AreEqual(kind, conflict.Kind);
            Assert.IsNull(conflict.InnerException);
            Assert.IsFalse(string.IsNullOrWhiteSpace(conflict.Message));
        }
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new RepositoryConflictException((RepositoryConflictKind)999));
    }

    private static CallSnapshot Call() => new()
    {
        Id = Guid.NewGuid(), Revision = 0, Company = Company(), Direction = CallDirection.Incoming,
        State = CallState.Created, StartedAtUtc = Now, StateChangedAtUtc = Now,
        LocalAddress = new(NumberPresentation.Unknown), RemoteAddress = new(NumberPresentation.Available, "030 123")
    };
}

using System.Collections.Immutable;
using WawiCallPro.Core.PhoneNumbers;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Interfaces.PhoneNumbers;
using WawiCallPro.Interfaces.Telephony;

namespace WawiCallPro.Core.Tests;

[TestClass]
public sealed class CompanyPhoneMatchIndexTests
{
    private readonly LibPhoneNumberParser _parser = new();
    private static readonly CompanyKey Company = new(LexwareProductLine.Pro, "fixture", "01");
    private static Party Party(string id, string? number = null, PartyKind kind = PartyKind.Customer, string? owner = null, string? extension = null) => new()
    {
        Key = new(Company, kind, id, owner), DisplayName = "Fixture " + id,
        Phones = number is null ? [] : [new(number, PhoneKind.Telephone, new("fixture", id, "telephone"), number, extension)]
    };
    private ICompanyPhoneMatchIndex Index(Party[] parties, int width = 5, bool enabled = true,
        PhoneNumberBlockRule[]? blocks = null, long ruleRevision = 1, string? cacheVersion = null) =>
        new CompanyPhoneMatchIndexFactory(_parser).Create(new(new(new(Company, enabled, width), 1, ruleRevision), 7,
            cacheVersion ?? _parser.Version, true, parties.ToImmutableArray(), (blocks ?? []).ToImmutableArray()));
    private PhoneNumberInterpretation Incoming(string number) => new PhoneNumberInterpreter(_parser).InterpretIncoming(
        new(NumberPresentation.Available, number), new("fixture-pbx", "line", "DE", "030"));

    [TestMethod]
    [DataRow(5, "+493012345000", "+4930123456789", "12345", "6789")]
    [DataRow(4, "+49301234000", "+493012345678", "1234", "5678")]
    [DataRow(5, "+493012345", "+493012345678", "12345", "678")]
    [DataRow(5, "+493012345000", "+493012345", "12345", "")]
    public void ConfiguredWidthCountsOnlySubscriberDigitsAndPreservesBothSuffixForms(int width, string registered, string incoming, string stem, string apparatus)
    {
        var query = Incoming(incoming + " ext. 42");
        var index = Index([Party("A", registered)], width);
        var candidate = index.Find(query).Single();
        var evidence = candidate.Evidence.Single(item => item.Match.Kind == PhoneNumberMatchKind.StemPrefix).Match;
        Assert.AreEqual(49, evidence.CountryCallingCode); Assert.AreEqual("30", evidence.DestinationCode);
        Assert.AreEqual(stem, evidence.Stem); Assert.AreEqual(apparatus, evidence.ApparatusNumber);
        Assert.AreEqual(width, evidence.StartWidth); Assert.AreEqual("42", query.Address.Extension);
        Assert.AreEqual(registered, evidence.MatchedE164); Assert.AreEqual(7L, evidence.CacheRevision);
        Assert.AreEqual(1L, evidence.RuleRevision); Assert.IsTrue(candidate.IsPositive);
    }

    [TestMethod]
    [DataRow("+4930123466789")]
    [DataRow("+4940123456789")]
    [DataRow("+31301234567")]
    [DataRow("+49301234")]
    public void DifferentStemDestinationCountryAndTooShortSubscriberNeverFallBack(string incoming) =>
        Assert.IsEmpty(Index([Party("A", "+493012345000")]).Find(Incoming(incoming)));

    [TestMethod]
    public void DisablingStemMatchingKeepsExactMatchesAndAllAlternativeParties()
    {
        var parties = new[] { Party("A", "+4930123456789"), Party("B", "+493012345000") };
        var query = Incoming("+4930123456789");
        var matches = Index(parties).Find(query);
        Assert.HasCount(2, matches); Assert.AreEqual("A", matches[0].Party.ExternalId);
        Assert.AreEqual(PhoneNumberMatchKind.Exact, matches[0].Evidence[0].Match.Kind);
        Assert.IsTrue(matches.All(item => item.IsPositive));
        var disabled = Index(parties, enabled: false).Find(query);
        Assert.HasCount(1, disabled); Assert.AreEqual(PhoneNumberMatchKind.Exact, disabled[0].Evidence.Single().Match.Kind);
    }

    [TestMethod]
    public void ContactPrefixEvidenceCollapsesToItsOrganisationAndRetainsSources()
    {
        var owner = Party("owner");
        var contacts = new[] { owner, Party("one", "+493012345000", PartyKind.CustomerContact, "owner", "001"),
            Party("two", "+493012345111", PartyKind.CustomerContact, "owner", "002") };
        var match = Index(contacts).Find(Incoming("+4930123456789")).Single();
        Assert.AreEqual(owner.Key, match.Party); Assert.HasCount(2, match.Evidence);
        CollectionAssert.AreEquivalent(new[] { "one", "two" }, match.Evidence.Select(item => item.SourceParty.ExternalId).ToArray());
        Assert.IsTrue(match.Evidence.All(item => item.Match.ApparatusNumber == "6789"));
    }

    [TestMethod]
    public void ASharedBaseWithoutExplicitExtensionDoesNotIdentifyAContact()
    {
        var owner = Party("owner"); var contact = Party("contact", "+493012345000", PartyKind.CustomerContact, "owner", "0042");
        var index = Index([owner, contact], enabled: false);
        Assert.AreEqual(owner.Key, index.Find(Incoming("+493012345000")).Single().Party);
        Assert.AreEqual(contact.Key, index.Find(Incoming("+493012345000 ext. 0042")).Single().Party);
        Assert.AreEqual(owner.Key, index.Find(Incoming("+493012345000 ext. 0043")).Single().Party);
    }

    private static PhoneNumberBlockRule Block(Party owner, string stem, PhoneNumberBlockKind kind) => new()
    {
        Id = Guid.NewGuid(), Organisation = owner.Key, Kind = kind, CountryCallingCode = 49,
        DestinationCode = "30", Stem = stem, MinimumSuffixDigits = 1, MaximumSuffixDigits = 4, Evidence = "Synthetic explicit fixture"
    };

    [TestMethod]
    public void ConfirmedAndInferredBlocksStaySeparateAndPreferLongerPrefixes()
    {
        var a = Party("A"); var b = Party("B"); var c = Party("C");
        var confirmed = Block(a, "12345", PhoneNumberBlockKind.Confirmed);
        var shorter = Block(b, "1234", PhoneNumberBlockKind.Confirmed);
        var inferred = Block(c, "12345", PhoneNumberBlockKind.Inferred);
        var index = Index([a, b, c], enabled: false, blocks: [shorter, inferred, confirmed]);
        var matches = index.Find(Incoming("+493012345678"));
        Assert.HasCount(3, matches); Assert.AreEqual(a.Key, matches[0].Party); Assert.AreEqual(b.Key, matches[1].Party);
        Assert.AreEqual(c.Key, matches[2].Party); Assert.IsFalse(matches[2].IsPositive);
        Assert.AreEqual(confirmed.Id, matches[0].Evidence.Single().Match.RuleId);
        Assert.AreEqual("678", matches[0].Evidence.Single().Match.ApparatusNumber);
        Assert.IsEmpty(index.Find(Incoming("+49301234567890")), "Suffix limits apply to each block independently.");
    }

    [TestMethod]
    public void ANewRuleBuildNeverMutatesTheIndexHeldByAnEarlierCall()
    {
        var parties = new[] { Party("A", "+49301234000") };
        var old = Index(parties, 4, ruleRevision: 1); var fresh = Index(parties, 5, ruleRevision: 2);
        var query = Incoming("+493012345678");
        Assert.HasCount(1, old.Find(query)); Assert.IsEmpty(fresh.Find(query));
        Assert.AreEqual(1L, old.Find(query)[0].Evidence[0].Match.RuleRevision);
        Assert.AreEqual(2L, fresh.Profile.RuleRevision); StringAssert.EndsWith(fresh.Version, ";matching/1");
    }

    [TestMethod]
    public void MissingOwnersAndOutdatedParserMetadataRemainIncomplete()
    {
        var orphan = Index([Party("contact", "+493012345000", PartyKind.CustomerContact, "missing")]);
        Assert.IsFalse(orphan.IsComplete); Assert.IsEmpty(orphan.Find(Incoming("+4930123456789")));
        var stale = Index([Party("A", "+493012345000")], cacheVersion: "old-parser");
        Assert.IsFalse(stale.IsComplete); Assert.HasCount(1, stale.Find(Incoming("+4930123456789")));
    }

    [TestMethod]
    public void SignificantLeadingZeroInTheItalianDestinationRemainsPartOfThePrefixKey()
    {
        var index = Index([Party("IT", "+390236618300")]);
        var evidence = index.Find(Incoming("+390236618399")).Single().Evidence.Single().Match;
        Assert.AreEqual(39, evidence.CountryCallingCode); Assert.AreEqual("02", evidence.DestinationCode);
        Assert.AreEqual("36618", evidence.Stem); Assert.AreEqual("399", evidence.ApparatusNumber);
    }

    [TestMethod]
    public void NonPublicNumbersAndCancelledLookupsNeverProduceCandidates()
    {
        var index = Index([Party("A", "+493012345000")]);
        var interpreter = new PhoneNumberInterpreter(_parser);
        foreach (var presentation in new[] { NumberPresentation.Withheld, NumberPresentation.Unknown })
            Assert.IsEmpty(index.Find(interpreter.InterpretIncoming(new(presentation, "+4930123456789"))));
        Assert.IsEmpty(index.Find(Incoming("sip:fixture@example.invalid")));
        using var stopping = new CancellationTokenSource(); stopping.Cancel();
        Assert.ThrowsExactly<OperationCanceledException>(() => index.Find(Incoming("+4930123456789"), stopping.Token));
        Assert.ThrowsExactly<OperationCanceledException>(() => new CompanyPhoneMatchIndexFactory(_parser).Create(
            new(new(new(Company, true, 5), 1, 1), 1, _parser.Version, true, [], []), stopping.Token));
    }
}

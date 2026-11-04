using System.Collections.Immutable;
using System.Text.Json;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Interfaces.PhoneNumbers;
using WawiCallPro.Interfaces.Telephony;

namespace WawiCallPro.Database.Tests;

[TestClass]
public sealed class MultiCompanyContractTests
{
    private static readonly DateTimeOffset Now = new(638000000000000001, TimeSpan.Zero);
    private static readonly CompanyKey A = new(LexwareProductLine.Pro, "installation-A", "0007");
    private static readonly CompanyKey B = new(LexwareProductLine.Pro, "installation-B", "0007");
    private static PartyKey Customer(CompanyKey company) => new(company, PartyKind.Customer, "0042");
    private static CallSnapshot Call() => new()
    {
        Id = Guid.NewGuid(), Revision = 0, Company = A, Direction = CallDirection.Incoming, State = CallState.Ringing,
        LocalAddress = new(NumberPresentation.Unknown), RemoteAddress = new(NumberPresentation.Available, "+49301234542"),
        StartedAtUtc = Now, StateChangedAtUtc = Now
    };
    private static CallOrigin Origin() => new(Guid.NewGuid(), "TEST-PC", "DOMAIN\\User");
    private static CallerLookupSnapshot Lookup(bool complete = true) => new()
    {
        ParserVersion = "synthetic/2", SearchedAtUtc = Now, IsComplete = complete,
        SearchCompanies = [new(A, 1, 1, true, Now, "synthetic/2"), new(B, 1, 1, true, Now, "synthetic/2")]
    };

    [TestMethod]
    public void CandidatesCanCrossCompaniesBeforeAndAfterSelection()
    {
        var call = Call();
        CallPartySnapshot[] parties = [new(Customer(A), "A", "+49301234542", true), new(Customer(B), "B", "+49301234542", false)];
        var write = new CallWrite(call, null, [.. parties], [], Origin(), Lookup());
        Assert.HasCount(2, write.Parties);
        Assert.AreNotEqual(write.Parties[0].Party, write.Parties[1].Party);
        var pending = new CallWrite(call with { Company = null }, null, [.. parties.Select(party => party with { IsSelected = false })], [], Origin(), Lookup());
        Assert.IsNull(pending.Call.Company);
        Assert.HasCount(2, pending.Parties);
        Assert.ThrowsExactly<ArgumentException>(() => new CallWrite(call with { Company = B }, null, [.. parties], [], Origin()));
    }

    [TestMethod]
    public void CompletenessRequiresEveryDistinctCompanyAndCompatibleCacheMetadata()
    {
        var call = Call();
        var lookup = Lookup();
        _ = new CallWrite(call, null, [], [], Origin(), lookup);
        var missing = lookup with { SearchCompanies = [lookup.SearchCompanies[0], new(B, 0, 0, false, null, null)] };
        Assert.ThrowsExactly<ArgumentException>(() => new CallWrite(call, null, [], [], Origin(), missing));
        _ = new CallWrite(call, null, [], [], Origin(), missing with { IsComplete = false });
        Assert.ThrowsExactly<ArgumentException>(() => new CallWrite(call, null, [], [], Origin(), lookup with { SearchCompanies = [lookup.SearchCompanies[0], lookup.SearchCompanies[0]] }));
        Assert.ThrowsExactly<ArgumentException>(() => new CallWrite(call, null, [], [], Origin(), lookup with { ParserVersion = "changed" }));
        Assert.ThrowsExactly<ArgumentException>(() => new CallWrite(call, null, [], [], Origin(), lookup with { SearchCompanies = [] }));
    }

    [TestMethod]
    public void HistoricalPrefixEvidenceRetainsTheExactBoundaryAndVersion()
    {
        var evidence = new PhoneNumberMatchEvidence
        {
            Kind = PhoneNumberMatchKind.StemPrefix, ParserVersion = "synthetic/2", CacheRevision = 1, RuleRevision = 2,
            StartWidth = 5, CountryCallingCode = 49, DestinationCode = "30", Stem = "12345", ApparatusNumber = "0042",
            MatchedE164 = "+493012345000", Source = new("customer", "0042", "phone")
        };
        var candidate = new CallPartySnapshot(Customer(A), "Old customer", "+493012345000", true)
        { CompanyDisplayName = "Old company", Match = evidence };
        var write = new CallWrite(Call(), null, [candidate], [], Origin(), Lookup());
        var restored = JsonSerializer.Deserialize<CallPartySnapshot>(JsonSerializer.Serialize(write.Parties[0]))!;
        Assert.AreEqual(candidate, restored);
        Assert.AreEqual("0042", restored.Match!.ApparatusNumber);
        Assert.ThrowsExactly<ArgumentException>(() => new CallWrite(Call(), null, [candidate], [], Origin(), Lookup() with { SearchCompanies = [Lookup().SearchCompanies[1]] }));
        Assert.ThrowsExactly<ArgumentException>(() => new CallWrite(Call(), null, [candidate with { Match = evidence with { StartWidth = 4 } }], [], Origin()));
        Assert.ThrowsExactly<ArgumentException>(() => new CallWrite(Call(), null, [candidate with { Match = evidence with { Kind = PhoneNumberMatchKind.Exact } }], [], Origin()));
        Assert.ThrowsExactly<ArgumentException>(() => new CallWrite(Call(), null, [candidate with { Match = evidence with { Kind = PhoneNumberMatchKind.ConfirmedBlock, StartWidth = null } }], [], Origin()));
    }

    [TestMethod]
    public void SharedProfilesAndBlockRulesRequireBoundedDigitsAndOrganisationIdentity()
    {
        _ = new PhoneMatchingSettingsWrite(new(A, true, 5), null, Now);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new PhoneMatchingSettingsWrite(new(A, true, 0), null, Now));
        var rule = new PhoneNumberBlockRule
        {
            Id = Guid.NewGuid(), Organisation = Customer(A), Kind = PhoneNumberBlockKind.Confirmed,
            CountryCallingCode = 49, DestinationCode = "30", Stem = "12345", MaximumSuffixDigits = 4, Evidence = "Explicit user rule"
        };
        var write = new PhoneNumberBlockRuleWrite(rule, null, Now);
        Assert.AreSame(rule, write.Rule);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new PhoneNumberBlockRuleWrite(rule with { MaximumSuffixDigits = int.MaxValue }, null, Now));
        Assert.ThrowsExactly<ArgumentException>(() => new PhoneNumberBlockRuleWrite(rule with { DestinationCode = "30x" }, null, Now));
        Assert.ThrowsExactly<ArgumentException>(() => new PhoneNumberBlockRuleWrite(rule with { Organisation = new(A, PartyKind.CustomerContact, "1", "0042") }, null, Now));
    }
}

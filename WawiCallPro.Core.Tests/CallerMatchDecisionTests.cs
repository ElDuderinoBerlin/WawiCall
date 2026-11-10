using System.Collections.Immutable;
using WawiCallPro.Core.PhoneNumbers;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Interfaces.PhoneNumbers;

namespace WawiCallPro.Core.Tests;

[TestClass]
public sealed class CallerMatchDecisionTests
{
    private static readonly CompanyKey A = new(LexwareProductLine.Pro, "A", "1");
    private static readonly CompanyKey B = new(LexwareProductLine.Pro, "B", "1");
    private static readonly CallerMatchDecisionService Policy = new();
    private static CallerLineAssignment Line(CompanyKey company, bool verified = true) => new(new("pbx", "line", "DE"), company, "Zentrale", verified);
    private static PhoneMatchCandidate Candidate(CompanyKey company, string id = "01", PartyKind role = PartyKind.Customer,
        PhoneNumberMatchKind kind = PhoneNumberMatchKind.Exact)
    {
        var key = new PartyKey(company, role, id, role is PartyKind.CustomerContact or PartyKind.SupplierContact ? "owner" : null);
        return new(key, "Same name", [new(key, "42", new() { Kind = kind, ParserVersion = "test", CacheRevision = 1, RuleRevision = 2 })]);
    }
    private static PhoneSearchResult Result(params PhoneMatchCandidate[] candidates)
    {
        var companies = new[] { A, B }.Select(company => new PhoneSearchCompanyStatus(company, "Same company name", PhoneSearchCompanyState.Ready, 1, 2)).ToImmutableArray();
        return new(new(1, PhoneSearchScopeState.Ready, DateTimeOffset.UtcNow, companies, true),
            new() { ParserVersion = "test", IsComplete = true, SearchedAtUtc = DateTimeOffset.UtcNow,
                SearchCompanies = companies.Select(company => new CallerSearchCompanySnapshot(company.Company, 1, 2, true, null, "test")).ToImmutableArray() },
            candidates.ToImmutableArray());
    }
    [TestMethod]
    public void FullKeysSeparateIdenticalNamesAndIdsAndActiveCompanyOnlyChangesOrder()
    {
        var result = Result(Candidate(A), Candidate(B));
        var first = Policy.Evaluate(result, new(A));
        var second = Policy.Evaluate(result, new(B));
        Assert.AreEqual(CallerMatchDecisionKind.Ambiguous, first.Kind);
        Assert.IsNull(first.Selection); Assert.IsNull(second.Selection);
        Assert.AreEqual(A, first.Groups[0].Company.Company); Assert.AreEqual(B, second.Groups[0].Company.Company);
        Assert.HasCount(2, first.Groups.SelectMany(group => group.Candidates));
    }
    [TestMethod]
    [DataRow(PhoneNumberMatchKind.Exact)]
    [DataRow(PhoneNumberMatchKind.ConfirmedBlock)]
    [DataRow(PhoneNumberMatchKind.StemPrefix)]
    public void OnePositiveInCompleteScopeIsSelectedWithReason(PhoneNumberMatchKind kind)
    {
        var candidate = Candidate(B, kind: kind);
        var decision = Policy.Evaluate(Result(candidate), new(A));
        Assert.AreEqual(CallerMatchDecisionKind.Unique, decision.Kind);
        Assert.AreEqual(candidate.Party, decision.Selection!.Party);
        Assert.AreEqual("unique", decision.Lookup.SelectionReason);
        Assert.IsFalse(decision.Selection.IsProvisional);
    }
    [TestMethod]
    public void CustomerSupplierAndContactsRemainDistinctEvenWithSameExternalIds()
    {
        var decision = Policy.Evaluate(Result(Candidate(A), Candidate(A, role: PartyKind.Supplier),
            Candidate(A, role: PartyKind.CustomerContact), Candidate(A, role: PartyKind.SupplierContact)), new());
        Assert.AreEqual(CallerMatchDecisionKind.Ambiguous, decision.Kind);
        Assert.HasCount(4, decision.Groups.Single(group => group.Company.Company == A).Candidates);
        Assert.IsNull(decision.Selection);
    }
    [TestMethod]
    public void ExactVersusStemCollisionNeverChoosesByRankOrResponseOrder()
    {
        var exact = Candidate(A); var stem = Candidate(B, kind: PhoneNumberMatchKind.StemPrefix);
        foreach (var result in new[] { Result(exact, stem), Result(stem, exact) })
            Assert.IsNull(Policy.Evaluate(result, new(A)).Selection);
    }
    [TestMethod]
    public void InferredOnlyNeverSelectsAndInferredAlternativesRemainVisibleBesidePositive()
    {
        var inferred = Candidate(B, kind: PhoneNumberMatchKind.InferredBlock);
        Assert.IsNull(Policy.Evaluate(Result(inferred), new()).Selection);
        var result = Policy.Evaluate(Result(Candidate(A), inferred), new());
        Assert.AreEqual(A, result.Selection!.Party.Company);
        Assert.AreEqual(inferred, result.Groups.Single(group => group.Company.Company == B).Candidates.Single());
    }
    [TestMethod]
    public void VerifiedLineSelectsItsSinglePositiveWithoutRemovingAlternatives()
    {
        var result = Policy.Evaluate(Result(Candidate(A), Candidate(B)), new(B, Line(A)));
        Assert.AreEqual(CallerMatchDecisionKind.LineUnique, result.Kind);
        Assert.AreEqual(A, result.Selection!.Party.Company);
        Assert.AreEqual("configured-line", result.Lookup.SelectionReason);
        Assert.HasCount(2, result.Groups.SelectMany(group => group.Candidates));
        Assert.AreEqual(A, result.Groups[0].Company.Company);
    }
    [TestMethod]
    public void UnverifiedLineCannotResolveAmbiguityOrOverrideActiveOrdering()
    {
        var result = Policy.Evaluate(Result(Candidate(A), Candidate(B)), new(B, Line(A, false)));
        Assert.IsNull(result.Selection); Assert.AreEqual(B, result.Groups[0].Company.Company);
    }
    [TestMethod]
    public void VerifiedLineConflictAndMultiplePartiesOnLineRequireExplicitChoice()
    {
        Assert.AreEqual(CallerMatchDecisionKind.LineConflict, Policy.Evaluate(Result(Candidate(B)), new(null, Line(A))).Kind);
        var ambiguous = Policy.Evaluate(Result(Candidate(A), Candidate(A, "02"), Candidate(B)), new(null, Line(A)));
        Assert.AreEqual(CallerMatchDecisionKind.Ambiguous, ambiguous.Kind); Assert.IsNull(ambiguous.Selection);
        var inferred = Policy.Evaluate(Result(Candidate(B), Candidate(A, kind: PhoneNumberMatchKind.InferredBlock)), new(null, Line(A)));
        Assert.AreEqual(CallerMatchDecisionKind.LineConflict, inferred.Kind); Assert.IsNull(inferred.Selection);
    }
    [TestMethod]
    [DataRow(PhoneSearchScopeState.Partial)]
    [DataRow(PhoneSearchScopeState.ScopeExpired)]
    [DataRow(PhoneSearchScopeState.Refreshing)]
    [DataRow(PhoneSearchScopeState.CatalogUnavailable)]
    public void GlobalIncompletenessPreventsUniqueAndLineAutomaticChoice(PhoneSearchScopeState state)
    {
        var result = Result(Candidate(A));
        result = result with { Index = result.Index with { State = state, IsComplete = false }, Lookup = result.Lookup with { IsComplete = false } };
        foreach (var context in new[] { new CallerMatchContext(), new CallerMatchContext(null, Line(A)) })
        {
            var decision = Policy.Evaluate(result, context);
            Assert.AreEqual(CallerMatchDecisionKind.Incomplete, decision.Kind); Assert.IsNull(decision.Selection);
        }
    }
    [TestMethod]
    [DataRow(PhoneSearchCompanyState.CacheMissing)]
    [DataRow(PhoneSearchCompanyState.CacheStale)]
    [DataRow(PhoneSearchCompanyState.NormalizationOutdated)]
    [DataRow(PhoneSearchCompanyState.ContextChanged)]
    [DataRow(PhoneSearchCompanyState.Incomplete)]
    [DataRow(PhoneSearchCompanyState.NotAuthorized)]
    public void AnUnavailableOtherCompanyPreventsGlobalUniqueness(PhoneSearchCompanyState state)
    {
        var result = Result(Candidate(A));
        result = result with { Index = result.Index with { Companies = result.Index.Companies.Select(company => company.Company == B ? company with { State = state } : company).ToImmutableArray() } };
        Assert.IsNull(Policy.Evaluate(result, new()).Selection);
    }
    [TestMethod]
    public void MismatchingEvidenceAndScopeRevisionsCannotClaimUniqueness()
    {
        var result = Result(Candidate(A));
        result = result with { Lookup = result.Lookup with { SearchCompanies = result.Lookup.SearchCompanies.Select(company => company with { RuleRevision = 3 }).ToImmutableArray() } };
        Assert.AreEqual(CallerMatchDecisionKind.Incomplete, Policy.Evaluate(result, new()).Kind);
        result = Result(Candidate(A)) with { Candidates = [Candidate(A) with { Evidence = [new(new(A, PartyKind.Customer, "01"), null,
            new() { Kind = PhoneNumberMatchKind.Exact, ParserVersion = "old", CacheRevision = 1, RuleRevision = 2 })] }] };
        Assert.IsNull(Policy.Evaluate(result, new()).Selection);
    }
    [TestMethod]
    public void ManualChoiceSurvivesLineAndRankChangesAndMissingChoiceNeverSelectsAReplacement()
    {
        var decision = Policy.Evaluate(Result(Candidate(A), Candidate(B)), new());
        var chosen = Policy.Choose(decision, Candidate(B).Party);
        var updated = Policy.Evaluate(Result(Candidate(A), Candidate(B)), new(null, Line(A)), chosen.Selection);
        Assert.AreEqual(CallerMatchSelectionKind.Manual, updated.Selection!.Kind);
        Assert.AreEqual(B, updated.Selection.Party.Company);
        var missing = Policy.Evaluate(Result(Candidate(A)), new(null, Line(A)), chosen.Selection);
        Assert.AreEqual(CallerMatchDecisionKind.SelectionUnavailable, missing.Kind); Assert.IsNull(missing.Selection);
        Assert.AreEqual("manual", chosen.Lookup.SelectionReason);
    }
    [TestMethod]
    public void ExplicitProvisionalChoiceIsPossibleButForeignChoiceIsRejected()
    {
        var inferred = Candidate(A, kind: PhoneNumberMatchKind.InferredBlock);
        var chosen = Policy.Choose(Policy.Evaluate(Result(inferred), new()), inferred.Party);
        Assert.IsTrue(chosen.Selection!.IsProvisional);
        Assert.ThrowsExactly<ArgumentException>(() => Policy.Choose(chosen, Candidate(B).Party));
    }
    [TestMethod]
    public void MalformedCrossCompanyOrDuplicateCandidateIsRejected()
    {
        var malformed = Candidate(A) with { Evidence = Candidate(B).Evidence };
        Assert.ThrowsExactly<ArgumentException>(() => Policy.Evaluate(Result(malformed), new()));
        Assert.ThrowsExactly<ArgumentException>(() => Policy.Evaluate(Result(Candidate(A), Candidate(A)), new()));
    }
    [TestMethod]
    public void EmptyCompleteScopeHasNoAutomaticSelection()
    {
        var decision = Policy.Evaluate(Result(), new());
        Assert.AreEqual(CallerMatchDecisionKind.NoMatch, decision.Kind); Assert.IsNull(decision.Selection);
    }
    [TestMethod]
    public void MissingConfiguredSearchScopeIsDistinguishedFromACompleteSearchWithoutHits()
    {
        var result = Result();
        result = result with { Index = result.Index with { State = PhoneSearchScopeState.NoSelection, Companies = [], IsComplete = false },
            Lookup = result.Lookup with { SearchCompanies = [], IsComplete = false } };
        Assert.AreEqual(CallerMatchDecisionKind.NoSearchCompanies, Policy.Evaluate(result, new()).Kind);
    }
}

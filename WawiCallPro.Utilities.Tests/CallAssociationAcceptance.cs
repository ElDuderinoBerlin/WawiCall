using System.Collections.Immutable;
using Microsoft.Extensions.DependencyInjection;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Common;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Interfaces.PhoneNumbers;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Utilities.Hosting;
using WawiCallPro.Utilities.Persistence;

namespace WawiCallPro.Utilities.Tests;

/// <summary>The same real-repository scenario runs on SQLite and each isolated server engine.</summary>
public static class CallAssociationAcceptance
{
    public static async Task RunAsync(IServiceProvider services, CancellationToken token)
    {
        var line = Enum.Parse<LexwareProductLine>(services.GetRequiredService<ApplicationDescriptor>().ProductLine);
        var companies = new[] { new Company(new(line, "call-context-A", Guid.NewGuid().ToString("N")), "Firma A"),
            new Company(new(line, "call-context-B", Guid.NewGuid().ToString("N")), "Firma B") };
        foreach (var company in companies) await services.GetRequiredService<ICompanyRepository>().WriteAsync(new(company, null, DateTimeOffset.UtcNow), token);
        var candidates = companies.Select(company => new PhoneMatchCandidate(new(company.Key, PartyKind.Customer, "01"), "Original " + company.DisplayName,
            [new(new(company.Key, PartyKind.Customer, "01"), null, new() { Kind = PhoneNumberMatchKind.Exact, ParserVersion = "fixture", CacheRevision = 1 })])).ToImmutableArray();
        var lookup = new CallerLookupSnapshot { IsComplete = true, ParserVersion = "fixture", SearchedAtUtc = DateTimeOffset.UtcNow,
            SearchCompanies = companies.Select(company => new CallerSearchCompanySnapshot(company.Key, 1, 0, true, DateTimeOffset.UtcNow, "fixture")).ToImmutableArray() };
        var result = new PhoneSearchResult(new(1, PhoneSearchScopeState.Ready, DateTimeOffset.UtcNow,
            companies.Select(company => new PhoneSearchCompanyStatus(company.Key, company.DisplayName, PhoneSearchCompanyState.Ready, 1)).ToImmutableArray(), true), lookup, candidates);
        var policy = services.GetRequiredService<ICallerMatchDecisionService>();
        var decision = policy.Evaluate(result, new());
        var now = DateTimeOffset.UtcNow;
        var call = await services.GetRequiredService<ICallJournal>().BeginAsync(new() { Id = Guid.NewGuid(), Revision = 0, Direction = CallDirection.Incoming,
            State = CallState.Created, StartedAtUtc = now, StateChangedAtUtc = now,
            LocalAddress = new(NumberPresentation.Unknown), RemoteAddress = new(NumberPresentation.Available, "+49301234567") }, token);
        var association = services.GetRequiredService<ICallAssociationService>();
        var first = await association.SaveAsync(call.Id, decision, token);
        Assert.HasCount(2, first.Parties); Assert.IsNull(first.Call.Company);
        Assert.AreEqual(System.Text.Json.JsonSerializer.Serialize(lookup), System.Text.Json.JsonSerializer.Serialize(first.Lookup));
        var repository = services.GetRequiredService<ICallRepository>();
        var racing = new RacingRepository(repository);
        var selected = policy.Choose(decision, candidates[1].Party);
        var saved = await new DatabaseCallAssociation(racing).SaveAsync(call.Id, selected, token);
        Assert.AreEqual(2, racing.Writes, "An intervening state write requires a CAS retry.");
        Assert.AreEqual(CallState.Ringing, saved.Call.State); Assert.AreEqual(1L, saved.Call.Revision);
        Assert.AreEqual(companies[1].Key, saved.Call.Company); Assert.AreEqual(candidates[1].Party, saved.Parties.Single(party => party.IsSelected).Party);
        var newer = decision with { Groups = decision.Groups.Select(group => group with { Candidates = group.Candidates.Select(candidate => candidate with { DisplayName = "New master data" }).ToImmutableArray() }).ToImmutableArray() };
        saved = await association.SaveAsync(call.Id, policy.Choose(newer, candidates[0].Party), token);
        Assert.AreEqual("Original Firma A", saved.Parties.Single(party => party.IsSelected).DisplayName);
        Assert.AreEqual(System.Text.Json.JsonSerializer.Serialize(lookup with { SelectionReason = "manual" }), System.Text.Json.JsonSerializer.Serialize(saved.Lookup));
        var automatic = policy.Evaluate(result with { Candidates = [candidates[1]] }, new());
        saved = await association.SaveAsync(call.Id, automatic, token);
        Assert.AreEqual(candidates[0].Party, saved.Parties.Single(party => party.IsSelected).Party, "Automatic responses cannot overwrite a recorded manual decision.");
        Assert.AreEqual(1L, saved.Call.Revision, "Association writes do not invent SIP transitions.");
        await Assert.ThrowsExactlyAsync<RepositoryConflictException>(() => association.SaveAsync(Guid.NewGuid(), decision, token));
    }
    private sealed class RacingRepository(ICallRepository inner) : ICallRepository
    {
        internal int Writes;
        public Task<StoredCall?> GetAsync(Guid callId, CancellationToken cancellationToken = default) => inner.GetAsync(callId, cancellationToken);
        public Task<Page<CallSnapshot>> GetPageAsync(CallHistoryQuery query, CancellationToken cancellationToken = default) => inner.GetPageAsync(query, cancellationToken);
        public async Task<StoredCall> WriteAsync(CallWrite write, CancellationToken cancellationToken = default)
        {
            if (++Writes == 1)
            {
                var ringing = write.Call with { Company = null, Revision = 1, State = CallState.Ringing, StateChangedAtUtc = write.Call.StateChangedAtUtc.AddSeconds(1) };
                var current = (await inner.GetAsync(ringing.Id, cancellationToken))!;
                await inner.WriteAsync(new(ringing, current.Version, current.History.Parties,
                    [new(Guid.NewGuid(), ringing.Id, 1, ringing.StateChangedAtUtc, "Fixture.Ringing", CallState.Ringing)], ringing.Origin!, current.History.Lookup), cancellationToken);
            }
            return await inner.WriteAsync(write, cancellationToken);
        }
    }
}

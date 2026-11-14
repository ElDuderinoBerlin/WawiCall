using System.Collections.Immutable;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WawiCallPro.Core.Directory;
using WawiCallPro.Interfaces.Capabilities;
using WawiCallPro.Interfaces.Common;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Parties;

namespace WawiCallPro.Core.Tests;

[TestClass]
public sealed class PartySearchTests
{
    private static readonly CompanyKey Company = new(LexwareProductLine.Pro, "fixture", "0001");
    private static Party Row(string id, PartyKind kind = PartyKind.Customer, string? owner = null) => new()
    { Key = new(Company, kind, id, owner), DisplayName = "Beispiel " + id, Number = id };

    [TestMethod]
    public async Task SearchReturnsAll501RowsInStableOrderAndKeepsTheExplicitQuery()
    {
        var query = new PartySearchQuery(Company, "  Beispiel  ");
        var rows = Enumerable.Range(0, 501).Reverse().Select(id => Row(id.ToString("D4"))).ToImmutableArray();
        var source = new Source((request, _) => Task.FromResult(new PartyPage(Company,
            new(rows.Skip(request.Page.Offset).Take(request.Page.Limit).ToImmutableArray(), request.Page.Offset + request.Page.Limit < rows.Length))));
        var result = await new PartySearchReader().ReadAsync(source, query);
        Assert.AreSame(query, result.Query); Assert.HasCount(501, result.Items);
        Assert.AreEqual("0000", result.Items[0].Key.ExternalId); Assert.AreEqual("0500", result.Items[^1].Key.ExternalId);
        Assert.HasCount(2, source.Requests); Assert.AreEqual(500, source.Requests[1].Page.Offset);
        Assert.IsTrue(source.Requests.All(request => request.Company == Company && request.SearchText == "Beispiel" && request.Kind is null));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    public async Task RolesAndContactOwnersRemainDistinct(int role)
    {
        var rows = ImmutableArray.Create(Row("7"), Row("7", PartyKind.Supplier), Row("7", PartyKind.CustomerContact, "1"),
            Row("7", PartyKind.CustomerContact, "2"), Row("7", PartyKind.SupplierContact, "1"));
        PartyKind? kind = role == 0 ? null : (PartyKind)role;
        var source = new Source((request, _) => Task.FromResult(new PartyPage(Company,
            new(rows.Where(row => request.Kind is null || row.Key.Kind == request.Kind).ToImmutableArray(), false))));
        var result = await new PartySearchReader().ReadAsync(source, new(Company, "7", kind));
        Assert.AreEqual(rows.Count(row => kind is null || row.Key.Kind == kind), result.Items.Length);
        Assert.AreEqual(kind, source.Requests.Single().Kind);
    }

    [TestMethod]
    [DataRow("null-response")]
    [DataRow("foreign-product")]
    [DataRow("foreign-installation")]
    [DataRow("foreign-company")]
    [DataRow("null-page")]
    [DataRow("default-items")]
    [DataRow("null-party")]
    [DataRow("null-key")]
    [DataRow("foreign-party")]
    [DataRow("wrong-role")]
    [DataRow("blank-name")]
    [DataRow("oversized-page")]
    [DataRow("empty-continuation")]
    [DataRow("duplicate")]
    public async Task InvalidResponsesNeverBecomeSuccessfulEmptyOrPartialResults(string scenario)
    {
        var foreign = scenario switch
        {
            "foreign-product" => new CompanyKey(LexwareProductLine.FinOffice, Company.InstallationId, Company.ExternalId),
            "foreign-installation" => new CompanyKey(Company.ProductLine, "other", Company.ExternalId),
            _ => new CompanyKey(Company.ProductLine, Company.InstallationId, "other")
        };
        PartyPage response = scenario switch
        {
            "null-response" => null!,
            "foreign-product" or "foreign-installation" or "foreign-company" => new(foreign, new([], false)),
            "null-page" => new(Company, null!),
            "default-items" => new(Company, new(default, false)),
            "null-party" => new(Company, new([null!], false)),
            "null-key" => new(Company, new([Row("1") with { Key = null! }], false)),
            "foreign-party" => new(Company, new([Row("1") with { Key = new(foreign, PartyKind.Customer, "1") }], false)),
            "wrong-role" => new(Company, new([Row("1", PartyKind.Supplier)], false)),
            "blank-name" => new(Company, new([Row("1") with { DisplayName = " " }], false)),
            "oversized-page" => new(Company, new(Enumerable.Range(0, 501).Select(id => Row(id.ToString())).ToImmutableArray(), false)),
            "empty-continuation" => new(Company, new([], true)),
            _ => new(Company, new([Row("1"), Row("1")], false))
        };
        var source = new Source((_, _) => Task.FromResult(response));
        var error = await Assert.ThrowsExactlyAsync<PartySearchException>(() => new PartySearchReader().ReadAsync(source, new(Company, "private-search", PartyKind.Customer)));
        Assert.AreEqual(PartySearchFailure.InvalidResponse, error.Failure);
        Assert.DoesNotContain("private-search", error.ToString());
        Assert.HasCount(1, source.Requests);
    }

    [TestMethod]
    public async Task RepeatingSourcePageFailsRatherThanLoopingOrDroppingDuplicates()
    {
        var source = new Source((_, _) => Task.FromResult(new PartyPage(Company, new([Row("1")], true))));
        var error = await Assert.ThrowsExactlyAsync<PartySearchException>(() => new PartySearchReader().ReadAsync(source, new(Company, "Beispiel")));
        Assert.AreEqual(PartySearchFailure.InvalidResponse, error.Failure); Assert.HasCount(2, source.Requests);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task CapacityRequiresACompleteResultAndNeverSilentlyTruncates(bool hasMore)
    {
        var source = new Source((_, _) => Task.FromResult(new PartyPage(Company, new([Row("1"), Row("2")], hasMore))));
        if (hasMore)
        {
            var error = await Assert.ThrowsExactlyAsync<PartySearchException>(() => new PartySearchReader(2).ReadAsync(source, new(Company, "Beispiel")));
            Assert.AreEqual(PartySearchFailure.TooManyResults, error.Failure);
        }
        else Assert.HasCount(2, (await new PartySearchReader(2).ReadAsync(source, new(Company, "Beispiel"))).Items);
        Assert.HasCount(1, source.Requests); Assert.AreEqual(2, source.Requests[0].Page.Limit);
    }

    [TestMethod]
    public async Task EmptySuccessIsDistinctFromSourceFailureAndCancellation()
    {
        var empty = new Source((_, _) => Task.FromResult(new PartyPage(Company, new([], false))));
        Assert.IsEmpty((await new PartySearchReader().ReadAsync(empty, new(Company, "nothing"))).Items);
        var failure = new InvalidOperationException("private-source-failure");
        var broken = new Source((_, _) => throw failure);
        Assert.AreSame(failure, await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => new PartySearchReader().ReadAsync(broken, new(Company, "nothing"))));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        var unused = new Source((_, _) => throw new AssertFailedException());
        await Assert.ThrowsAsync<OperationCanceledException>(() => new PartySearchReader().ReadAsync(unused, new(Company, "nothing"), cancelled.Token));
        Assert.IsEmpty(unused.Requests);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task CancelledNonCooperativeRepliesAreNeverPublishedOrContinued(bool hasMore)
    {
        using var cancellation = new CancellationTokenSource();
        var reply = new TaskCompletionSource<PartyPage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var source = new Source((_, _) => reply.Task);
        var pending = new PartySearchReader().ReadAsync(source, new(Company, "Beispiel"), cancellation.Token);
        cancellation.Cancel(); Assert.IsFalse(pending.IsCompleted);
        reply.SetResult(new(Company, new([Row("1")], hasMore)));
        await Assert.ThrowsAsync<OperationCanceledException>(() => pending); Assert.HasCount(1, source.Requests);
    }

    [TestMethod]
    public async Task FailureAfterTheFirstPageDoesNotPublishPartialMatches()
    {
        var failure = new IOException("private-source-failure");
        var source = new Source((request, _) => request.Page.Offset == 0
            ? Task.FromResult(new PartyPage(Company, new([Row("1")], true))) : throw failure);
        Assert.AreSame(failure, await Assert.ThrowsExactlyAsync<IOException>(() =>
            new PartySearchReader().ReadAsync(source, new(Company, "Beispiel"))));
        Assert.HasCount(2, source.Requests);
    }

    [TestMethod]
    public async Task ConcurrentExplicitCompaniesNeverShareResultsOrSelectionState()
    {
        var other = new CompanyKey(LexwareProductLine.FinOffice, "fixture", "1");
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = new Source(async (request, _) =>
        {
            await release.Task;
            return new(request.Company, new([Row("7")], false));
        });
        var second = new Source((request, _) => Task.FromResult(new PartyPage(request.Company,
            new([Row("7") with { Key = new(other, PartyKind.Customer, "7") }], false))));
        var reader = new PartySearchReader();
        var pending = reader.ReadAsync(first, new(Company, "Beispiel"));
        var result = await reader.ReadAsync(second, new(other, "Beispiel"));
        Assert.AreEqual(other, result.Items.Single().Key.Company);
        release.SetResult();
        Assert.AreEqual(Company, (await pending).Items.Single().Key.Company);
        Assert.AreEqual(other, result.Query.Company);
    }

    [TestMethod]
    public void InvalidQueriesAreRejectedWithoutEchoingTheSearchAndDiagnosticStringsContainNoTerms()
    {
        foreach (var invalid in new[] { "", " ", "private\nsearch", new string('x', 201) })
        {
            var error = Assert.Throws<ArgumentException>(() => new PartySearchQuery(Company, invalid));
            Assert.DoesNotContain("private", error.ToString());
        }
        Assert.ThrowsExactly<ArgumentNullException>(() => new PartySearchQuery(null!, "query"));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new PartySearchQuery(Company, "query", (PartyKind)9));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new PartySearchReader(0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new PartySearchReader(PartySearchReader.MaximumResults + 1));
        var query = new PartySearchQuery(Company, "private-search");
        Assert.AreEqual(nameof(PartySearchQuery), query.ToString());
        Assert.AreEqual(nameof(PartySearchResult), new PartySearchResult(query, []).ToString());
    }

    private sealed class Source(Func<PartyQuery, CancellationToken, Task<PartyPage>> read) : IBusinessDirectory
    {
        internal List<PartyQuery> Requests { get; } = [];
        public Task<PartyPage> GetPartiesAsync(PartyQuery query, CancellationToken cancellationToken = default)
        { Requests.Add(query); return read(query, cancellationToken); }
        public Task<ImmutableArray<Company>> GetCompaniesAsync(CancellationToken cancellationToken = default) => throw new AssertFailedException("Search must retain the explicit company.");
        public Task<CapabilitySet> GetCapabilitiesAsync(CompanyKey company, CancellationToken cancellationToken = default) => throw new AssertFailedException();
        public Task<Party?> GetPartyAsync(PartyKey key, CancellationToken cancellationToken = default) => throw new AssertFailedException();
    }
}

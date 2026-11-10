using WawiCallPro.Core.PhoneNumbers;
using WawiCallPro.Core.Threading;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Interfaces.PhoneNumbers;
using WawiCallPro.Interfaces.Telephony;

namespace WawiCallPro.Core.Tests;

[TestClass]
public sealed class CallViewScopeTests
{
    private static readonly CompanyKey A = new(LexwareProductLine.Pro, "A", "01");
    private static readonly CompanyKey B = new(LexwareProductLine.Pro, "B", "01");
    private static CallSnapshot Call() => new() { Id = Guid.NewGuid(), Revision = 0, Direction = CallDirection.Incoming,
        State = CallState.Ringing, LocalAddress = new(NumberPresentation.Unknown), RemoteAddress = new(NumberPresentation.Available, "+49301234567"),
        StartedAtUtc = DateTimeOffset.UtcNow, StateChangedAtUtc = DateTimeOffset.UtcNow };
    private static CallerMatchDecision Choice(CompanyKey company)
    {
        var party = new PartyKey(company, PartyKind.Customer, "01");
        var lookup = new CallerLookupSnapshot { IsComplete = true, ParserVersion = "fixture", SearchedAtUtc = DateTimeOffset.UtcNow, SearchCompanies = [] };
        return new(CallerMatchDecisionKind.Manual, [new(new(company, "Firma", PhoneSearchCompanyState.Ready), [new(party, "Kunde", [])])], null,
            new(party, CallerMatchSelectionKind.Manual, false, lookup), lookup);
    }
    [TestMethod]
    public async Task CallsWithIdenticalPartyIdsHaveIndependentScopesAndNeverChangeTheMainCompany()
    {
        var factory = new CallViewScopeFactory();
        await using var first = factory.Create(Call()); await using var second = factory.Create(Call());
        await using var main = new WawiCallPro.Core.Companies.CompanyContext();
        await main.SelectAsync(new(A, "Hauptfirma"));
        await first.SelectAsync(Choice(B)); await second.SelectAsync(Choice(A));
        Assert.AreNotEqual(first.CallId, second.CallId);
        Assert.AreEqual(B, first.Current!.Party.Company); Assert.AreEqual(A, second.Current!.Party.Company);
        Assert.AreEqual(A, main.Current.Company!.Key);
    }
    [TestMethod]
    public async Task ReselectingTheSamePartyInvalidatesOldLoadsWithANewSelectionRevision()
    {
        await using var scope = new CallViewScopeFactory().Create(Call());
        var first = await scope.SelectAsync(Choice(A)); using var load = scope.BeginOperation();
        var next = await scope.SelectAsync(Choice(A));
        Assert.IsTrue(load.CancellationToken.IsCancellationRequested);
        Assert.IsTrue(next!.Revision > first!.Revision);
    }
    [TestMethod]
    public async Task PublicationRechecksCallPartyAndRevisionInsideTheQueuedUiCallback()
    {
        await using var scope = new CallViewScopeFactory().Create(Call());
        await scope.SelectAsync(Choice(A)); using var load = scope.BeginOperation();
        var queue = new QueuedDispatcher(); var published = false;
        var pending = load.PublishAsync(queue, () => published = true).AsTask();
        await scope.SelectAsync(Choice(B)); queue.Run();
        Assert.IsFalse(await pending); Assert.IsFalse(published);
        using var current = scope.BeginOperation();
        Assert.IsTrue(await current.PublishAsync(new InlineUiDispatcher(), () => published = true)); Assert.IsTrue(published);
    }
    [TestMethod]
    [DataRow(CallState.Ended)]
    [DataRow(CallState.Failed)]
    public async Task TerminalObservationsInvalidatePendingWorkAndCannotBeReopened(CallState terminal)
    {
        var call = Call(); await using var scope = new CallViewScopeFactory().Create(call);
        await scope.SelectAsync(Choice(A)); using var load = scope.BeginOperation();
        Assert.IsTrue(await scope.ObserveAsync(call with { Revision = 2, State = terminal }));
        Assert.IsTrue(load.CancellationToken.IsCancellationRequested);
        Assert.IsFalse(await scope.ObserveAsync(call with { Revision = 3, State = CallState.Connected }));
        Assert.ThrowsExactly<InvalidOperationException>(() => scope.BeginOperation());
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => scope.SelectAsync(Choice(B)));
    }
    [TestMethod]
    public async Task OldStateEventsAreIgnoredAndNewerNonterminalEventsKeepTheSelectedContext()
    {
        var call = Call(); await using var scope = new CallViewScopeFactory().Create(call);
        var context = await scope.SelectAsync(Choice(A));
        Assert.IsTrue(await scope.ObserveAsync(call with { Revision = 2, State = CallState.Connected }));
        Assert.IsFalse(await scope.ObserveAsync(call with { Revision = 1 }));
        Assert.AreEqual(context, scope.Current); Assert.AreEqual(CallState.Connected, scope.Call.State);
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => scope.ObserveAsync(call with { Id = Guid.NewGuid(), Revision = 3 }));
    }
    [TestMethod]
    public async Task ClearingSelectionCancelsWorkWithoutInventingACompany()
    {
        await using var scope = new CallViewScopeFactory().Create(Call());
        await scope.SelectAsync(Choice(A)); using var load = scope.BeginOperation();
        await scope.SelectAsync(Choice(A) with { Selection = null });
        Assert.IsNull(scope.Current); Assert.IsTrue(load.CancellationToken.IsCancellationRequested);
    }
    [TestMethod]
    public async Task DisposingBlocksQueuedPublicationAndIsIdempotent()
    {
        var scope = new CallViewScopeFactory().Create(Call());
        await scope.SelectAsync(Choice(A)); using var load = scope.BeginOperation();
        var queue = new QueuedDispatcher(); var published = false;
        var task = load.PublishAsync(queue, () => published = true).AsTask();
        await scope.DisposeAsync(); await scope.DisposeAsync(); queue.Run();
        Assert.IsFalse(await task); Assert.IsFalse(published);
        Assert.ThrowsExactly<ObjectDisposedException>(() => scope.BeginOperation());
    }
    private sealed class QueuedDispatcher : IUiDispatcher
    {
        private Action? _callback;
        private readonly TaskCompletionSource _completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ValueTask InvokeAsync(Action callback, CancellationToken cancellationToken = default)
        { _callback = callback; return new(_completed.Task); }
        internal void Run() { _callback!(); _completed.SetResult(); }
    }
}

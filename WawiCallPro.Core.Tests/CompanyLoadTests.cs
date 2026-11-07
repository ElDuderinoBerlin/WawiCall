using WawiCallPro.Core.Companies;
using WawiCallPro.Core.Threading;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Common;
using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Interfaces.Parties;

namespace WawiCallPro.Core.Tests;

[TestClass]
public sealed class CompanyLoadTests
{
    private static Company Company(string id = "1", string installation = "installation-A") =>
        new(new CompanyKey(LexwareProductLine.Pro, installation, id), id);

    [TestMethod]
    public async Task BorrowedInterfaceLoadsWithCapturedCompanyAndKeepsSuccessfulEmptyResults()
    {
        await using var context = new CompanyContext();
        var selection = await context.SelectAsync(Company());
        ICompanyContext borrowed = context;
        using var operation = borrowed.BeginOperation();
        PartyPage? bound = null;

        Assert.IsTrue(await operation.LoadAndPublishAsync(new InlineUiDispatcher(), (company, token) =>
        {
            Assert.AreEqual(selection.Company!.Key, company);
            Assert.AreEqual(operation.CancellationToken, token);
            return Task.FromResult(new PartyPage(company, new Page<Party>([], false)));
        }, page => bound = page));

        Assert.AreEqual(selection.Company!.Key, bound!.Company);
        Assert.IsEmpty(bound.Page.Items);
    }

    [TestMethod]
    [DataRow("same")]
    [DataRow("other")]
    [DataRow("other-installation")]
    [DataRow("deselect")]
    [DataRow("return-to-first")]
    public async Task NonCooperativeLateResultsCannotOverwriteANewerSelection(string change)
    {
        await using var context = new CompanyContext();
        await context.SelectAsync(Company());
        using var operation = context.BeginOperation();
        var source = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var bound = "new selection";
        var work = operation.LoadAndPublishAsync(new IgnoringCancellationDispatcher(), (_, _) => source.Task, value => bound = value);

        await context.SelectAsync(change switch
        {
            "same" => Company(),
            "other-installation" => Company(installation: "installation-B"),
            "deselect" => null,
            _ => Company("2")
        });
        if (change == "return-to-first") await context.SelectAsync(Company());
        Assert.IsTrue(operation.CancellationToken.IsCancellationRequested);
        Assert.IsFalse(work.IsCompleted, "Cancellation must not abandon upstream work.");
        source.SetResult("old selection");

        Assert.IsFalse(await work);
        Assert.AreEqual("new selection", bound);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task SwitchAfterQueueingRejectsBothDataAndErrorCallbacks(bool fail)
    {
        await using var context = new CompanyContext();
        using var ui = new QueuedSynchronizationContext();
        await context.SelectAsync(Company());
        using var operation = context.BeginOperation();
        var failure = new IOException("source failed");
        var callbacks = 0;
        var work = operation.LoadAndPublishAsync(new SynchronizationContextUiDispatcher(ui),
            (_, _) => fail ? Task.FromException<string>(failure) : Task.FromResult("data"),
            _ => callbacks++, _ => callbacks++);
        await ui.WaitForPostAsync();
        await context.SelectAsync(Company("2"));
        ui.RunNext();

        if (fail) Assert.AreSame(failure, await Assert.ThrowsExactlyAsync<IOException>(() => work));
        else await Assert.ThrowsAsync<OperationCanceledException>(() => work);
        Assert.AreEqual(0, callbacks);
    }

    [TestMethod]
    public async Task LateSourceFailureIsObservedWithoutReplacingTheNewCompanyErrorState()
    {
        await using var context = new CompanyContext();
        await context.SelectAsync(Company());
        using var operation = context.BeginOperation();
        var source = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var failure = new IOException("old company failed");
        var failedView = false;
        var work = operation.LoadAndPublishAsync(new IgnoringCancellationDispatcher(), (_, _) => source.Task,
            _ => Assert.Fail("Failed source published success."), _ => failedView = true);
        await context.SelectAsync(Company("2"));
        source.SetException(failure);

        Assert.AreSame(failure, await Assert.ThrowsExactlyAsync<IOException>(() => work));
        Assert.IsFalse(failedView);
    }

    [TestMethod]
    public async Task CurrentSourceFailureIsDispatchedAndRemainsObservable()
    {
        await using var context = new CompanyContext();
        using var ui = new QueuedSynchronizationContext();
        await context.SelectAsync(Company());
        using var operation = context.BeginOperation();
        var failure = new IOException("current source failed");
        Exception? shown = null;
        var work = operation.LoadAndPublishAsync(new SynchronizationContextUiDispatcher(ui),
            (_, _) => Task.FromException<string>(failure), _ => Assert.Fail(), error =>
            {
                Assert.AreSame(ui, SynchronizationContext.Current);
                shown = error;
            });
        await ui.WaitForPostAsync();
        Assert.IsFalse(work.IsCompleted);
        ui.RunNext();

        Assert.AreSame(failure, await Assert.ThrowsExactlyAsync<IOException>(() => work));
        Assert.AreSame(failure, shown);
    }

    [TestMethod]
    public async Task CancellationIsNotPublishedAsASourceFailure()
    {
        await using var context = new CompanyContext();
        await context.SelectAsync(Company());
        using var operation = context.BeginOperation();
        var work = operation.LoadAndPublishAsync(new InlineUiDispatcher(), async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return "unreachable";
        }, _ => Assert.Fail(), _ => Assert.Fail("Cancellation became an error view."));
        await context.SelectAsync(Company("2"));

        await Assert.ThrowsAsync<OperationCanceledException>(() => work);
    }

    [TestMethod]
    public async Task MainCompanySwitchLeavesIndependentCallViewAndExplicitReadsAlone()
    {
        await using var main = new CompanyContext();
        await using var callView = new CompanyContext();
        await main.SelectAsync(Company());
        await callView.SelectAsync(Company());
        using var mainOperation = main.BeginOperation();
        using var callOperation = callView.BeginOperation();
        var source = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var bound = "";
        var work = callOperation.LoadAndPublishAsync(new InlineUiDispatcher(), (company, _) =>
        {
            Assert.AreEqual(Company().Key, company);
            return source.Task;
        }, value => bound = value);

        await main.SelectAsync(Company("2"));
        Assert.IsTrue(mainOperation.CancellationToken.IsCancellationRequested);
        Assert.IsFalse(callOperation.CancellationToken.IsCancellationRequested);
        source.SetResult("call company 1");

        Assert.IsTrue(await work);
        Assert.AreEqual("call company 1", bound);
        Assert.AreEqual(Company().Key, callView.Current.Company!.Key);
    }

    [TestMethod]
    public async Task BindingFailureIsNotRepublishedAsALoadFailure()
    {
        await using var context = new CompanyContext();
        await context.SelectAsync(Company());
        using var operation = context.BeginOperation();
        var failure = new InvalidOperationException("binding failed");

        var observed = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => operation.LoadAndPublishAsync(
            new InlineUiDispatcher(), (_, _) => Task.FromResult(1), _ => throw failure, _ => Assert.Fail()));

        Assert.AreSame(failure, observed);
    }

    [TestMethod]
    public async Task SourceAndErrorBindingFailuresBothRemainObservable()
    {
        await using var context = new CompanyContext();
        await context.SelectAsync(Company());
        using var operation = context.BeginOperation();
        var sourceFailure = new IOException("source failed");
        var bindingFailure = new InvalidOperationException("error binding failed");

        var observed = await Assert.ThrowsExactlyAsync<AggregateException>(() => operation.LoadAndPublishAsync(
            new InlineUiDispatcher(), (_, _) => Task.FromException<int>(sourceFailure), _ => Assert.Fail(), _ => throw bindingFailure));

        CollectionAssert.AreEqual(new Exception[] { sourceFailure, bindingFailure }, observed.InnerExceptions.ToArray());
    }

    [TestMethod]
    public async Task CallerCancellationBlocksPublicationEvenWithANonCancellingDispatcher()
    {
        await using var context = new CompanyContext();
        await context.SelectAsync(Company());
        using var cancellation = new CancellationTokenSource();
        using var operation = context.BeginOperation(cancellation.Token);
        var source = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var work = operation.LoadAndPublishAsync(new IgnoringCancellationDispatcher(), (_, _) => source.Task, _ => Assert.Fail());
        cancellation.Cancel();
        source.SetResult(1);

        await Assert.ThrowsAsync<OperationCanceledException>(() => work);
    }

    [TestMethod]
    public async Task ShutdownInvalidatesPublicationWithoutAbandoningTheAwaitedSource()
    {
        await using var context = new CompanyContext();
        await context.SelectAsync(Company());
        using var operation = context.BeginOperation();
        var source = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var work = operation.LoadAndPublishAsync(new IgnoringCancellationDispatcher(), (_, _) => source.Task, _ => Assert.Fail());
        await context.DisposeAsync();
        Assert.IsFalse(work.IsCompleted);
        source.SetResult(1);

        Assert.IsFalse(await work);
    }

    [TestMethod]
    public async Task CancelledOrDisposedOperationsDoNotStartAnotherSourceRead()
    {
        await using var context = new CompanyContext();
        await context.SelectAsync(Company());
        using var cancellation = new CancellationTokenSource();
        using var operation = context.BeginOperation(cancellation.Token);
        Task<int> Read(CompanyKey _, CancellationToken token) { Assert.Fail("Source should not start."); return Task.FromResult(0); }
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => operation.LoadAndPublishAsync(new InlineUiDispatcher(), Read, _ => Assert.Fail()));
        operation.Dispose();
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => operation.LoadAndPublishAsync(new InlineUiDispatcher(), Read, _ => Assert.Fail()));
    }

    private sealed class IgnoringCancellationDispatcher : IUiDispatcher
    {
        public ValueTask InvokeAsync(Action callback, CancellationToken cancellationToken = default)
        {
            callback();
            return ValueTask.CompletedTask;
        }
    }
}

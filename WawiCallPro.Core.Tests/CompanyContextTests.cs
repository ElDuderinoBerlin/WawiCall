using Microsoft.VisualStudio.TestTools.UnitTesting;
using WawiCallPro.Core.Companies;
using WawiCallPro.Core.Threading;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Hosting;

namespace WawiCallPro.Core.Tests;

[TestClass]
public sealed class CompanyContextTests
{
    private static Company Company(string id) => new(new CompanyKey(LexwareProductLine.Pro, "installation-A", id), id);

    [TestMethod]
    public async Task ReselectAndDeselectInvalidatePreviousOperations()
    {
        await using var context = new CompanyContext();
        Assert.ThrowsExactly<InvalidOperationException>(() => context.BeginOperation());
        var first = await context.SelectAsync(Company("1"));
        using var oldOperation = context.BeginOperation();
        var second = await context.SelectAsync(Company("1"));
        using var currentOperation = context.BeginOperation();
        Assert.IsTrue(oldOperation.CancellationToken.IsCancellationRequested);
        Assert.AreEqual(first.Revision + 1, second.Revision);
        Assert.IsFalse(currentOperation.CancellationToken.IsCancellationRequested);
        var empty = await context.SelectAsync(null);
        Assert.IsNull(empty.Company);
        Assert.IsTrue(currentOperation.CancellationToken.IsCancellationRequested);
        Assert.ThrowsExactly<InvalidOperationException>(() => context.BeginOperation());
    }

    [TestMethod]
    public async Task CallerCancellationDoesNotCancelOtherConsumers()
    {
        await using var context = new CompanyContext();
        await context.SelectAsync(Company("1"));
        using var cancellation = new CancellationTokenSource();
        using var first = context.BeginOperation(cancellation.Token);
        using var second = context.BeginOperation();
        cancellation.Cancel();
        Assert.IsTrue(first.CancellationToken.IsCancellationRequested);
        Assert.IsFalse(second.CancellationToken.IsCancellationRequested);
        first.Dispose();
        Assert.IsTrue(await second.PublishAsync(new InlineUiDispatcher(), () => { }));
    }

    [TestMethod]
    public async Task CompanySwitchAfterQueueingPreventsStaleBinding()
    {
        await using var context = new CompanyContext();
        using var ui = new QueuedSynchronizationContext();
        await context.SelectAsync(Company("1"));
        using var load = context.BeginOperation();
        var called = false;
        var publication = load.PublishAsync(new SynchronizationContextUiDispatcher(ui), () => called = true).AsTask();
        await ui.WaitForPostAsync();
        await context.SelectAsync(Company("2"));
        ui.RunNext();
        await Assert.ThrowsAsync<OperationCanceledException>(() => publication);
        Assert.IsFalse(called);
    }

    [TestMethod]
    public async Task RevisionGuardAlsoRejectsResultsFromCodeIgnoringCancellation()
    {
        await using var context = new CompanyContext();
        await context.SelectAsync(Company("1"));
        using var load = context.BeginOperation();
        await context.SelectAsync(Company("2"));
        var called = false;
        Assert.IsFalse(await load.PublishAsync(new NonCancellingDispatcher(), () => called = true));
        Assert.IsFalse(called);
    }

    [TestMethod]
    public async Task CancellationCallbacksDoNotBlockSelectionOnTheirCallingThread()
    {
        await using var context = new CompanyContext();
        await context.SelectAsync(Company("1"));
        using var load = context.BeginOperation();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var callback = load.CancellationToken.Register(() =>
        {
            entered.SetResult();
            release.Task.GetAwaiter().GetResult();
        });
        var selection = context.SelectAsync(Company("2"));
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsFalse(selection.IsCompleted);
            Assert.AreEqual("2", context.Current.Company!.Key.ExternalId);
        }
        finally
        {
            release.TrySetResult();
        }
        await selection.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [TestMethod]
    public async Task DisposeCancelsOperationsRejectsNewWorkAndSuppressesPublication()
    {
        var context = new CompanyContext();
        await context.SelectAsync(Company("1"));
        using var load = context.BeginOperation();
        await context.DisposeAsync();
        await context.DisposeAsync();
        Assert.IsTrue(load.CancellationToken.IsCancellationRequested);
        Assert.IsNull(context.Current.Company);
        Assert.ThrowsExactly<ObjectDisposedException>(() => context.BeginOperation());
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => context.SelectAsync(Company("2")));
        Assert.IsFalse(await load.PublishAsync(new NonCancellingDispatcher(), () => Assert.Fail("Disposed context published data.")));
    }

    [TestMethod]
    public async Task SelectionNotificationCarriesTheCapturedRevision()
    {
        await using var context = new CompanyContext();
        CompanySelection? observed = null;
        context.Changed += (_, args) => observed = args.Selection;
        var selected = await context.SelectAsync(Company("1"));
        Assert.AreEqual(selected, observed);
        Assert.AreEqual(selected, context.Current);
    }

    [TestMethod]
    public async Task FailingCancellationCallbackIsObservedWithoutLosingSelectionNotification()
    {
        await using var context = new CompanyContext();
        await context.SelectAsync(Company("1"));
        using var load = context.BeginOperation();
        using var callback = load.CancellationToken.Register(() => throw new InvalidOperationException("cancel callback failed"));
        CompanySelection? observed = null;
        context.Changed += (_, args) => observed = args.Selection;

        var failure = await Assert.ThrowsExactlyAsync<AggregateException>(() => context.SelectAsync(Company("2")));

        Assert.IsTrue(failure.Flatten().InnerExceptions.Any(error => error is InvalidOperationException));
        Assert.AreEqual("2", observed!.Company!.Key.ExternalId);
        Assert.AreEqual(observed, context.Current);
    }

    [TestMethod]
    public async Task ConcurrentDisposalCallsAwaitTheSameCancellationCompletion()
    {
        var context = new CompanyContext();
        await context.SelectAsync(Company("1"));
        using var load = context.BeginOperation();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var callback = load.CancellationToken.Register(() =>
        {
            entered.TrySetResult();
            release.Task.GetAwaiter().GetResult();
        });
        var first = context.DisposeAsync().AsTask();
        var second = context.DisposeAsync().AsTask();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsFalse(first.IsCompleted);
            Assert.IsFalse(second.IsCompleted);
        }
        finally
        {
            release.TrySetResult();
        }
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));
    }

    private sealed class NonCancellingDispatcher : IUiDispatcher
    {
        public ValueTask InvokeAsync(Action callback, CancellationToken cancellationToken = default)
        {
            callback();
            return ValueTask.CompletedTask;
        }
    }
}

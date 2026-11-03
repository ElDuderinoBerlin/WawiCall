using Microsoft.VisualStudio.TestTools.UnitTesting;
using WawiCallPro.Core.Threading;

namespace WawiCallPro.Core.Tests;

[TestClass]
public sealed class DispatcherTests
{
    [TestMethod]
    public async Task BackgroundCallerAwaitsCallbackOnThePumpingThread()
    {
        using var context = new QueuedSynchronizationContext();
        var dispatcher = new SynchronizationContextUiDispatcher(context);
        var callbackThread = 0;
        var operation = Task.Run(async () => await dispatcher.InvokeAsync(() => callbackThread = Environment.CurrentManagedThreadId));
        await context.WaitForPostAsync();
        Assert.IsFalse(operation.IsCompleted);
        var pumpThread = Environment.CurrentManagedThreadId;
        context.RunNext();
        await operation.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual(pumpThread, callbackThread);
    }

    [TestMethod]
    public async Task CancelledQueuedCallbackNeverRuns()
    {
        using var context = new QueuedSynchronizationContext();
        using var cancellation = new CancellationTokenSource();
        var called = false;
        var operation = new SynchronizationContextUiDispatcher(context).InvokeAsync(() => called = true, cancellation.Token).AsTask();
        await context.WaitForPostAsync();
        cancellation.Cancel();
        await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => operation);
        context.RunNext();
        Assert.IsFalse(called);
    }

    [TestMethod]
    public async Task CancellationAfterCallbackStartsStillAwaitsItsCompletion()
    {
        using var context = new QueuedSynchronizationContext();
        using var cancellation = new CancellationTokenSource();
        Task? operation = null;
        var returned = false;
        operation = new SynchronizationContextUiDispatcher(context).InvokeAsync(() =>
        {
            cancellation.Cancel();
            Assert.IsFalse(operation!.IsCompleted);
            returned = true;
        }, cancellation.Token).AsTask();
        await context.WaitForPostAsync();
        context.RunNext();
        await operation.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsTrue(returned);
    }

    [TestMethod]
    public async Task CallbackFailuresReachTheAwaitingCaller()
    {
        using var context = new QueuedSynchronizationContext();
        var failure = new InvalidOperationException("binding failed");
        var operation = new SynchronizationContextUiDispatcher(context).InvokeAsync(() => throw failure).AsTask();
        await context.WaitForPostAsync();
        context.RunNext();
        Assert.AreSame(failure, await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => operation));
        Assert.AreSame(failure, await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            new InlineUiDispatcher().InvokeAsync(() => throw failure).AsTask()));
    }

    [TestMethod]
    public async Task PreCancelledInlineAndQueuedWorkDoesNotStart()
    {
        using var context = new QueuedSynchronizationContext();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var called = false;
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            new InlineUiDispatcher().InvokeAsync(() => called = true, cancellation.Token).AsTask());
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            new SynchronizationContextUiDispatcher(context).InvokeAsync(() => called = true, cancellation.Token).AsTask());
        Assert.IsFalse(called);
    }

    [TestMethod]
    public async Task AlreadyOnUiContextRunsWithoutPosting()
    {
        using var context = new QueuedSynchronizationContext();
        var dispatcher = new SynchronizationContextUiDispatcher(context);
        var count = 0;
        var outer = dispatcher.InvokeAsync(() =>
        {
            var inner = dispatcher.InvokeAsync(() => count++);
            Assert.IsTrue(inner.IsCompletedSuccessfully);
            inner.GetAwaiter().GetResult();
        }).AsTask();
        await context.WaitForPostAsync();
        context.RunNext();
        await outer.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual(1, count);
    }

    [TestMethod]
    public void BaseSynchronizationContextCannotMasqueradeAsUiContext()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new SynchronizationContextUiDispatcher(new SynchronizationContext()));
    }
}

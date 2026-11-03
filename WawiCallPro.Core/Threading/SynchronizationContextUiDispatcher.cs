using WawiCallPro.Interfaces.Hosting;

namespace WawiCallPro.Core.Threading;

/// <summary>
/// Uses an explicitly supplied UI context. Construct on the UI thread after its message loop is available.
/// Queued work is cancellable; once a synchronous callback starts, completion waits for its return or failure.
/// </summary>
public sealed class SynchronizationContextUiDispatcher : IUiDispatcher
{
    private readonly SynchronizationContext _context;

    public SynchronizationContextUiDispatcher(SynchronizationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.GetType() == typeof(SynchronizationContext))
            throw new ArgumentException("A dispatcher requires a context bound to the UI thread.", nameof(context));
        _context = context;
    }

    public async ValueTask InvokeAsync(Action callback, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callback);
        cancellationToken.ThrowIfCancellationRequested();
        if (ReferenceEquals(SynchronizationContext.Current, _context))
        {
            callback();
            return;
        }

        var invocation = new Invocation(callback, cancellationToken);
        using var registration = cancellationToken.UnsafeRegister(static state => ((Invocation)state!).Cancel(), invocation);
        _context.Post(static state => ((Invocation)state!).Run(), invocation);
        await invocation.Completion.Task.ConfigureAwait(false);
    }

    private sealed class Invocation(Action callback, CancellationToken cancellationToken)
    {
        // Only the pending -> running/cancelled race decides whether user code may start.
        private int _state;
        internal TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal void Cancel()
        {
            if (Interlocked.CompareExchange(ref _state, 2, 0) == 0)
                Completion.TrySetCanceled(cancellationToken);
        }

        internal void Run()
        {
            if (cancellationToken.IsCancellationRequested)
            {
                Cancel();
                return;
            }
            if (Interlocked.CompareExchange(ref _state, 1, 0) != 0) return;
            try
            {
                callback();
                Completion.TrySetResult();
            }
            catch (Exception exception)
            {
                Completion.TrySetException(exception);
            }
        }
    }
}

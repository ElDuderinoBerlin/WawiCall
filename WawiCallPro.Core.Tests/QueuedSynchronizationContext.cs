using System.Collections.Concurrent;

namespace WawiCallPro.Core.Tests;

/// <summary>A deterministic UI queue for dispatcher races; it never creates a WinForms application.</summary>
internal sealed class QueuedSynchronizationContext : SynchronizationContext, IDisposable
{
    private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _queue = new();
    private readonly SemaphoreSlim _posted = new(0);

    public override void Post(SendOrPostCallback d, object? state)
    {
        _queue.Enqueue((d, state));
        _posted.Release();
    }

    internal async Task WaitForPostAsync()
    {
        if (!await _posted.WaitAsync(TimeSpan.FromSeconds(5)))
            throw new TimeoutException("No callback was queued.");
    }

    internal void RunNext()
    {
        if (!_queue.TryDequeue(out var work)) throw new InvalidOperationException("No queued callback.");
        var previous = Current;
        try
        {
            SetSynchronizationContext(this);
            work.Callback(work.State);
        }
        finally
        {
            SetSynchronizationContext(previous);
        }
    }

    public void Dispose() => _posted.Dispose();
}

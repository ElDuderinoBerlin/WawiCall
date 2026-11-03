using WawiCallPro.Interfaces.Hosting;

namespace WawiCallPro.Core.Companies;

/// <summary>One consumer-owned operation. Dispose after its awaited load/publication completes.</summary>
public sealed class CompanyOperation : IDisposable
{
    private readonly CompanyContext _context;
    private readonly CancellationTokenSource _cancellation;
    private int _disposed;

    internal CompanyOperation(CompanyContext context, CompanySelection selection, CancellationTokenSource cancellation)
    {
        _context = context;
        _cancellation = cancellation;
        Selection = selection;
        CancellationToken = cancellation.Token;
    }

    public CompanySelection Selection { get; }
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// Marshals a short binding callback and checks its selection inside that callback, not before queuing it.
    /// Returns false if superseded; cancellation and callback/dispatcher failures remain observable by the caller.
    /// </summary>
    public async ValueTask<bool> PublishAsync(IUiDispatcher dispatcher, Action callback)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(callback);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        var published = false;
        await dispatcher.InvokeAsync(() => published = _context.TryPublish(Selection, callback), CancellationToken)
            .ConfigureAwait(false);
        return published;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        try { _cancellation.Cancel(); }
        finally { _cancellation.Dispose(); }
    }
}

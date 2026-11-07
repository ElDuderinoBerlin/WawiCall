using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Interfaces.Companies;

namespace WawiCallPro.Core.Companies;

/// <summary>One consumer-owned operation. Dispose after its awaited load/publication completes.</summary>
public sealed class CompanyOperation : ICompanyOperation
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
        await dispatcher.InvokeAsync(() => published = _context.TryPublish(Selection, () =>
        {
            // Also protect caller cancellation/disposal when a dispatcher ignores its token.
            CancellationToken.ThrowIfCancellationRequested();
            callback();
        }), CancellationToken)
            .ConfigureAwait(false);
        return published;
    }

    /// <inheritdoc />
    public async Task<bool> LoadAndPublishAsync<T>(IUiDispatcher dispatcher,
        Func<CompanyKey, CancellationToken, Task<T>> load, Action<T> publish, Action<Exception>? publishFailure = null)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(load);
        ArgumentNullException.ThrowIfNull(publish);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        CancellationToken.ThrowIfCancellationRequested();
        T result;
        try
        {
            // Do not use WaitAsync(token): upstream work must finish before releasing plugin resources.
            result = await load(Selection.Company!.Key, CancellationToken).ConfigureAwait(false);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            if (publishFailure is not null)
            {
                try
                {
                    await PublishAsync(dispatcher, () => publishFailure(failure)).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested) { }
                catch (Exception publicationFailure)
                {
                    throw new AggregateException("Source load and error publication failed.", failure, publicationFailure);
                }
            }
            throw;
        }

        return await PublishAsync(dispatcher, () => publish(result)).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        try { _cancellation.Cancel(); }
        finally { _cancellation.Dispose(); }
    }
}

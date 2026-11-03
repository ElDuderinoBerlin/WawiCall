using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Hosting;

namespace WawiCallPro.Core.Companies;

/// <summary>
/// Owns selection revisions and cancels operations on replacement/shutdown. It does not own the tasks
/// started by consumers: the hosting service must await those tasks before disposing its dependencies.
/// </summary>
public sealed class CompanyContext : ICompanyContext, IAsyncDisposable
{
    private readonly object _gate = new();
    private CompanySelection _current = new(null, 0);
    private CancellationTokenSource _selectionCancellation = new();
    private bool _disposed;
    private Task? _disposeTask;

    public CompanySelection Current
    {
        get { lock (_gate) return _current; }
    }

    public event EventHandler<CompanySelectionChangedEventArgs>? Changed;

    /// <summary>
    /// Re-selecting the same company also invalidates previous work. Cancellation callbacks run outside
    /// the selection lock. Changed notifications can arrive out of order and carry their revision explicitly.
    /// </summary>
    public async Task<CompanySelection> SelectAsync(Company? company)
    {
        CancellationTokenSource previousCancellation;
        CompanySelection selection;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            selection = new CompanySelection(company, checked(_current.Revision + 1));
            previousCancellation = _selectionCancellation;
            _selectionCancellation = new CancellationTokenSource();
            _current = selection;
        }

        try
        {
            await CancelAndDisposeAsync(previousCancellation).ConfigureAwait(false);
        }
        finally
        {
            Changed?.Invoke(this, new CompanySelectionChangedEventArgs(selection));
        }
        return selection;
    }

    /// <summary>Captures the selection and links caller cancellation to that selection's lifetime.</summary>
    public CompanyOperation BeginOperation(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            cancellationToken.ThrowIfCancellationRequested();
            if (_current.Company is null) throw new InvalidOperationException("No company is selected.");
            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _selectionCancellation.Token);
            return new CompanyOperation(this, _current, cancellation);
        }
    }

    internal bool TryPublish(CompanySelection selection, Action callback)
    {
        lock (_gate)
        {
            if (_disposed || _current.Revision != selection.Revision) return false;
            // Keep the short, synchronous binding atomic with respect to selection changes.
            // Callers must not block or perform I/O inside this callback.
            callback();
            return true;
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_disposeTask is not null) return new ValueTask(_disposeTask);
            _disposed = true;
            _current = new CompanySelection(null, checked(_current.Revision + 1));
            // CancelAsync schedules registered callbacks asynchronously, including when called under the gate.
            _disposeTask = CancelAndDisposeAsync(_selectionCancellation);
            return new ValueTask(_disposeTask);
        }
    }

    private static async Task CancelAndDisposeAsync(CancellationTokenSource cancellation)
    {
        try
        {
            await cancellation.CancelAsync().ConfigureAwait(false);
        }
        finally
        {
            cancellation.Dispose();
        }
    }
}

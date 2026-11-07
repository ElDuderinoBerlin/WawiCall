using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.History;

namespace WawiCallPro.Interfaces.Hosting;

/// <summary>A monotonic revision distinguishes repeated selection of the same company as well as deselection.</summary>
public sealed record CompanySelection(Company? Company, long Revision);

/// <summary>The host owns selection. Consumers capture it before loading and check its revision before publishing.</summary>
public interface ICompanyContext
{
    CompanySelection Current { get; }
    event EventHandler<CompanySelectionChangedEventArgs>? Changed;

    /// <summary>Captures this view's selection and links caller cancellation without affecting other consumers.</summary>
    ICompanyOperation BeginOperation(CancellationToken cancellationToken = default);
}

public sealed class CompanySelectionChangedEventArgs(CompanySelection selection) : EventArgs
{
    public CompanySelection Selection { get; } = selection;
}

/// <summary>
/// Invokes a short synchronous callback on the host's UI context and completes after it has run.
/// Errors propagate; cancellation prevents queued callbacks from starting, but does not interrupt running callbacks.
/// The console executes callbacks without a WinForms dependency. Callers must await the returned ValueTask.
/// </summary>
public interface IUiDispatcher
{
    ValueTask InvokeAsync(Action callback, CancellationToken cancellationToken = default);
}

/// <summary>
/// Borrowed, typed host services supplied after Quantarix Initialize and before business operations.
/// Plugins must not dispose host services. Quantarix IPluginHost continues to supply the logger.
/// </summary>
public interface IWawiCallHostServices
{
    ICompanyContext Companies { get; }
    IUiDispatcher UiDispatcher { get; }
    ICallHistoryService History { get; }
}

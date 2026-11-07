using WawiCallPro.Interfaces.Companies;

namespace WawiCallPro.Interfaces.Hosting;

/// <summary>
/// A consumer-owned load scope tied to one selection revision. Dispose only after all source work and
/// publication have been awaited. Cancellation never permits abandoning source work that is still running.
/// </summary>
public interface ICompanyOperation : IDisposable
{
    CompanySelection Selection { get; }
    CancellationToken CancellationToken { get; }

    /// <summary>
    /// Checks the selection inside the dispatched callback. Returns false for a superseded selection;
    /// dispatcher cancellation and callback failures remain observable. Callbacks must be short and synchronous.
    /// </summary>
    ValueTask<bool> PublishAsync(IUiDispatcher dispatcher, Action callback);

    /// <summary>
    /// Awaits source work with the captured company and token, then publishes through the revision guard.
    /// Source failures are rethrown after optional guarded error publication; cancellation is never an error view.
    /// Publication failures are not reported as source failures. Both failures remain observable if error publication fails.
    /// </summary>
    Task<bool> LoadAndPublishAsync<T>(IUiDispatcher dispatcher,
        Func<CompanyKey, CancellationToken, Task<T>> load, Action<T> publish, Action<Exception>? publishFailure = null);
}

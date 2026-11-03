using WawiCallPro.Interfaces.Hosting;

namespace WawiCallPro.Core.Threading;

/// <summary>Console dispatcher: runs short callbacks on the caller's thread without creating a UI context.</summary>
public sealed class InlineUiDispatcher : IUiDispatcher
{
    public ValueTask InvokeAsync(Action callback, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callback);
        if (cancellationToken.IsCancellationRequested) return ValueTask.FromCanceled(cancellationToken);
        try
        {
            callback();
            return ValueTask.CompletedTask;
        }
        catch (Exception exception)
        {
            return ValueTask.FromException(exception);
        }
    }
}

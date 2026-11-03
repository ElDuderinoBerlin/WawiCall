namespace WawiCallPro.Utilities.Operations;

/// <summary>Retains the first fatal error in memory; callers never serialize the exception or its message.</summary>
public sealed class RuntimeFailureState
{
    private Exception? _failure;
    public Exception? Failure => Volatile.Read(ref _failure);
    public event EventHandler? Failed;

    public void Report(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (Interlocked.CompareExchange(ref _failure, exception, null) is null)
            Failed?.Invoke(this, EventArgs.Empty);
    }
}

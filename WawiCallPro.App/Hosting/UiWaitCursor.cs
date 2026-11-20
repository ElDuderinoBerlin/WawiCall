namespace WawiCallPro.App.Hosting;

/// <summary>Restores the calling control's cursor even when a browse operation is cancelled or fails.</summary>
internal sealed class UiWaitCursor : IDisposable
{
    private readonly Control _owner;
    private readonly bool _waiting;
    private readonly Cursor? _cursor;

    internal UiWaitCursor(Control owner)
    {
        _owner = owner;
        _waiting = owner.UseWaitCursor;
        _cursor = owner.Cursor;
        owner.UseWaitCursor = true;
        owner.Cursor = Cursors.WaitCursor;
    }

    public void Dispose()
    {
        if (_owner.IsDisposed) return;
        _owner.UseWaitCursor = _waiting;
        _owner.Cursor = _cursor;
    }
}

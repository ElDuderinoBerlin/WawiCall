using System.Runtime.CompilerServices;

namespace WawiCallPro.App.Hosting;

/// <summary>Restores the calling control's cursor even when a browse operation is cancelled or fails.</summary>
internal sealed class UiWaitCursor : IDisposable
{
    private static readonly ConditionalWeakTable<Control, CursorState> States = new();
    private readonly Control _owner;
    private readonly CursorState? _state;
    private bool _disposed;

    internal UiWaitCursor(Control owner)
    {
        _owner = (Control?)owner.FindForm() ?? owner;
        if (_owner.IsDisposed) return;
        _state = States.GetValue(_owner, control => new CursorState(control.UseWaitCursor, control.Cursor));
        _state.Leases++;
        try
        {
            _owner.UseWaitCursor = true;
            _owner.Cursor = Cursors.WaitCursor;
        }
        catch { Dispose(); throw; }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_state is null || --_state.Leases != 0) return;
        States.Remove(_owner);
        if (_owner.IsDisposed) return;
        _owner.UseWaitCursor = _state.Waiting;
        _owner.Cursor = _state.Cursor;
    }

    private sealed class CursorState(bool waiting, Cursor? cursor)
    {
        internal bool Waiting { get; } = waiting;
        internal Cursor? Cursor { get; } = cursor;
        internal int Leases;
    }
}

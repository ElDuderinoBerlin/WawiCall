using WawiCallPro.App.Hosting;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.App;

public partial class MainForm
{
    private IAppHotkeyFactory? _hotkeyFactory;
    private UserSettings? _hotkeySettings;
    private AppHotkeySession? _hotkeys;

    internal void ConfigureHotkeys(IAppHotkeyFactory factory, UserSettings settings)
    {
        _hotkeys?.Dispose();
        _hotkeyFactory = factory;
        _hotkeySettings = settings;
        if (IsHandleCreated) _hotkeys = factory.Create(Handle, settings, ExecuteHotkey);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (_hotkeyFactory is not null && _hotkeySettings is not null && !_closing)
            _hotkeys = _hotkeyFactory.Create(Handle, _hotkeySettings, ExecuteHotkey);
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        _hotkeys?.Dispose(); _hotkeys = null;
        base.OnHandleDestroyed(e);
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == AppHotkeySession.MessageId && _hotkeys?.TryDispatch(message.WParam, message.LParam) == true) return;
        base.WndProc(ref message);
    }

    private void ExecuteHotkey(AppHotkeyAction action)
    {
        if (_closing || !_loaded || IsDisposed) return;
        if (!ReferenceEquals(RestoreFromTray(), this)) return;
        if (action == AppHotkeyAction.OpenDial) Dial_Click(this, null!);
    }
}

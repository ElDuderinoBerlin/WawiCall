using DevExpress.XtraBars;

namespace WawiCallPro.App;

public partial class MainForm
{
    private bool _inTray;
    private FormWindowState _lastWindowState = FormWindowState.Normal;

    internal bool MoveToTray()
    {
        if (_closing || IsDisposed) return false;
        if (FindModalWindow() is not null)
        {
            connectionLabel.Text = "Bitte zuerst den geöffneten Dialog schließen.";
            return false;
        }
        if (!_inTray && WindowState != FormWindowState.Minimized) _lastWindowState = WindowState;
        if (!_backgroundRun)
        {
            tray.Icon = Icon ?? SystemIcons.Application;
            tray.Visible = true;
        }
        _inTray = true;
        Hide();
        return true;
    }

    /// <summary>One explicit restore path for the tray and a second launch; modal dialogs remain the usable target.</summary>
    internal Form? RestoreFromTray()
    {
        if (_closing || IsDisposed) return null;
        if (WindowState == FormWindowState.Minimized) WindowState = _lastWindowState;
        if (!Visible) Show();
        _inTray = false;
        tray.Visible = false;
        var target = FindModalWindow() ?? this;
        if (target.WindowState == FormWindowState.Minimized) target.WindowState = FormWindowState.Normal;
        if (!_backgroundRun) target.Activate();
        return target;
    }

    private Form? FindModalWindow()
    {
        Form owner = this;
        Form? modal = null;
        while (owner.OwnedForms.LastOrDefault(form => form.Modal && form.Visible && !form.IsDisposed) is { } child)
        { modal = child; owner = child; }
        return modal;
    }

    private void Tray_Show(object sender, ItemClickEventArgs e) => RestoreFromTray();
    private void Tray_History(object sender, ItemClickEventArgs e) { if (ReferenceEquals(RestoreFromTray(), this)) Navigate("history"); }
    private void Tray_Settings(object sender, ItemClickEventArgs e) { if (ReferenceEquals(RestoreFromTray(), this)) OpenSettings(); }
    private void Tray_Exit(object sender, ItemClickEventArgs e) { if (ReferenceEquals(RestoreFromTray(), this)) Close(); }
    private void Tray_MouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right || _closing || _backgroundRun) return;
        trayMenu.ShowPopup(Cursor.Position);
    }
    private void Tray_BeforePopup(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        trayMenu.MenuAppearance.Menu.Font = Font;
        trayMenu.MenuAppearance.Menu.Options.UseFont = true;
        e.Cancel = _closing;
        var available = !_closing && FindModalWindow() is null;
        trayHistoryItem.Enabled = traySettingsItem.Enabled = trayExitItem.Enabled = available;
    }
}

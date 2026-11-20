namespace WawiCallPro.App.Hosting;

internal readonly record struct WindowMonitor(string Name, Rectangle WorkingArea);

/// <summary>Native monitor selection and window DPI, replaceable without changing the user's display configuration.</summary>
internal interface IWindowDisplayEnvironment
{
    WindowMonitor GetCurrent(Form form);
    WindowMonitor Select(string deviceName, Rectangle bounds);
    int GetDpi(Form form);
    int PrepareRestore(Form form, WindowMonitor monitor);
}

internal sealed class WindowDisplayEnvironment : IWindowDisplayEnvironment
{
    public WindowMonitor GetCurrent(Form form)
    {
        var screen = Screen.FromControl(form);
        return new WindowMonitor(screen.DeviceName, screen.WorkingArea);
    }

    public WindowMonitor Select(string deviceName, Rectangle bounds)
    {
        var screen = Screen.AllScreens.FirstOrDefault(screen => screen.DeviceName == deviceName)
            ?? Screen.FromRectangle(bounds);
        return new WindowMonitor(screen.DeviceName, screen.WorkingArea);
    }

    public int GetDpi(Form form) => form.DeviceDpi;

    public int PrepareRestore(Form form, WindowMonitor monitor)
    {
        // Move first: WinForms processes the native DPI transition synchronously. Reading DeviceDpi
        // on the target avoids GetDpiForMonitor, which is unsuitable for a per-monitor-aware thread.
        var area = monitor.WorkingArea;
        form.MinimumSize = WindowWorkArea.LimitMinimum(form.MinimumSize, area);
        form.Bounds = WindowWorkArea.Fit(new Rectangle(area.Location, form.Size), form.MinimumSize, area);
        _ = form.Handle;
        return GetDpi(form);
    }
}

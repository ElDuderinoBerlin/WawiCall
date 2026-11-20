using System.Runtime.InteropServices;

namespace WawiCallPro.ComPrototype;

internal static class NativeProbe
{
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(WindowCallback callback, IntPtr state);
    private delegate bool WindowCallback(IntPtr window, IntPtr state);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("ole32.dll")]
    internal static extern int CoSuspendClassObjects();

    internal static int CountVisibleWindows()
    {
        var count = 0;
        EnumWindows((window, _) =>
        {
            GetWindowThreadProcessId(window, out var process);
            if (process == Environment.ProcessId && IsWindowVisible(window)) count++;
            return true;
        }, IntPtr.Zero);
        return count;
    }
}

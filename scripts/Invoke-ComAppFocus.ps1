param([long]$WindowHandle, [int]$OwnerProcessId)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$element = [System.Windows.Automation.AutomationElement]::FromHandle([IntPtr]$WindowHandle)
if ($element.Current.ProcessId -ne $OwnerProcessId -or !$element.Current.Name.StartsWith('WawiCallPro')) {
    throw 'The exact isolated application window was not found.'
}
$element.SetFocus()
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class ComAppFocusPreparation {
    [StructLayout(LayoutKind.Sequential)] struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] struct MouseInput { public int X, Y; public uint Data, Flags, Time; public UIntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)] struct Input { public uint Type; public MouseInput Mouse; }
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window, out Rect bounds);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("user32.dll")] static extern int GetSystemMetricsForDpi(int metric, uint dpi);
    [DllImport("user32.dll")] static extern IntPtr SendMessageTimeout(IntPtr window, uint message, IntPtr wparam, IntPtr lparam, uint flags, uint timeout, out IntPtr result);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(Point point);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll")] static extern uint SendInput(uint count, Input[] inputs, int size);
    public static void Focus(IntPtr window, int process) {
        if (GetForegroundWindow() == window) return;
        Rect bounds; Point previous;
        if (!GetWindowRect(window, out bounds) || !GetCursorPos(out previous) || bounds.Right - bounds.Left < 200)
            throw new InvalidOperationException("The isolated test window has no usable title bar.");
        uint dpi = GetDpiForWindow(window);
        Point point = new Point { X = bounds.Left + (bounds.Right - bounds.Left) / 2,
            Y = bounds.Top + GetSystemMetricsForDpi(33, dpi) + GetSystemMetricsForDpi(4, dpi) / 2 };
        IntPtr hit;
        var position = new IntPtr(unchecked((int)((uint)(ushort)point.X | ((uint)(ushort)point.Y << 16))));
        if (SendMessageTimeout(window, 0x84, IntPtr.Zero, position, 2, 1000, out hit) == IntPtr.Zero || hit.ToInt64() != 2)
            throw new InvalidOperationException("The exact test window title bar could not be verified.");
        // One initial test input only. Subsequent activation assertions receive no corrective input.
        if (!SetWindowPos(window, IntPtr.Zero, 0, 0, 0, 0, 0x13))
            throw new InvalidOperationException("The isolated test window could not be exposed.");
        uint owner; GetWindowThreadProcessId(WindowFromPoint(point), out owner);
        if (owner != process) throw new InvalidOperationException("The title bar is covered by another process.");
        try {
            if (!SetCursorPos(point.X, point.Y) || SendInput(2, new[] {
                new Input { Mouse = new MouseInput { Flags = 2 } },
                new Input { Mouse = new MouseInput { Flags = 4 } }
            }, Marshal.SizeOf(typeof(Input))) != 2) throw new InvalidOperationException("The initial test input was rejected.");
        }
        finally { SetCursorPos(previous.X, previous.Y); }
    }
}
'@
[ComAppFocusPreparation]::Focus([IntPtr]$WindowHandle, $OwnerProcessId)

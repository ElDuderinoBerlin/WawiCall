namespace WawiCallPro.Utilities.Configuration;

/// <summary>Optional global shortcuts; the supported set avoids Windows keys and the debugger's F12.</summary>
public static class GlobalHotkeySettings
{
    public static bool IsSupported(string? value) => value is null || GetFunctionKey(value) != 0;

    public static int GetFunctionKey(string? value)
    {
        for (var key = 1; key <= 11; key++)
            if (value == "Ctrl+Alt+F" + key) return key;
        return 0;
    }
}

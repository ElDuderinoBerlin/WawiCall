namespace WawiCallPro.Utilities.Configuration;

/// <summary>Optional global shortcuts; the supported set avoids Windows keys and the debugger's F12.</summary>
public static class GlobalHotkeySettings
{
    public static bool IsSupported(string? value) => value is null || TryGetRegistration(value, out _, out _);

    /// <summary>Gets the Windows virtual key and modifier flags from the invariant shortcut text.</summary>
    public static bool TryGetRegistration(string? value, out int key, out int modifiers)
    {
        var remaining = value.AsSpan();
        modifiers = 0;
        if (ConsumeModifier(ref remaining, "Ctrl+")) modifiers |= 0x0002;
        if (ConsumeModifier(ref remaining, "Alt+")) modifiers |= 0x0001;
        if (ConsumeModifier(ref remaining, "Shift+")) modifiers |= 0x0004;

        key = remaining switch
        {
            "F1" => 0x70, "F2" => 0x71, "F3" => 0x72, "F4" => 0x73,
            "F5" => 0x74, "F6" => 0x75, "F7" => 0x76, "F8" => 0x77,
            "F9" => 0x78, "F10" => 0x79, "F11" => 0x7A,
            _ => 0
        };
        return key != 0;
    }

    private static bool ConsumeModifier(ref ReadOnlySpan<char> value, string prefix)
    {
        if (!value.StartsWith(prefix, StringComparison.Ordinal)) return false;
        value = value[prefix.Length..];
        return true;
    }
}

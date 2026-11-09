using DevExpress.XtraEditors;

namespace WawiCallPro.App.Hosting;

internal static class UiButtonSizing
{
    internal static Size Apply(params SimpleButton[] buttons)
    {
        foreach (var button in buttons) button.MinimumSize = Size.Empty;
        var preferred = buttons.Select(button => button.GetPreferredSize(Size.Empty)).ToArray();
        var size = new Size(preferred.Max(value => value.Width) + buttons[0].LogicalToDeviceUnits(12),
            preferred.Max(value => value.Height));
        foreach (var button in buttons) button.MinimumSize = size;
        return size;
    }
}

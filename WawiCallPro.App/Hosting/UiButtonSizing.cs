using DevExpress.XtraEditors;

namespace WawiCallPro.App.Hosting;

internal static class UiButtonSizing
{
    internal static Size Apply(params SimpleButton[] buttons) => ToolbarButtonLayoutHelper.Apply(buttons);
    internal static void UseNativeHeight(SimpleButton button) => ToolbarButtonLayoutHelper.UseNativeHeight(button);
    internal static void Attach(Control owner) => ToolbarButtonLayoutHelper.Attach(owner);
}

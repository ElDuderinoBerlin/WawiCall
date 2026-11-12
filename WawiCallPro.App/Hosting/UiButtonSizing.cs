using DevExpress.XtraEditors;

namespace WawiCallPro.App.Hosting;

internal static class UiButtonSizing
{
    internal static Size Apply(params SimpleButton[] buttons) => ToolbarButtonLayoutHelper.Apply(buttons);
    internal static void Attach(Control owner) => ToolbarButtonLayoutHelper.Attach(owner);
}

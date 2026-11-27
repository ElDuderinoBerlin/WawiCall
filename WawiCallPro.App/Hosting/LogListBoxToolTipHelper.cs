using System.ComponentModel;
using DevExpress.Utils;
using DevExpress.XtraEditors;

namespace WawiCallPro.App.Hosting;

internal static class LogListBoxToolTipHelper
{
    private const int TextInset = 4;
    private const int ToolTipMaxWidth = 720;

    public static void Attach(ListBoxControl listLog, IContainer components)
    {
        ArgumentNullException.ThrowIfNull(listLog);
        ArgumentNullException.ThrowIfNull(components);

        var controller = new ToolTipController(components);

        controller.GetActiveObjectInfo += (_, e) => SetToolTipInfo(listLog, e);

        // Der allgemeine Control-Tooltip würde den dynamischen Tooltip der Logzeile überdecken.
        listLog.ToolTip = string.Empty;
        listLog.ToolTipTitle = string.Empty;
        listLog.ShowToolTips = true;
        listLog.ToolTipController = controller;
    }

    private static void SetToolTipInfo(
        ListBoxControl listLog,
        ToolTipControllerGetActiveObjectInfoEventArgs e)
    {
        if (e.SelectedControl != listLog)
            return;

        var index = listLog.IndexFromPoint(e.ControlMousePosition);
        if (index < 0)
            return;

        var item = listLog.Items[index];
        var htmlText = item is LogListBoxItem logItem
            ? logItem.HtmlText
            : item?.ToString() ?? string.Empty;
        var plainText = LogListBoxText.ToPlainText(htmlText);
        var itemBounds = listLog.GetItemRectangle(index);

        var details = (item as LogListBoxItem)?.GetDetails();
        if (details is null && !IsClipped(listLog, plainText, itemBounds))
            return;

        var toolTipText = details ?? plainText;
        var identity = new LogItemToolTipIdentity(index, item);
        var superTip = new SuperToolTip
        {
            MaxWidth = ScaleToDpi(listLog, ToolTipMaxWidth),
            AllowHtmlText = DefaultBoolean.False
        };
        superTip.Items.Add(new ToolTipItem { Text = toolTipText });

        e.Info = new ToolTipControlInfo(identity, toolTipText)
        {
            ObjectBounds = listLog.RectangleToScreen(itemBounds),
            SuperTip = superTip
        };
    }

    private static bool IsClipped(ListBoxControl listLog, string text, Rectangle itemBounds)
    {
        if (string.IsNullOrEmpty(text) || itemBounds.Width <= 0)
            return false;

        var availableWidth = Math.Max(
            0,
            itemBounds.Width - listLog.ItemPadding.Horizontal - ScaleToDpi(listLog, TextInset));
        if (availableWidth == 0)
            return true;

        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (lines.Length > 1)
            return true;

        using var graphics = listLog.CreateGraphics();
        var textSize = listLog.Appearance.CalcTextSize(graphics, text, int.MaxValue);
        return textSize.Width > availableWidth;
    }

    private static int ScaleToDpi(Control control, int value)
        => Math.Max(1, value * control.DeviceDpi / 96);

    private sealed record LogItemToolTipIdentity(int Index, object? Item);
}

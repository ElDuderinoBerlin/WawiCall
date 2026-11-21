namespace WawiCallPro.App.Views;

public partial class SettingsPage
{
    private bool _sizingSipColumns;

    private void SipLayout_Layout(object? sender, LayoutEventArgs e)
    {
        if (_sizingSipColumns || IsDisposed || sipLayout.ColumnStyles.Count != 3) return;
        _sizingSipColumns = true;
        try
        {
            var labelWidth = new[] { serverLabel, portLabel, transportLabel, usernameLabel, authUserLabel, passwordLabel }
                .Max(label => label.GetPreferredSize(Size.Empty).Width + label.Margin.Horizontal);
            var available = Math.Max(1, sipLayout.ClientSize.Width - sipLayout.Padding.Horizontal - labelWidth);
            // The final percentage column absorbs extra space; editors stay readable at each font/DPI.
            var preferred = (int)Math.Ceiling(Font.GetHeight(DeviceDpi) * 24) + LogicalToDeviceUnits(24);
            var width = Math.Min(available, preferred);
            if (Math.Abs(sipLayout.ColumnStyles[1].Width - width) >= 1) sipLayout.ColumnStyles[1].Width = width;
            var portWidth = Math.Min(width, (int)Math.Ceiling(Font.GetHeight(DeviceDpi) * 7) + LogicalToDeviceUnits(24));
            if (portEdit.Width != portWidth) portEdit.Width = portWidth;
        }
        finally { _sizingSipColumns = false; }
    }
}

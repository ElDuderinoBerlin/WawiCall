namespace WawiCallPro.App.Views;

public partial class SettingsPage
{
    private bool _sizingSipColumns;

    internal void FitSipViewport(bool limitedByMonitor)
    {
        if (tabs.SelectedTabPage != sipTab || sipScroll.ClientSize.Height <= 0) return;
        // The dialog first requests its full content height. Only a monitor-limited large-font layout needs scrolling.
        var overflow = sipLayout.GetPreferredSize(new(Math.Max(1, sipScroll.ClientSize.Width), 0)).Height > sipScroll.ClientSize.Height;
        var scrolling = limitedByMonitor && overflow;
        if (sipScroll.AutoScroll != scrolling) sipScroll.AutoScroll = scrolling;
        if (!scrolling && sipScroll.AutoScrollPosition != Point.Empty) sipScroll.AutoScrollPosition = Point.Empty;
    }

    private void SipLayout_Layout(object? sender, LayoutEventArgs e)
    {
        if (_sizingSipColumns || IsDisposed || sipLayout.ColumnStyles.Count != 3) return;
        _sizingSipColumns = true;
        try
        {
            var labelWidth = new[] { serverLabel, portLabel, transportLabel, usernameLabel, authUserLabel, passwordLabel, sipProfileLabel, sipProfileNameLabel }
                .Max(label => label.GetPreferredSize(Size.Empty).Width + label.Margin.Horizontal);
            if (sipLayout.ColumnStyles[0].SizeType != SizeType.Absolute) sipLayout.ColumnStyles[0].SizeType = SizeType.Absolute;
            if (Math.Abs(sipLayout.ColumnStyles[0].Width - labelWidth) >= 1) sipLayout.ColumnStyles[0].Width = labelWidth;
            if (sipProfilesLayout.ColumnStyles[0].SizeType != SizeType.Absolute) sipProfilesLayout.ColumnStyles[0].SizeType = SizeType.Absolute;
            if (Math.Abs(sipProfilesLayout.ColumnStyles[0].Width - labelWidth) >= 1) sipProfilesLayout.ColumnStyles[0].Width = labelWidth;
            var available = Math.Max(1, sipLayout.ClientSize.Width - sipLayout.Padding.Horizontal - labelWidth);
            // The final percentage column absorbs extra space; editors stay readable at each font/DPI.
            var preferred = (int)Math.Ceiling(Font.GetHeight(DeviceDpi) * 24) + LogicalToDeviceUnits(24);
            var width = Math.Min(available, preferred);
            if (Math.Abs(sipLayout.ColumnStyles[1].Width - width) >= 1) sipLayout.ColumnStyles[1].Width = width;
            if (Math.Abs(sipProfilesLayout.ColumnStyles[1].Width - width) >= 1) sipProfilesLayout.ColumnStyles[1].Width = width;
            var portWidth = Math.Min(width, (int)Math.Ceiling(Font.GetHeight(DeviceDpi) * 7) + LogicalToDeviceUnits(24));
            if (portEdit.Width != portWidth) portEdit.Width = portWidth;
        }
        finally { _sizingSipColumns = false; }
    }
}

namespace WawiCallPro.App.Views;

public partial class CallDetailsPage
{
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        UpdateContentExtent();
    }

    private void DetailsContent_SizeChanged(object? sender, EventArgs e) => UpdateContentExtent();

    private void UpdateContentExtent()
    {
        if (layout is null || tabs is null || IsDisposed) return;

        var width = Math.Max(1, layout.ClientSize.Width - layout.Padding.Horizontal);
        var height = layout.Padding.Vertical + tabs.Margin.Vertical + tabs.Font.Height * 6;
        foreach (Control control in layout.Controls)
        {
            if (ReferenceEquals(control, tabs) || !control.Visible) continue;
            height += control.GetPreferredSize(new Size(Math.Max(1, width - control.Margin.Horizontal), 0)).Height
                + control.Margin.Vertical;
        }

        var extent = new Size(0, height);
        if (AutoScrollMinSize != extent) AutoScrollMinSize = extent;
    }
}

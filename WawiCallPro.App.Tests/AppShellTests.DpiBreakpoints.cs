using WawiCallPro.App.Hosting;
using WawiCallPro.App.Views;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    [DataRow(96)]
    [DataRow(120)]
    [DataRow(144)]
    [DataRow(192)]
    public void ResponsiveBreakpointsUseLogicalWidthAndHeightAtEachDpi(int dpi)
    {
        foreach (var font in new[] { 9F, 10F, 16F, 24F })
        foreach (var breakpoint in new[] { 800, 1000 })
        {
            var expected = (int)Math.Ceiling(breakpoint * font / 10d * dpi / 96d);
            Assert.AreEqual(expected, DpiLayoutMetrics.FontThreshold(breakpoint, font, dpi));
            foreach (var logicalWidth in new[] { 600, 800, 1000, 1600, 2500 })
                Assert.AreEqual(logicalWidth < breakpoint * font / 10d,
                    logicalWidth * dpi / 96 < DpiLayoutMetrics.FontThreshold(breakpoint, font, dpi));
        }
        foreach (var height in new[] { 600, 899, 900, 1080 })
        {
            Assert.AreEqual(height < 900, DpiLayoutMetrics.MinimizeRibbon(height * dpi / 96, 16F, dpi));
            Assert.IsFalse(DpiLayoutMetrics.MinimizeRibbon(height * dpi / 96, 9F, dpi));
        }
    }

    [TestMethod]
    public Task DashboardReflowsOnFontChangeWithoutReplacingControls() => RunStaAsync(() =>
    {
        AppUiStartup.Apply();
        using var dashboard = new HomeDashboard();
        dashboard.Size = new Size(dashboard.LogicalToDeviceUnits(1400), dashboard.LogicalToDeviceUnits(900));
        var cards = Field<TableLayoutPanel>(dashboard, "cards");
        var body = Field<TableLayoutPanel>(dashboard, "body");
        var database = Field<DevExpress.XtraEditors.LabelControl>(dashboard, "databaseValue");
        database.Text = "Erhaltener Datenbankstatus";
        using var regular = new Font("Segoe UI", 10F);
        dashboard.Font = regular;
        // Font autoscaling also resizes the control; hold the available viewport constant.
        dashboard.Size = new Size(dashboard.LogicalToDeviceUnits(1400), dashboard.LogicalToDeviceUnits(900));
        Assert.AreEqual(1, body.RowCount); Assert.AreEqual(2, cards.ColumnCount);
        using var large = new Font("Segoe UI", 24F);
        dashboard.Font = large;
        dashboard.Size = new Size(dashboard.LogicalToDeviceUnits(1400), dashboard.LogicalToDeviceUnits(900));
        Assert.AreEqual(2, body.RowCount); Assert.AreEqual(1, cards.ColumnCount);
        Assert.AreSame(database, Field<DevExpress.XtraEditors.LabelControl>(dashboard, "databaseValue"));
        Assert.AreEqual("Erhaltener Datenbankstatus", database.Text);
        Assert.IsFalse(dashboard.Visible && dashboard.IsHandleCreated);
        return Task.CompletedTask;
    });
}

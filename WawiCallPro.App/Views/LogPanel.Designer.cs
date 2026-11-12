#nullable disable
namespace WawiCallPro.App.Views;
partial class LogPanel
{
    private System.ComponentModel.IContainer components;
    protected override void Dispose(bool disposing) { if (disposing && components != null) components.Dispose(); base.Dispose(disposing); }
    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        layout = new System.Windows.Forms.TableLayoutPanel();
        searchController = new WawiCallPro.App.Hosting.LogListBoxSearchController();
        listLog = new DevExpress.XtraEditors.ListBoxControl();
        refreshTimer = new System.Windows.Forms.Timer(components);
        SuspendLayout();
        layout.Dock = System.Windows.Forms.DockStyle.Fill;
        layout.ColumnCount = 1;
        layout.RowCount = 2;
        layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        searchController.Dock = System.Windows.Forms.DockStyle.Fill;
        searchController.Margin = new System.Windows.Forms.Padding(0);
        listLog.Name = "listLog";
        listLog.Dock = System.Windows.Forms.DockStyle.Fill;
        listLog.AllowHtmlDraw = DevExpress.Utils.DefaultBoolean.True;
        listLog.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended;
        listLog.KeyDown += Log_KeyDown;
        refreshTimer.Interval = 150;
        refreshTimer.Tick += Refresh_Tick;
        layout.Controls.Add(listLog, 0, 0);
        layout.Controls.Add(searchController, 0, 1);
        Controls.Add(layout);
        AutoScaleDimensions = new System.Drawing.SizeF(7F, 14F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        Size = new System.Drawing.Size(800, 240);
        Name = "LogPanel";
        ResumeLayout(false);
    }
    private System.Windows.Forms.TableLayoutPanel layout;
    private WawiCallPro.App.Hosting.LogListBoxSearchController searchController;
    private DevExpress.XtraEditors.ListBoxControl listLog;
    private System.Windows.Forms.Timer refreshTimer;
}

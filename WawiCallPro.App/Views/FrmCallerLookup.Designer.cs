#nullable disable

namespace WawiCallPro.App.Views;

partial class FrmCallerLookup
{
    private System.ComponentModel.IContainer components;
    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null) components.Dispose();
        base.Dispose(disposing);
    }
    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        layout = new System.Windows.Forms.TableLayoutPanel();
        callControls = new WawiCallPro.App.Views.CallControlPanel();
        callerPage = new WawiCallPro.App.Views.CallerLookupPage();
        statusManager = new DevExpress.XtraBars.BarManager(components);
        statusBar = new DevExpress.XtraBars.Bar();
        statusText = new DevExpress.XtraBars.BarStaticItem();
        ((System.ComponentModel.ISupportInitialize)statusManager).BeginInit();
        SuspendLayout();
        layout.Dock = System.Windows.Forms.DockStyle.Fill;
        layout.ColumnCount = 1;
        layout.RowCount = 2;
        layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
        callControls.Dock = System.Windows.Forms.DockStyle.Fill;
        callControls.Visible = false;
        callerPage.Dock = System.Windows.Forms.DockStyle.Fill;
        layout.Controls.Add(callControls, 0, 0);
        layout.Controls.Add(callerPage, 0, 1);
        Controls.Add(layout);
        statusManager.Bars.Add(statusBar);
        statusManager.Form = this;
        statusManager.Items.Add(statusText);
        statusManager.MaxItemId = 1;
        statusManager.StatusBar = statusBar;
        statusBar.BarName = "Statusleiste";
        statusBar.DockStyle = DevExpress.XtraBars.BarDockStyle.Bottom;
        statusBar.OptionsBar.AllowQuickCustomization = false;
        statusBar.OptionsBar.DrawBorder = true;
        statusBar.OptionsBar.DrawDragBorder = false;
        statusBar.OptionsBar.DrawSizeGrip = true;
        statusBar.OptionsBar.UseWholeRow = true;
        statusBar.ItemLinks.Add(statusText);
        statusText.Id = 0;
        statusText.Name = "statusText";
        statusText.AutoSize = DevExpress.XtraBars.BarStaticItemSize.Spring;
        statusText.ItemAppearance.Normal.TextOptions.WordWrap = DevExpress.Utils.WordWrap.NoWrap;
        ClientSize = new System.Drawing.Size(960, 640);
        MinimumSize = new System.Drawing.Size(600, 440);
        Text = "WawiCallPro – Anrufzuordnung";
        StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        AutoScaleDimensions = new System.Drawing.SizeF(7F, 14F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        FormClosing += Caller_FormClosing;
        FontChanged += Caller_FontChanged;
        Name = "FrmCallerLookup";
        ((System.ComponentModel.ISupportInitialize)statusManager).EndInit();
        ResumeLayout(false);
    }
    private System.Windows.Forms.TableLayoutPanel layout;
    private WawiCallPro.App.Views.CallerLookupPage callerPage;
    private WawiCallPro.App.Views.CallControlPanel callControls;
    private DevExpress.XtraBars.BarManager statusManager;
    private DevExpress.XtraBars.Bar statusBar;
    private DevExpress.XtraBars.BarStaticItem statusText;
}

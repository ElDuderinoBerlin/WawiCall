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
        callerPage = new WawiCallPro.App.Views.CallerLookupPage();
        SuspendLayout();
        layout.Dock = System.Windows.Forms.DockStyle.Fill;
        layout.ColumnCount = 1;
        layout.RowCount = 1;
        layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
        callerPage.Dock = System.Windows.Forms.DockStyle.Fill;
        layout.Controls.Add(callerPage, 0, 0);
        Controls.Add(layout);
        ClientSize = new System.Drawing.Size(960, 640);
        MinimumSize = new System.Drawing.Size(600, 440);
        Text = "WawiCallPro – Anrufzuordnung";
        StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        AutoScaleDimensions = new System.Drawing.SizeF(7F, 14F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        FormClosing += Caller_FormClosing;
        Name = "FrmCallerLookup";
        ResumeLayout(false);
    }
    private System.Windows.Forms.TableLayoutPanel layout;
    private WawiCallPro.App.Views.CallerLookupPage callerPage;
}

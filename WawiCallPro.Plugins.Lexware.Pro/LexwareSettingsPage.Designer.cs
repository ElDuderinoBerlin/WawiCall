#nullable disable
namespace WawiCallPro.Plugins.Lexware.Pro;

partial class LexwareSettingsPage
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
        companyPage = new WawiCallPro.Plugin.Windows.PluginCompanySettingsPage();
        connectionPage = new WawiCallPro.Plugins.Lexware.Pro.ProConnectionSettingsPage();
        SuspendLayout();
        layout.Dock = System.Windows.Forms.DockStyle.Top;
        layout.AutoSize = true;
        layout.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        layout.ColumnCount = 1;
        layout.RowCount = 2;
        layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        layout.Controls.Add(companyPage, 0, 0);
        layout.Controls.Add(connectionPage, 0, 1);
        companyPage.Name = "companyPage";
        companyPage.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        companyPage.Margin = new System.Windows.Forms.Padding(0);
        connectionPage.Name = "connectionPage";
        connectionPage.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        connectionPage.Margin = new System.Windows.Forms.Padding(0);
        Controls.Add(layout);
        AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        AutoSize = true;
        AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        Name = "LexwareSettingsPage";
        Size = new System.Drawing.Size(900, 680);
        ResumeLayout(false);
        PerformLayout();
    }
    private System.Windows.Forms.TableLayoutPanel layout;
    private WawiCallPro.Plugin.Windows.PluginCompanySettingsPage companyPage;
    private WawiCallPro.Plugins.Lexware.Pro.ProConnectionSettingsPage connectionPage;
}

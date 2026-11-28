#nullable disable
namespace WawiCallPro.Plugins.Lexware.Pro;

partial class LexwareSettingsPage
{
    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        layout = new System.Windows.Forms.TableLayoutPanel();
        companyPage = new WawiCallPro.Plugin.Windows.PluginCompanySettingsPage();
        connectionPage = new WawiCallPro.Plugins.Lexware.Pro.ProConnectionSettingsPage();
        layout.SuspendLayout();
        SuspendLayout();
        // 
        // layout
        // 
        layout.AutoSize = true;
        layout.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        layout.ColumnCount = 1;
        layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        layout.Controls.Add(companyPage, 0, 0);
        layout.Controls.Add(connectionPage, 0, 1);
        layout.Dock = System.Windows.Forms.DockStyle.Top;
        layout.Location = new System.Drawing.Point(0, 0);
        layout.Margin = new System.Windows.Forms.Padding(4);
        layout.Name = "layout";
        layout.RowCount = 2;
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle());
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle());
        layout.Size = new System.Drawing.Size(0, 266);
        layout.TabIndex = 0;
        // 
        // companyPage
        // 
        companyPage.Anchor = ((System.Windows.Forms.AnchorStyles)(System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right));
        companyPage.AutoSize = true;
        companyPage.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        companyPage.Location = new System.Drawing.Point(0, 0);
        companyPage.Margin = new System.Windows.Forms.Padding(0);
        companyPage.Name = "companyPage";
        companyPage.Size = new System.Drawing.Size(1, 58);
        companyPage.TabIndex = 0;
        // 
        // connectionPage
        // 
        connectionPage.Anchor = ((System.Windows.Forms.AnchorStyles)(System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right));
        connectionPage.AutoSize = true;
        connectionPage.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        connectionPage.Location = new System.Drawing.Point(0, 58);
        connectionPage.Margin = new System.Windows.Forms.Padding(0);
        connectionPage.Name = "connectionPage";
        connectionPage.Size = new System.Drawing.Size(1, 208);
        connectionPage.TabIndex = 1;
        // 
        // LexwareSettingsPage
        // 
        AutoScaleDimensions = new System.Drawing.SizeF(9F, 19F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        AutoSize = true;
        AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        Controls.Add(layout);
        Margin = new System.Windows.Forms.Padding(4);
        Size = new System.Drawing.Size(0, 266);
        layout.ResumeLayout(false);
        layout.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }
    private System.Windows.Forms.TableLayoutPanel layout;
    private WawiCallPro.Plugin.Windows.PluginCompanySettingsPage companyPage;
    private WawiCallPro.Plugins.Lexware.Pro.ProConnectionSettingsPage connectionPage;
}

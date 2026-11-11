#nullable disable

namespace WawiCallPro.App.Views;

partial class FrmSettings
{
    private System.ComponentModel.IContainer components;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposePage();
            if (components != null) components.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        layout = new System.Windows.Forms.TableLayoutPanel();
        settingsPage = new WawiCallPro.App.Views.SettingsPage();
        closeButton = new DevExpress.XtraEditors.SimpleButton();
        SuspendLayout();
        layout.Dock = System.Windows.Forms.DockStyle.Fill;
        layout.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        layout.ColumnCount = 1;
        layout.RowCount = 2;
        layout.Padding = new System.Windows.Forms.Padding(12);
        layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100.0F));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        layout.Padding = new System.Windows.Forms.Padding(6);
        layout.RowStyles[0].SizeType = System.Windows.Forms.SizeType.Percent;
        layout.RowStyles[0].Height = 100F;
        layout.Controls.Add(settingsPage, 0, 0);
        layout.Controls.Add(closeButton, 0, 1);
        settingsPage.Dock = System.Windows.Forms.DockStyle.Fill;
        closeButton.Name = "closeButton";
        closeButton.Text = "Schließen";
        closeButton.AutoSize = true;
        closeButton.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        closeButton.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
        closeButton.Click += Close_Click;
        closeButton.Anchor = System.Windows.Forms.AnchorStyles.Right;
        Controls.Add(layout);
        ClientSize = new System.Drawing.Size(980, 760);
        MinimumSize = new System.Drawing.Size(680, 580);
        StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        Text = "WawiCallPro – Einstellungen";
        FormClosing += Settings_FormClosing;
        AutoScaleDimensions = new System.Drawing.SizeF(7F, 14F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        Name = "FrmSettings";
        ResumeLayout(false);
    }
    private System.Windows.Forms.TableLayoutPanel layout;
    private WawiCallPro.App.Views.SettingsPage settingsPage;
    private DevExpress.XtraEditors.SimpleButton closeButton;
}

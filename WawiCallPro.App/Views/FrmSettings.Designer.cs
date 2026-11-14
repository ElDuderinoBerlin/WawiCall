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
        SuspendLayout();
        layout.Dock = System.Windows.Forms.DockStyle.Fill;
        layout.ColumnCount = 1;
        layout.RowCount = 1;
        layout.Margin = new System.Windows.Forms.Padding(0);
        layout.Padding = new System.Windows.Forms.Padding(0);
        layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
        layout.Controls.Add(settingsPage, 0, 0);
        settingsPage.Dock = System.Windows.Forms.DockStyle.Fill;
        settingsPage.Margin = new System.Windows.Forms.Padding(0);
        Controls.Add(layout);
        ClientSize = new System.Drawing.Size(980, 760);
        MinimumSize = new System.Drawing.Size(680, 580);
        StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        Text = "Einstellungen";
        FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
        FormClosing += Settings_FormClosing;
        AutoScaleDimensions = new System.Drawing.SizeF(7F, 14F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        Name = "FrmSettings";
        ResumeLayout(false);
    }
    private System.Windows.Forms.TableLayoutPanel layout;
    private WawiCallPro.App.Views.SettingsPage settingsPage;
}

#nullable disable

namespace WawiCallPro.App;

partial class MainForm
{
    private System.ComponentModel.IContainer components;
    private System.Windows.Forms.TableLayoutPanel contentLayout;
    private DevExpress.XtraEditors.LabelControl titleLabel;
    private DevExpress.XtraEditors.LabelControl connectionLabel;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null)
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        contentLayout = new System.Windows.Forms.TableLayoutPanel();
        titleLabel = new DevExpress.XtraEditors.LabelControl();
        connectionLabel = new DevExpress.XtraEditors.LabelControl();
        contentLayout.SuspendLayout();
        SuspendLayout();
        contentLayout.ColumnCount = 1;
        contentLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        contentLayout.Controls.Add(titleLabel, 0, 0);
        contentLayout.Controls.Add(connectionLabel, 0, 1);
        contentLayout.Dock = System.Windows.Forms.DockStyle.Fill;
        contentLayout.Name = "contentLayout";
        contentLayout.Padding = new System.Windows.Forms.Padding(24);
        contentLayout.RowCount = 3;
        contentLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        contentLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        contentLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
        contentLayout.TabIndex = 0;
        titleLabel.Appearance.FontStyleDelta = System.Drawing.FontStyle.Bold;
        titleLabel.Appearance.Options.UseFont = true;
        titleLabel.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.Vertical;
        titleLabel.Dock = System.Windows.Forms.DockStyle.Top;
        titleLabel.Margin = new System.Windows.Forms.Padding(0, 0, 0, 12);
        titleLabel.Name = "titleLabel";
        titleLabel.TabIndex = 0;
        titleLabel.Text = "WawiCallPro";
        connectionLabel.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.Vertical;
        connectionLabel.Dock = System.Windows.Forms.DockStyle.Top;
        connectionLabel.Margin = new System.Windows.Forms.Padding(0);
        connectionLabel.Name = "connectionLabel";
        connectionLabel.TabIndex = 1;
        connectionLabel.Text = "Keine Verbindung konfiguriert.";
        AutoScaleDimensions = new System.Drawing.SizeF(7F, 14F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        ClientSize = new System.Drawing.Size(720, 420);
        Controls.Add(contentLayout);
        MinimumSize = new System.Drawing.Size(360, 220);
        Name = "MainForm";
        StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
        Text = "WawiCallPro";
        Load += MainForm_Load;
        contentLayout.ResumeLayout(false);
        ResumeLayout(false);
    }
}

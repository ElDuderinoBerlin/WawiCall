#nullable disable

namespace WawiCallPro.App.Views;

partial class StartPage
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
        titleLabel = new DevExpress.XtraEditors.LabelControl();
        bodyLabel = new DevExpress.XtraEditors.LabelControl();
        statusLabel = new DevExpress.XtraEditors.LabelControl();
        settingsButton = new DevExpress.XtraEditors.SimpleButton();
        historyButton = new DevExpress.XtraEditors.SimpleButton();
        SuspendLayout();
        layout.Dock = System.Windows.Forms.DockStyle.Fill;
        layout.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        layout.ColumnCount = 2;
        layout.RowCount = 4;
        layout.Padding = new System.Windows.Forms.Padding(12);
        layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50.0F));
        layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50.0F));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        titleLabel.Name = "titleLabel";
        titleLabel.Text = "Willkommen bei WawiCallPro";
        titleLabel.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.Vertical;
        titleLabel.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        titleLabel.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
        titleLabel.Appearance.FontStyleDelta = System.Drawing.FontStyle.Bold;
        titleLabel.Appearance.Options.UseFont = true;
        layout.Controls.Add(titleLabel, 0, 0);
        layout.SetColumnSpan(titleLabel, 2);
        bodyLabel.Name = "bodyLabel";
        bodyLabel.Text = "Richten Sie Ihre Kommunikationsdatenbank ein und wählen Sie den gewünschten Arbeitsbereich.";
        bodyLabel.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.Vertical;
        bodyLabel.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        bodyLabel.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
        layout.Controls.Add(bodyLabel, 0, 1);
        layout.SetColumnSpan(bodyLabel, 2);
        statusLabel.Name = "statusLabel";
        statusLabel.Text = "Telefonie ist noch nicht verbunden.";
        statusLabel.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.Vertical;
        statusLabel.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        statusLabel.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
        layout.Controls.Add(statusLabel, 0, 2);
        layout.SetColumnSpan(statusLabel, 2);
        settingsButton.Name = "settingsButton";
        settingsButton.Text = "Einstellungen öffnen";
        settingsButton.AutoSize = true;
        settingsButton.Padding = new System.Windows.Forms.Padding(12, 6, 12, 6);
        settingsButton.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        settingsButton.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
        settingsButton.Click += Settings_Click;
        layout.Controls.Add(settingsButton, 0, 3);
        historyButton.Name = "historyButton";
        historyButton.Text = "Gesprächshistorie öffnen";
        historyButton.AutoSize = true;
        historyButton.Padding = new System.Windows.Forms.Padding(12, 6, 12, 6);
        historyButton.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        historyButton.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
        historyButton.Click += History_Click;
        layout.Controls.Add(historyButton, 1, 3);
        layout.Padding = new System.Windows.Forms.Padding(6);
        titleLabel.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
        bodyLabel.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
        statusLabel.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
        Controls.Add(layout);
        Size = new System.Drawing.Size(720, 500);
        AutoScaleDimensions = new System.Drawing.SizeF(7F, 14F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        Name = "StartPage";
        ResumeLayout(false);
    }
    private System.Windows.Forms.TableLayoutPanel layout;
    private DevExpress.XtraEditors.LabelControl titleLabel;
    private DevExpress.XtraEditors.LabelControl bodyLabel;
    private DevExpress.XtraEditors.LabelControl statusLabel;
    private DevExpress.XtraEditors.SimpleButton settingsButton;
    private DevExpress.XtraEditors.SimpleButton historyButton;
}

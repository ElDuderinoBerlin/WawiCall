#nullable disable

namespace WawiCallPro.App.Views;

partial class FrmToast
{
    private void InitializeComponent()
    {
        layout = new System.Windows.Forms.TableLayoutPanel();
        actionsLayout = new System.Windows.Forms.TableLayoutPanel();
        titleLabel = new DevExpress.XtraEditors.LabelControl();
        messageLabel = new DevExpress.XtraEditors.LabelControl();
        detailLabel = new DevExpress.XtraEditors.LabelControl();
        statusLabel = new DevExpress.XtraEditors.LabelControl();
        closeButton = new DevExpress.XtraEditors.SimpleButton();
        layout.SuspendLayout();
        SuspendLayout();
        layout.AutoSize = true;
        layout.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        layout.Dock = System.Windows.Forms.DockStyle.Fill;
        layout.Padding = new System.Windows.Forms.Padding(12, 8, 12, 8);
        layout.ColumnCount = 2;
        layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
        layout.RowCount = 5;
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        titleLabel.Name = "titleLabel";
        titleLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
        titleLabel.Appearance.FontStyleDelta = System.Drawing.FontStyle.Bold;
        titleLabel.Text = "Benachrichtigung";
        messageLabel.Name = "messageLabel";
        messageLabel.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        messageLabel.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.Vertical;
        messageLabel.Appearance.TextOptions.WordWrap = DevExpress.Utils.WordWrap.Wrap;
        messageLabel.Appearance.FontStyleDelta = System.Drawing.FontStyle.Bold;
        detailLabel.Name = "detailLabel";
        detailLabel.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        detailLabel.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.Vertical;
        detailLabel.Appearance.TextOptions.WordWrap = DevExpress.Utils.WordWrap.Wrap;
        statusLabel.Name = "statusLabel";
        statusLabel.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        statusLabel.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.Vertical;
        statusLabel.Appearance.TextOptions.WordWrap = DevExpress.Utils.WordWrap.Wrap;
        closeButton.Name = "closeButton";
        closeButton.Anchor = System.Windows.Forms.AnchorStyles.Right;
        closeButton.AutoSize = true;
        closeButton.Text = "×";
        closeButton.AccessibleName = "Toast schließen";
        closeButton.ToolTip = "Toast schließen";
        closeButton.Click += Close_Click;
        actionsLayout.Name = "actionsLayout";
        actionsLayout.AutoSize = true;
        actionsLayout.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        actionsLayout.Anchor = System.Windows.Forms.AnchorStyles.Left;
        actionsLayout.ColumnCount = 0;
        actionsLayout.RowCount = 1;
        actionsLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        actionsLayout.GrowStyle = System.Windows.Forms.TableLayoutPanelGrowStyle.AddColumns;
        layout.Controls.Add(titleLabel, 0, 0);
        layout.Controls.Add(closeButton, 1, 0);
        layout.Controls.Add(messageLabel, 0, 1);
        layout.SetColumnSpan(messageLabel, 2);
        layout.Controls.Add(detailLabel, 0, 2);
        layout.SetColumnSpan(detailLabel, 2);
        layout.Controls.Add(actionsLayout, 0, 3);
        layout.SetColumnSpan(actionsLayout, 2);
        layout.Controls.Add(statusLabel, 0, 4);
        layout.SetColumnSpan(statusLabel, 2);
        AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
        AutoSize = true;
        AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        ClientSize = new System.Drawing.Size(360, 120);
        MinimumSize = new System.Drawing.Size(360, 0);
        FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
        ControlBox = false;
        ShowInTaskbar = false;
        StartPosition = System.Windows.Forms.FormStartPosition.Manual;
        TopMost = true;
        Controls.Add(layout);
        layout.ResumeLayout(false);
        layout.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }

    private System.Windows.Forms.TableLayoutPanel layout;
    private System.Windows.Forms.TableLayoutPanel actionsLayout;
    private DevExpress.XtraEditors.LabelControl titleLabel;
    private DevExpress.XtraEditors.LabelControl messageLabel;
    private DevExpress.XtraEditors.LabelControl detailLabel;
    private DevExpress.XtraEditors.LabelControl statusLabel;
    private DevExpress.XtraEditors.SimpleButton closeButton;
}

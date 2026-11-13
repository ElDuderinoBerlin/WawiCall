#nullable disable
namespace WawiCallPro.App.Views;

partial class CallControlPanel
{
    private System.ComponentModel.IContainer components;
    protected override void Dispose(bool disposing)
    {
        if (disposing) { DisposePage(); if (components != null) components.Dispose(); }
        base.Dispose(disposing);
    }
    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        layout = new System.Windows.Forms.TableLayoutPanel();
        actions = new System.Windows.Forms.TableLayoutPanel();
        answerButton = new DevExpress.XtraEditors.SimpleButton();
        endButton = new DevExpress.XtraEditors.SimpleButton();
        holdButton = new DevExpress.XtraEditors.SimpleButton();
        statusLabel = new DevExpress.XtraEditors.LabelControl();
        SuspendLayout();
        layout.ColumnCount = 1;
        layout.RowCount = 2;
        layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        layout.AutoSize = true;
        layout.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        layout.Dock = System.Windows.Forms.DockStyle.Top;
        layout.Padding = new System.Windows.Forms.Padding(4);
        actions.ColumnCount = 4;
        actions.RowCount = 1;
        actions.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
        actions.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
        actions.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
        actions.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        actions.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        actions.AutoSize = true;
        actions.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        actions.Dock = System.Windows.Forms.DockStyle.Fill;
        actions.Margin = new System.Windows.Forms.Padding(0);
        answerButton.Name = "answerButton";
        answerButton.Text = "Annehmen";
        answerButton.Padding = new System.Windows.Forms.Padding(12, 6, 12, 6);
        answerButton.Margin = new System.Windows.Forms.Padding(4);
        answerButton.TabIndex = 0;
        answerButton.Click += Answer_Click;
        endButton.Name = "endButton";
        endButton.Text = "Auflegen";
        endButton.Padding = new System.Windows.Forms.Padding(12, 6, 12, 6);
        endButton.Margin = new System.Windows.Forms.Padding(4);
        endButton.TabIndex = 1;
        endButton.Click += End_Click;
        holdButton.Name = "holdButton";
        holdButton.Text = "Fortsetzen";
        holdButton.Padding = new System.Windows.Forms.Padding(12, 6, 12, 6);
        holdButton.Margin = new System.Windows.Forms.Padding(4);
        holdButton.TabIndex = 2;
        holdButton.Click += Hold_Click;
        statusLabel.Name = "statusLabel";
        statusLabel.Text = "Kein Gespräch";
        statusLabel.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.Vertical;
        statusLabel.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        statusLabel.Margin = new System.Windows.Forms.Padding(4);
        actions.Controls.Add(answerButton, 0, 0);
        actions.Controls.Add(endButton, 1, 0);
        actions.Controls.Add(holdButton, 2, 0);
        layout.Controls.Add(actions, 0, 0);
        layout.Controls.Add(statusLabel, 0, 1);
        Controls.Add(layout);
        AutoSize = true;
        AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        AutoScaleDimensions = new System.Drawing.SizeF(7F, 14F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        Name = "CallControlPanel";
        Size = new System.Drawing.Size(540, 82);
        ResumeLayout(false);
        PerformLayout();
    }
    private System.Windows.Forms.TableLayoutPanel layout;
    private System.Windows.Forms.TableLayoutPanel actions;
    private DevExpress.XtraEditors.SimpleButton answerButton;
    private DevExpress.XtraEditors.SimpleButton endButton;
    private DevExpress.XtraEditors.SimpleButton holdButton;
    private DevExpress.XtraEditors.LabelControl statusLabel;
}

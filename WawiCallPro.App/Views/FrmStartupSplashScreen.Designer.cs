#nullable disable
namespace WawiCallPro.App.Views;

partial class FrmStartupSplashScreen
{
    private System.ComponentModel.IContainer components;
    private System.Windows.Forms.TableLayoutPanel rootLayout;
    private System.Windows.Forms.TableLayoutPanel contentLayout;
    private DevExpress.XtraEditors.SvgImageBox svgLogo;
    private DevExpress.XtraEditors.LabelControl lblTitle;
    private DevExpress.XtraEditors.LabelControl lblSubtitle;
    private DevExpress.XtraEditors.LabelControl lblStatus;
    private DevExpress.XtraEditors.LabelControl lblVersion;
    private Quantarix.DevExpressExtensions.Controls.PulsingProgressBarControl progressBar;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        rootLayout = new System.Windows.Forms.TableLayoutPanel();
        contentLayout = new System.Windows.Forms.TableLayoutPanel();
        svgLogo = new DevExpress.XtraEditors.SvgImageBox();
        lblTitle = new DevExpress.XtraEditors.LabelControl();
        lblSubtitle = new DevExpress.XtraEditors.LabelControl();
        lblStatus = new DevExpress.XtraEditors.LabelControl();
        lblVersion = new DevExpress.XtraEditors.LabelControl();
        progressBar = new Quantarix.DevExpressExtensions.Controls.PulsingProgressBarControl();
        ((System.ComponentModel.ISupportInitialize)svgLogo).BeginInit();
        rootLayout.SuspendLayout();
        contentLayout.SuspendLayout();
        SuspendLayout();
        rootLayout.Name = "rootLayout";
        rootLayout.Dock = System.Windows.Forms.DockStyle.Fill;
        rootLayout.Padding = new System.Windows.Forms.Padding(24);
        rootLayout.ColumnCount = 2;
        rootLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 28F));
        rootLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 72F));
        rootLayout.RowCount = 1;
        rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
        rootLayout.Controls.Add(svgLogo, 0, 0);
        rootLayout.Controls.Add(contentLayout, 1, 0);
        svgLogo.Name = "svgLogo";
        svgLogo.Dock = System.Windows.Forms.DockStyle.Fill;
        svgLogo.SizeMode = DevExpress.XtraEditors.SvgImageSizeMode.Zoom;
        svgLogo.Margin = new System.Windows.Forms.Padding(8, 8, 24, 8);
        contentLayout.Name = "contentLayout";
        contentLayout.Dock = System.Windows.Forms.DockStyle.Fill;
        contentLayout.ColumnCount = 1;
        contentLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        contentLayout.RowCount = 6;
        contentLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        contentLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        contentLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
        contentLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        contentLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        contentLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        contentLayout.Controls.Add(lblTitle, 0, 0);
        contentLayout.Controls.Add(lblSubtitle, 0, 1);
        contentLayout.Controls.Add(progressBar, 0, 3);
        contentLayout.Controls.Add(lblStatus, 0, 4);
        contentLayout.Controls.Add(lblVersion, 0, 5);
        lblTitle.Name = "lblTitle";
        lblTitle.Text = "WawiCallPro";
        lblTitle.Appearance.FontSizeDelta = 12;
        lblTitle.Appearance.FontStyleDelta = System.Drawing.FontStyle.Bold;
        lblTitle.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        lblTitle.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.Vertical;
        lblTitle.Margin = new System.Windows.Forms.Padding(0, 8, 0, 8);
        lblSubtitle.Name = "lblSubtitle";
        lblSubtitle.Text = "Telefonie und Warenwirtschaft";
        lblSubtitle.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        lblSubtitle.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.Vertical;
        lblSubtitle.Margin = new System.Windows.Forms.Padding(0, 0, 0, 12);
        lblStatus.Name = "lblStatus";
        lblStatus.Text = "Start wird vorbereitet …";
        lblStatus.Appearance.TextOptions.WordWrap = DevExpress.Utils.WordWrap.Wrap;
        lblStatus.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        lblStatus.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.Vertical;
        lblStatus.Margin = new System.Windows.Forms.Padding(0, 0, 0, 12);
        lblVersion.Name = "lblVersion";
        lblVersion.Text = "v…";
        lblVersion.Anchor = System.Windows.Forms.AnchorStyles.Right;
        lblVersion.Margin = new System.Windows.Forms.Padding(0, 0, 0, 0);
        progressBar.Name = "progressBar";
        progressBar.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        progressBar.AnimationDuration = 480;
        progressBar.Position = 0;
        progressBar.Size = new System.Drawing.Size(440, 64);
        progressBar.MinimumSize = new System.Drawing.Size(0, 64);
        progressBar.TrackThickness = 8;
        progressBar.TrackLeftInset = 0;
        progressBar.TrackRightInset = 0;
        progressBar.UseParentBackground = true;
        progressBar.UseRoundedCorners = true;
        progressBar.UseSkinColors = false;
        progressBar.Margin = new System.Windows.Forms.Padding(0);
        AutoScaleDimensions = new System.Drawing.SizeF(7F, 14F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        ClientSize = new System.Drawing.Size(720, 400);
        Controls.Add(rootLayout);
        FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
        ControlBox = false;
        ShowInTaskbar = false;
        StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
        Text = "WawiCallPro";
        Name = "FrmStartupSplashScreen";
        ((System.ComponentModel.ISupportInitialize)svgLogo).EndInit();
        contentLayout.ResumeLayout(false);
        contentLayout.PerformLayout();
        rootLayout.ResumeLayout(false);
        ResumeLayout(false);
    }
}

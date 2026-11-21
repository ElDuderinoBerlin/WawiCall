namespace WawiCallPro.App.Views;

partial class CompanySelector
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
        companyLabel = new DevExpress.XtraEditors.LabelControl();
        companyEdit = new DevExpress.XtraEditors.LookUpEdit();
        refreshButton = new DevExpress.XtraEditors.SimpleButton();
        statusLabel = new DevExpress.XtraEditors.LabelControl();
        ((System.ComponentModel.ISupportInitialize)companyEdit.Properties).BeginInit();
        SuspendLayout();
        layout.Dock = System.Windows.Forms.DockStyle.Fill;
        layout.AutoSize = true;
        layout.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        layout.ColumnCount = 1;
        layout.RowCount = 4;
        layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        layout.Padding = new System.Windows.Forms.Padding(6);
        layout.Controls.Add(companyLabel, 0, 0);
        layout.Controls.Add(companyEdit, 0, 1);
        layout.Controls.Add(refreshButton, 0, 2);
        layout.Controls.Add(statusLabel, 0, 3);
        companyLabel.Text = "Mandant";
        companyLabel.Appearance.FontStyleDelta = System.Drawing.FontStyle.Bold;
        companyLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
        companyLabel.Margin = new System.Windows.Forms.Padding(3);
        companyEdit.Name = "companyEdit";
        companyEdit.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        companyEdit.Margin = new System.Windows.Forms.Padding(3);
        companyEdit.Enabled = false;
        companyEdit.Properties.DisplayMember = "DisplayName";
        companyEdit.Properties.ValueMember = "Key";
        companyEdit.Properties.Columns.Add(new DevExpress.XtraEditors.Controls.LookUpColumnInfo("DisplayName", "Mandant"));
        companyEdit.Properties.ShowHeader = false;
        companyEdit.Properties.ShowFooter = false;
        companyEdit.Properties.SearchMode = DevExpress.XtraEditors.Controls.SearchMode.AutoSearch;
        companyEdit.Properties.NullText = "Mandant auswählen";
        companyEdit.Properties.Buttons.Add(new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Combo));
        companyEdit.EditValueChanged += Company_Changed;
        refreshButton.Name = "refreshButton";
        refreshButton.Text = "Aktualisieren";
        refreshButton.ImageOptions.SvgImageSize = new System.Drawing.Size(16, 16);
        refreshButton.Anchor = System.Windows.Forms.AnchorStyles.Left;
        refreshButton.Margin = new System.Windows.Forms.Padding(3, 6, 3, 3);
        refreshButton.ToolTip = "Mandanten aus dem aktiven Warenwirtschaftsplugin neu laden.";
        refreshButton.Click += Refresh_Click;
        statusLabel.Name = "statusLabel";
        statusLabel.Text = "Mandanten noch nicht geladen.";
        statusLabel.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.Vertical;
        statusLabel.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        statusLabel.Margin = new System.Windows.Forms.Padding(3, 6, 3, 3);
        Controls.Add(layout);
        AutoSize = true;
        AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        AutoScaleDimensions = new System.Drawing.SizeF(7F, 14F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        Name = "CompanySelector";
        Size = new System.Drawing.Size(210, 140);
        ((System.ComponentModel.ISupportInitialize)companyEdit.Properties).EndInit();
        ResumeLayout(false);
        PerformLayout();
    }

    private System.Windows.Forms.TableLayoutPanel layout;
    private DevExpress.XtraEditors.LabelControl companyLabel;
    private DevExpress.XtraEditors.LookUpEdit companyEdit;
    private DevExpress.XtraEditors.SimpleButton refreshButton;
    private DevExpress.XtraEditors.LabelControl statusLabel;
}

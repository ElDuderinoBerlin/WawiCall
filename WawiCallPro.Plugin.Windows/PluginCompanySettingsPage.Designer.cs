#nullable disable
namespace WawiCallPro.Plugin.Windows;

partial class PluginCompanySettingsPage
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
        companyLabel = new DevExpress.XtraEditors.LabelControl();
        companyEdit = new DevExpress.XtraEditors.LookUpEdit();
        refreshButton = new DevExpress.XtraEditors.SimpleButton();
        clearButton = new DevExpress.XtraEditors.SimpleButton();
        statusLabel = new DevExpress.XtraEditors.LabelControl();
        ((System.ComponentModel.ISupportInitialize)companyEdit.Properties).BeginInit();
        SuspendLayout();
        layout.Dock = System.Windows.Forms.DockStyle.Top;
        layout.AutoSize = true;
        layout.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        layout.Padding = new System.Windows.Forms.Padding(12);
        layout.ColumnCount = 3;
        layout.RowCount = 3;
        layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
        layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 360F));
        layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        layout.Controls.Add(companyLabel, 0, 0);
        layout.Controls.Add(companyEdit, 1, 0);
        layout.Controls.Add(actions, 1, 1);
        layout.Controls.Add(statusLabel, 0, 2);
        layout.SetColumnSpan(statusLabel, 2);
        layout.Layout += CompanyLayout_Layout;
        companyLabel.Text = "Mandant";
        companyLabel.Name = "companyLabel";
        companyLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
        companyLabel.Margin = new System.Windows.Forms.Padding(4, 6, 12, 6);
        companyLabel.Appearance.FontStyleDelta = System.Drawing.FontStyle.Bold;
        companyLabel.Appearance.Options.UseFont = true;
        companyEdit.Name = "companyEdit";
        companyEdit.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        companyEdit.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
        companyEdit.Properties.DisplayMember = "DisplayName";
        companyEdit.Properties.ValueMember = "Key";
        companyEdit.Properties.ShowHeader = false;
        companyEdit.Properties.ShowFooter = false;
        companyEdit.Properties.NullText = "Mandant auswählen";
        companyEdit.Properties.SearchMode = DevExpress.XtraEditors.Controls.SearchMode.AutoSearch;
        companyEdit.Properties.AllowNullInput = DevExpress.Utils.DefaultBoolean.True;
        companyEdit.Properties.Columns.Add(new DevExpress.XtraEditors.Controls.LookUpColumnInfo("DisplayName", "Mandant"));
        companyEdit.EditValueChanged += Company_Changed;
        actions.AutoSize = true;
        actions.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        actions.Anchor = System.Windows.Forms.AnchorStyles.Left;
        actions.Margin = new System.Windows.Forms.Padding(0);
        actions.ColumnCount = 2;
        actions.RowCount = 1;
        actions.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
        actions.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
        actions.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        actions.Controls.Add(refreshButton, 0, 0);
        actions.Controls.Add(clearButton, 1, 0);
        refreshButton.Text = "Mandanten laden";
        refreshButton.Name = "refreshButton";
        refreshButton.AutoSize = true;
        refreshButton.Margin = new System.Windows.Forms.Padding(4);
        refreshButton.Click += Refresh_Click;
        clearButton.Text = "Auswahl aufheben";
        clearButton.Name = "clearButton";
        clearButton.AutoSize = true;
        clearButton.Margin = new System.Windows.Forms.Padding(4);
        clearButton.Click += Clear_Click;
        statusLabel.Name = "statusLabel";
        statusLabel.Text = "Mandanten werden nach dem Öffnen geladen.";
        statusLabel.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.Vertical;
        statusLabel.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        statusLabel.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
        Controls.Add(layout);
        AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        AutoSize = true;
        AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        Name = "PluginCompanySettingsPage";
        SizeChanged += CompanySize_Changed;
        Size = new System.Drawing.Size(760, 150);
        ((System.ComponentModel.ISupportInitialize)companyEdit.Properties).EndInit();
        ResumeLayout(false);
        PerformLayout();
    }
    private System.Windows.Forms.TableLayoutPanel layout;
    private System.Windows.Forms.TableLayoutPanel actions;
    private DevExpress.XtraEditors.LabelControl companyLabel;
    private DevExpress.XtraEditors.LookUpEdit companyEdit;
    private DevExpress.XtraEditors.SimpleButton refreshButton;
    private DevExpress.XtraEditors.SimpleButton clearButton;
    private DevExpress.XtraEditors.LabelControl statusLabel;
}

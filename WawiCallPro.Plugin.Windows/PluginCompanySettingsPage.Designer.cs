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
        companyGrid = new DevExpress.XtraGrid.GridControl();
        companyView = new DevExpress.XtraGrid.Views.Grid.GridView();
        nameColumn = new DevExpress.XtraGrid.Columns.GridColumn();
        refreshButton = new DevExpress.XtraEditors.SimpleButton();
        clearButton = new DevExpress.XtraEditors.SimpleButton();
        ((System.ComponentModel.ISupportInitialize)companyGrid).BeginInit();
        ((System.ComponentModel.ISupportInitialize)companyView).BeginInit();
        SuspendLayout();
        layout.Dock = System.Windows.Forms.DockStyle.Top;
        layout.AutoSize = true;
        layout.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        layout.Padding = new System.Windows.Forms.Padding(12);
        layout.ColumnCount = 2;
        layout.RowCount = 2;
        layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        layout.Controls.Add(companyLabel, 0, 0);
        layout.Controls.Add(actions, 1, 0);
        layout.Controls.Add(companyGrid, 0, 1);
        layout.SetColumnSpan(companyGrid, 2);
        companyLabel.Name = "companyLabel";
        companyLabel.Text = "Mandanten";
        companyLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
        companyLabel.Margin = new System.Windows.Forms.Padding(4, 6, 12, 6);
        companyLabel.Appearance.FontStyleDelta = System.Drawing.FontStyle.Bold;
        companyLabel.Appearance.Options.UseFont = true;
        companyGrid.Name = "companyGrid";
        companyGrid.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        companyGrid.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
        companyGrid.Size = new System.Drawing.Size(600, 160);
        companyGrid.MainView = companyView;
        companyGrid.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[] { companyView });
        companyView.GridControl = companyGrid;
        companyView.OptionsBehavior.Editable = false;
        companyView.OptionsView.ShowGroupPanel = false;
        companyView.OptionsView.ShowColumnHeaders = false;
        companyView.OptionsSelection.MultiSelect = true;
        companyView.OptionsSelection.MultiSelectMode = DevExpress.XtraGrid.Views.Grid.GridMultiSelectMode.CheckBoxRowSelect;
        companyView.OptionsSelection.CheckBoxSelectorField = "Selected";
        companyView.OptionsFind.AlwaysVisible = false;
        companyView.SelectionChanged += Company_Changed;
        nameColumn.FieldName = "Name";
        nameColumn.Caption = "Mandant";
        nameColumn.Visible = true;
        companyView.Columns.AddRange(new DevExpress.XtraGrid.Columns.GridColumn[] { nameColumn });
        actions.AutoSize = true;
        actions.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        actions.Anchor = System.Windows.Forms.AnchorStyles.Right;
        actions.Margin = new System.Windows.Forms.Padding(0);
        actions.ColumnCount = 2;
        actions.RowCount = 1;
        actions.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
        actions.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
        actions.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        actions.Controls.Add(refreshButton, 0, 0);
        actions.Controls.Add(clearButton, 1, 0);
        refreshButton.Text = "Aktualisieren";
        refreshButton.Name = "refreshButton";
        refreshButton.AutoSize = true;
        refreshButton.Margin = new System.Windows.Forms.Padding(4);
        refreshButton.Click += Refresh_Click;
        clearButton.Text = "Auswahl aufheben";
        clearButton.Name = "clearButton";
        clearButton.AutoSize = true;
        clearButton.Margin = new System.Windows.Forms.Padding(4);
        clearButton.Click += Clear_Click;
        Controls.Add(layout);
        AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        AutoSize = true;
        AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        Name = "PluginCompanySettingsPage";
        SizeChanged += CompanySize_Changed;
        FontChanged += CompanySize_Changed;
        Size = new System.Drawing.Size(760, 220);
        ((System.ComponentModel.ISupportInitialize)companyGrid).EndInit();
        ((System.ComponentModel.ISupportInitialize)companyView).EndInit();
        ResumeLayout(false);
        PerformLayout();
    }
    private System.Windows.Forms.TableLayoutPanel layout;
    private System.Windows.Forms.TableLayoutPanel actions;
    private DevExpress.XtraEditors.LabelControl companyLabel;
    private DevExpress.XtraGrid.GridControl companyGrid;
    private DevExpress.XtraGrid.Views.Grid.GridView companyView;
    private DevExpress.XtraGrid.Columns.GridColumn nameColumn;
    private DevExpress.XtraEditors.SimpleButton refreshButton;
    private DevExpress.XtraEditors.SimpleButton clearButton;
}

namespace WawiCallPro.App.Views;

public partial class FunctionPluginsPage
{
    protected override void Dispose(bool disposing)
    {
        if (disposing) DisposePage();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        layout = new System.Windows.Forms.TableLayoutPanel();
        pluginsGrid = new DevExpress.XtraGrid.GridControl();
        pluginsView = new DevExpress.XtraGrid.Views.Grid.GridView();
        nameColumn = new DevExpress.XtraGrid.Columns.GridColumn();
        versionColumn = new DevExpress.XtraGrid.Columns.GridColumn();
        executeButton = new DevExpress.XtraEditors.SimpleButton();
        ((System.ComponentModel.ISupportInitialize)pluginsGrid).BeginInit();
        ((System.ComponentModel.ISupportInitialize)pluginsView).BeginInit();
        layout.SuspendLayout();
        SuspendLayout();
        layout.ColumnCount = 1;
        layout.RowCount = 2;
        layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        layout.Dock = System.Windows.Forms.DockStyle.Fill;
        layout.Padding = new System.Windows.Forms.Padding(10);
        layout.Controls.Add(pluginsGrid, 0, 0);
        layout.Controls.Add(executeButton, 0, 1);
        pluginsGrid.Dock = System.Windows.Forms.DockStyle.Fill;
        pluginsGrid.AccessibleName = "Installierte Funktionsplugins";
        pluginsGrid.MainView = pluginsView;
        pluginsGrid.ViewCollection.Add(pluginsView);
        pluginsView.GridControl = pluginsGrid;
        pluginsView.Columns.AddRange(new DevExpress.XtraGrid.Columns.GridColumn[] { nameColumn, versionColumn });
        pluginsView.OptionsBehavior.AutoPopulateColumns = false;
        pluginsView.OptionsBehavior.Editable = false;
        pluginsView.OptionsCustomization.AllowGroup = false;
        pluginsView.OptionsSelection.MultiSelect = true;
        pluginsView.OptionsSelection.MultiSelectMode = DevExpress.XtraGrid.Views.Grid.GridMultiSelectMode.CheckBoxRowSelect;
        pluginsView.OptionsSelection.CheckBoxSelectorField = "Enabled";
        pluginsView.OptionsSelection.ResetSelectionClickOutsideCheckboxSelector = false;
        pluginsView.OptionsSelection.ShowCheckBoxSelectorChangesSelectionNavigation = DevExpress.Utils.DefaultBoolean.False;
        pluginsView.OptionsSelection.ShowCheckBoxSelectorInColumnHeader = DevExpress.Utils.DefaultBoolean.True;
        pluginsView.OptionsView.ShowGroupPanel = false;
        pluginsView.OptionsView.ShowIndicator = false;
        pluginsView.FocusedRowChanged += Plugins_FocusedRowChanged;
        pluginsView.SelectionChanged += Plugins_SelectionChanged;
        pluginsView.KeyDown += Plugins_KeyDown;
        nameColumn.Caption = "Funktionsplugin";
        nameColumn.FieldName = "Name";
        nameColumn.Visible = true;
        nameColumn.VisibleIndex = 0;
        nameColumn.Width = 280;
        versionColumn.Caption = "Version";
        versionColumn.FieldName = "Version";
        versionColumn.Visible = true;
        versionColumn.VisibleIndex = 1;
        versionColumn.Width = 90;
        executeButton.Anchor = System.Windows.Forms.AnchorStyles.Left;
        executeButton.Text = "Ausführen";
        executeButton.Click += Execute_Click;
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
        Controls.Add(layout);
        ((System.ComponentModel.ISupportInitialize)pluginsView).EndInit();
        ((System.ComponentModel.ISupportInitialize)pluginsGrid).EndInit();
        layout.ResumeLayout(false);
        ResumeLayout(false);
    }

    private System.Windows.Forms.TableLayoutPanel layout;
    private DevExpress.XtraGrid.GridControl pluginsGrid;
    private DevExpress.XtraGrid.Views.Grid.GridView pluginsView;
    private DevExpress.XtraGrid.Columns.GridColumn nameColumn;
    private DevExpress.XtraGrid.Columns.GridColumn versionColumn;
    private DevExpress.XtraEditors.SimpleButton executeButton;
}

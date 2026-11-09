#nullable disable

namespace WawiCallPro.App.Views;

partial class PluginPage
{
    private System.ComponentModel.IContainer components;
    private System.Windows.Forms.TableLayoutPanel layout;
    private System.Windows.Forms.TableLayoutPanel actions;
    private DevExpress.XtraEditors.LabelControl detailsLabel;
    private DevExpress.XtraEditors.LabelControl statusLabel;
    private DevExpress.XtraEditors.SimpleButton refreshButton;
    private DevExpress.XtraEditors.SimpleButton capabilitiesButton;
    private DevExpress.XtraEditors.SimpleButton activateButton;
    private DevExpress.XtraTab.XtraTabControl pluginTabs;
    private DevExpress.XtraTab.XtraTabPage installedTab;
    private DevExpress.XtraTab.XtraTabPage capabilitiesTab;
    private DevExpress.XtraGrid.GridControl catalogGrid;
    private DevExpress.XtraGrid.Views.Grid.GridView catalogView;
    private DevExpress.XtraGrid.Columns.GridColumn nameColumn;
    private DevExpress.XtraGrid.Columns.GridColumn versionColumn;
    private DevExpress.XtraGrid.Columns.GridColumn activeColumn;
    private DevExpress.XtraGrid.GridControl grid;
    private DevExpress.XtraGrid.Views.Grid.GridView view;

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
        detailsLabel = new DevExpress.XtraEditors.LabelControl();
        statusLabel = new DevExpress.XtraEditors.LabelControl();
        refreshButton = new DevExpress.XtraEditors.SimpleButton();
        capabilitiesButton = new DevExpress.XtraEditors.SimpleButton();
        activateButton = new DevExpress.XtraEditors.SimpleButton();
        pluginTabs = new DevExpress.XtraTab.XtraTabControl();
        installedTab = new DevExpress.XtraTab.XtraTabPage();
        capabilitiesTab = new DevExpress.XtraTab.XtraTabPage();
        catalogGrid = new DevExpress.XtraGrid.GridControl();
        catalogView = new DevExpress.XtraGrid.Views.Grid.GridView();
        nameColumn = new DevExpress.XtraGrid.Columns.GridColumn();
        versionColumn = new DevExpress.XtraGrid.Columns.GridColumn();
        activeColumn = new DevExpress.XtraGrid.Columns.GridColumn();
        grid = new DevExpress.XtraGrid.GridControl();
        view = new DevExpress.XtraGrid.Views.Grid.GridView();
        SuspendLayout();
        layout.Dock = System.Windows.Forms.DockStyle.Fill;
        layout.ColumnCount = 1;
        layout.RowCount = 4;
        layout.Padding = new System.Windows.Forms.Padding(8);
        layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        detailsLabel.Name = "detailsLabel";
        detailsLabel.Text = "Es ist immer nur ein Warenwirtschaftsplugin aktiv.";
        detailsLabel.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.Vertical;
        detailsLabel.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        detailsLabel.Margin = new System.Windows.Forms.Padding(3, 0, 3, 2);
        pluginTabs.Dock = System.Windows.Forms.DockStyle.Fill;
        pluginTabs.TabPages.AddRange(new DevExpress.XtraTab.XtraTabPage[] { installedTab, capabilitiesTab });
        installedTab.Text = "Installierte Plugins";
        capabilitiesTab.Text = "Fähigkeiten";
        installedTab.Controls.Add(catalogGrid);
        capabilitiesTab.Controls.Add(grid);
        catalogGrid.Dock = System.Windows.Forms.DockStyle.Fill;
        catalogGrid.MainView = catalogView;
        catalogGrid.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[] { catalogView });
        catalogView.GridControl = catalogGrid;
        catalogView.FocusedRowChanged += Catalog_FocusedRowChanged;
        catalogView.OptionsBehavior.Editable = false;
        catalogView.OptionsBehavior.AutoPopulateColumns = false;
        catalogView.OptionsView.ShowGroupPanel = false;
        catalogView.OptionsFind.AlwaysVisible = false;
        catalogView.Columns.AddRange(new DevExpress.XtraGrid.Columns.GridColumn[] { nameColumn, versionColumn, activeColumn });
        nameColumn.FieldName = "Plugin";
        nameColumn.Caption = "Plugin";
        nameColumn.Visible = true;
        nameColumn.VisibleIndex = 0;
        nameColumn.Width = 360;
        versionColumn.FieldName = "Version";
        versionColumn.Caption = "Version";
        versionColumn.Visible = true;
        versionColumn.VisibleIndex = 1;
        versionColumn.Width = 100;
        activeColumn.FieldName = "Status";
        activeColumn.Caption = "Status";
        activeColumn.Visible = true;
        activeColumn.VisibleIndex = 2;
        activeColumn.Width = 180;
        grid.Dock = System.Windows.Forms.DockStyle.Fill;
        grid.MainView = view;
        grid.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[] { view });
        view.GridControl = grid;
        view.OptionsBehavior.Editable = false;
        view.OptionsView.ShowGroupPanel = false;
        view.OptionsFind.AlwaysVisible = true;
        actions.AutoSize = true;
        actions.Dock = System.Windows.Forms.DockStyle.Fill;
        actions.ColumnCount = 3;
        actions.RowCount = 1;
        actions.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
        actions.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
        actions.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        actions.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        refreshButton.Name = "refreshButton";
        refreshButton.Text = "Liste aktualisieren";
        refreshButton.AutoSize = true;
        refreshButton.Click += Refresh_Click;
        activateButton.Name = "activateButton";
        activateButton.Text = "Plugin aktivieren";
        activateButton.AutoSize = true;
        activateButton.Click += Activate_Click;
        capabilitiesButton.Name = "capabilitiesButton";
        capabilitiesButton.Text = "Fähigkeiten prüfen";
        capabilitiesButton.AutoSize = true;
        capabilitiesButton.Anchor = System.Windows.Forms.AnchorStyles.Right;
        capabilitiesButton.Click += Capabilities_Click;
        actions.Controls.Add(refreshButton, 0, 0);
        actions.Controls.Add(activateButton, 1, 0);
        actions.Controls.Add(capabilitiesButton, 2, 0);
        statusLabel.Name = "statusLabel";
        statusLabel.Text = "Installiertes Plugin auswählen und aktivieren.";
        statusLabel.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.Vertical;
        statusLabel.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        statusLabel.Margin = new System.Windows.Forms.Padding(3, 2, 3, 0);
        layout.Controls.Add(detailsLabel, 0, 0);
        layout.Controls.Add(pluginTabs, 0, 1);
        layout.Controls.Add(actions, 0, 2);
        layout.Controls.Add(statusLabel, 0, 3);
        Controls.Add(layout);
        Size = new System.Drawing.Size(860, 540);
        AutoScaleDimensions = new System.Drawing.SizeF(7F, 14F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        Name = "PluginPage";
        ResumeLayout(false);
    }
}

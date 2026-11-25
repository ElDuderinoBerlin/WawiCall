#nullable disable
namespace WawiCallPro.App.Views;

partial class FrmRecordingInventory
{
    protected override void Dispose(bool disposing)
    {
        if (disposing) DisposePage();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        layout = new System.Windows.Forms.TableLayoutPanel();
        actions = new System.Windows.Forms.TableLayoutPanel();
        summaryLabel = new DevExpress.XtraEditors.LabelControl();
        hintLabel = new DevExpress.XtraEditors.LabelControl();
        directoryLabel = new DevExpress.XtraEditors.LabelControl();
        reviewGrid = new DevExpress.XtraGrid.GridControl();
        reviewView = new DevExpress.XtraGrid.Views.Grid.GridView();
        pathColumn = new DevExpress.XtraGrid.Columns.GridColumn();
        reasonColumn = new DevExpress.XtraGrid.Columns.GridColumn();
        refreshButton = new DevExpress.XtraEditors.SimpleButton();
        closeButton = new DevExpress.XtraEditors.SimpleButton();
        ((System.ComponentModel.ISupportInitialize)reviewGrid).BeginInit();
        ((System.ComponentModel.ISupportInitialize)reviewView).BeginInit();
        SuspendLayout();
        layout.SuspendLayout();
        actions.SuspendLayout();
        layout.ColumnCount = 1;
        layout.RowCount = 5;
        layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        layout.Dock = System.Windows.Forms.DockStyle.Fill;
        layout.Padding = new System.Windows.Forms.Padding(10);
        summaryLabel.Text = "Aufnahmeordner und lokaler Dateikatalog";
        summaryLabel.Appearance.FontStyleDelta = System.Drawing.FontStyle.Bold;
        summaryLabel.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.Vertical;
        summaryLabel.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        summaryLabel.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
        hintLabel.Text = "Dateien in dieser Liste können zu laufenden Aufnahmen oder anderen Arbeitsplätzen gehören.";
        hintLabel.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.Vertical;
        hintLabel.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        hintLabel.Margin = new System.Windows.Forms.Padding(4, 4, 4, 8);
        directoryLabel.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.Vertical;
        directoryLabel.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        directoryLabel.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
        reviewGrid.Dock = System.Windows.Forms.DockStyle.Fill;
        reviewGrid.AccessibleName = "Aufnahmedateien mit Prüfbedarf";
        reviewGrid.MainView = reviewView;
        reviewGrid.ViewCollection.Add(reviewView);
        reviewView.GridControl = reviewGrid;
        reviewView.Columns.AddRange(new DevExpress.XtraGrid.Columns.GridColumn[] { pathColumn, reasonColumn });
        reviewView.OptionsBehavior.AutoPopulateColumns = false;
        reviewView.OptionsBehavior.Editable = false;
        reviewView.OptionsView.ShowGroupPanel = false;
        reviewView.OptionsView.ShowIndicator = false;
        reviewView.OptionsFind.AlwaysVisible = true;
        reviewView.OptionsFind.FindNullPrompt = "Relativen Pfad oder Prüfgrund suchen …";
        reviewView.OptionsFind.ShowFindButton = false;
        pathColumn.Caption = "Relativer Pfad";
        pathColumn.FieldName = "RelativePath";
        pathColumn.Visible = true;
        pathColumn.VisibleIndex = 0;
        pathColumn.Width = 400;
        reasonColumn.Caption = "Prüfgrund";
        reasonColumn.FieldName = "Reason";
        reasonColumn.Visible = true;
        reasonColumn.VisibleIndex = 1;
        reasonColumn.Width = 300;
        actions.ColumnCount = 3;
        actions.RowCount = 1;
        actions.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
        actions.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        actions.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
        actions.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        actions.AutoSize = true;
        actions.Dock = System.Windows.Forms.DockStyle.Top;
        actions.Margin = new System.Windows.Forms.Padding(0, 4, 0, 0);
        refreshButton.Text = "Erneut prüfen";
        refreshButton.Click += Refresh_Click;
        closeButton.Text = "Schließen";
        closeButton.Click += Close_Click;
        actions.Controls.Add(refreshButton, 0, 0);
        actions.Controls.Add(closeButton, 2, 0);
        layout.Controls.Add(directoryLabel, 0, 0);
        layout.Controls.Add(summaryLabel, 0, 1);
        layout.Controls.Add(hintLabel, 0, 2);
        layout.Controls.Add(reviewGrid, 0, 3);
        layout.Controls.Add(actions, 0, 4);
        Controls.Add(layout);
        CancelButton = closeButton;
        AutoScaleDimensions = new System.Drawing.SizeF(7F, 14F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        ClientSize = new System.Drawing.Size(800, 460);
        MinimumSize = new System.Drawing.Size(530, 340);
        StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        Name = "FrmRecordingInventory";
        Text = "Aufnahmebestand prüfen";
        Shown += Inventory_Shown;
        FormClosing += Inventory_Closing;
        ((System.ComponentModel.ISupportInitialize)reviewView).EndInit();
        ((System.ComponentModel.ISupportInitialize)reviewGrid).EndInit();
        actions.ResumeLayout(false);
        layout.ResumeLayout(false);
        ResumeLayout(false);
    }

    private System.Windows.Forms.TableLayoutPanel layout;
    private System.Windows.Forms.TableLayoutPanel actions;
    private DevExpress.XtraEditors.LabelControl summaryLabel;
    private DevExpress.XtraEditors.LabelControl hintLabel;
    private DevExpress.XtraEditors.LabelControl directoryLabel;
    private DevExpress.XtraGrid.GridControl reviewGrid;
    private DevExpress.XtraGrid.Views.Grid.GridView reviewView;
    private DevExpress.XtraGrid.Columns.GridColumn pathColumn;
    private DevExpress.XtraGrid.Columns.GridColumn reasonColumn;
    private DevExpress.XtraEditors.SimpleButton refreshButton;
    private DevExpress.XtraEditors.SimpleButton closeButton;
}

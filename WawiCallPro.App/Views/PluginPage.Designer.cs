#nullable disable

namespace WawiCallPro.App.Views;

partial class PluginPage
{
    private System.ComponentModel.IContainer components;
    private System.Windows.Forms.TableLayoutPanel layout;
    private DevExpress.XtraEditors.LabelControl titleLabel;
    private DevExpress.XtraEditors.LabelControl detailsLabel;
    private DevExpress.XtraEditors.LabelControl statusLabel;
    private DevExpress.XtraEditors.SimpleButton refreshButton;
    private DevExpress.XtraEditors.SimpleButton capabilitiesButton;
    private DevExpress.XtraGrid.GridControl grid;
    private DevExpress.XtraGrid.Views.Grid.GridView view;

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
        detailsLabel = new DevExpress.XtraEditors.LabelControl();
        statusLabel = new DevExpress.XtraEditors.LabelControl();
        refreshButton = new DevExpress.XtraEditors.SimpleButton();
        capabilitiesButton = new DevExpress.XtraEditors.SimpleButton();
        grid = new DevExpress.XtraGrid.GridControl();
        view = new DevExpress.XtraGrid.Views.Grid.GridView();
        SuspendLayout();
        layout.Dock = System.Windows.Forms.DockStyle.Fill;
        layout.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        layout.ColumnCount = 1;
        layout.RowCount = 6;
        layout.Padding = new System.Windows.Forms.Padding(12);
        layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100.0F));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        titleLabel.Name = "titleLabel";
        titleLabel.Text = "Warenwirtschaftsplugin";
        titleLabel.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.Vertical;
        titleLabel.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        titleLabel.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
        titleLabel.Appearance.FontStyleDelta = System.Drawing.FontStyle.Bold;
        titleLabel.Appearance.Options.UseFont = true;
        detailsLabel.Name = "detailsLabel";
        detailsLabel.Text = "Pro Prozess ist genau ein Plugin vorgesehen. Das passende Plugin wird beim Programmstart geladen.";
        detailsLabel.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.Vertical;
        detailsLabel.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        detailsLabel.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
        statusLabel.Name = "statusLabel";
        statusLabel.Text = "Pluginstatus noch nicht gelesen.";
        statusLabel.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.Vertical;
        statusLabel.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        statusLabel.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
        refreshButton.Name = "refreshButton";
        refreshButton.Text = "Pluginstatus aktualisieren";
        refreshButton.AutoSize = true;
        refreshButton.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        refreshButton.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
        refreshButton.Click += Refresh_Click;
        capabilitiesButton.Name = "capabilitiesButton";
        capabilitiesButton.Text = "Fähigkeiten für den Mandanten prüfen";
        capabilitiesButton.AutoSize = true;
        capabilitiesButton.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        capabilitiesButton.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
        capabilitiesButton.Click += Capabilities_Click;
        layout.RowStyles[5].SizeType = System.Windows.Forms.SizeType.Percent;
        layout.RowStyles[5].Height = 100F;
        layout.Controls.Add(titleLabel, 0, 0);
        layout.Controls.Add(detailsLabel, 0, 1);
        layout.Controls.Add(refreshButton, 0, 2);
        layout.Controls.Add(capabilitiesButton, 0, 3);
        layout.Controls.Add(statusLabel, 0, 4);
        layout.Controls.Add(grid, 0, 5);
        grid.Dock = System.Windows.Forms.DockStyle.Fill;
        grid.MainView = view;
        grid.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[] { view });
        view.GridControl = grid;
        view.OptionsBehavior.Editable = false;
        view.OptionsView.ShowGroupPanel = false;
        view.OptionsFind.AlwaysVisible = true;
        Controls.Add(layout);
        Size = new System.Drawing.Size(720, 500);
        AutoScaleDimensions = new System.Drawing.SizeF(7F, 14F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        Name = "PluginPage";
        ResumeLayout(false);
    }
}

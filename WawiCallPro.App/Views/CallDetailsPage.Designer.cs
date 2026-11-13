#nullable disable
namespace WawiCallPro.App.Views;
partial class CallDetailsPage
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
        callLabel = new DevExpress.XtraEditors.LabelControl();
        callControls = new WawiCallPro.App.Views.CallControlPanel();
        selectionLabel = new DevExpress.XtraEditors.LabelControl();
        statusLabel = new DevExpress.XtraEditors.LabelControl();
        detailsLabel = new DevExpress.XtraEditors.LabelControl();
        chooseButton = new DevExpress.XtraEditors.SimpleButton();
        reloadButton = new DevExpress.XtraEditors.SimpleButton();
        tabs = new DevExpress.XtraTab.XtraTabControl();
        masterTab = new DevExpress.XtraTab.XtraTabPage();
        detailsScroll = new DevExpress.XtraEditors.XtraScrollableControl();
        SuspendLayout();
        layout.Dock = System.Windows.Forms.DockStyle.Fill;
        layout.Padding = new System.Windows.Forms.Padding(8);
        layout.ColumnCount = 1;
        layout.RowCount = 6;
        layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        callLabel.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.Vertical;
        callLabel.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        callLabel.Appearance.FontStyleDelta = System.Drawing.FontStyle.Bold;
        callLabel.Text = "Anruf";
        callControls.Dock = System.Windows.Forms.DockStyle.Fill;
        selectionLabel.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.Vertical;
        selectionLabel.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        selectionLabel.Text = "Noch keine Anrufzuordnung.";
        statusLabel.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.Vertical;
        statusLabel.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        statusLabel.Text = "Zuordnung wählen, um Details aus der passenden Firma nachzuladen.";
        detailsLabel.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.Vertical;
        detailsLabel.Dock = System.Windows.Forms.DockStyle.Top;
        detailsLabel.Text = "Noch keine Stammdaten geladen.";
        detailsLabel.Padding = new System.Windows.Forms.Padding(8);
        detailsScroll.Dock = System.Windows.Forms.DockStyle.Fill;
        detailsScroll.Controls.Add(detailsLabel);
        masterTab.Text = "Stammdaten der Auswahl";
        masterTab.Controls.Add(detailsScroll);
        tabs.Dock = System.Windows.Forms.DockStyle.Fill;
        tabs.TabPages.Add(masterTab);
        actions.AutoSize = true;
        actions.ColumnCount = 2;
        actions.RowCount = 1;
        actions.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
        actions.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
        actions.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        chooseButton.Text = "Zuordnung wählen";
        chooseButton.AutoSize = true;
        chooseButton.Padding = new System.Windows.Forms.Padding(12, 6, 12, 6);
        chooseButton.Click += Choose_Click;
        reloadButton.Text = "Details nachladen";
        reloadButton.AutoSize = true;
        reloadButton.Padding = new System.Windows.Forms.Padding(12, 6, 12, 6);
        reloadButton.Click += Reload_Click;
        actions.Controls.Add(chooseButton, 0, 0);
        actions.Controls.Add(reloadButton, 1, 0);
        layout.Controls.Add(callLabel, 0, 0);
        layout.Controls.Add(callControls, 0, 1);
        layout.Controls.Add(selectionLabel, 0, 2);
        layout.Controls.Add(statusLabel, 0, 3);
        layout.Controls.Add(tabs, 0, 4);
        layout.Controls.Add(actions, 0, 5);
        Controls.Add(layout);
        AutoScaleDimensions = new System.Drawing.SizeF(7F, 14F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        Name = "CallDetailsPage";
        Size = new System.Drawing.Size(900, 650);
        ResumeLayout(false);
    }
    private System.Windows.Forms.TableLayoutPanel layout;
    private System.Windows.Forms.TableLayoutPanel actions;
    private DevExpress.XtraEditors.LabelControl callLabel;
    private WawiCallPro.App.Views.CallControlPanel callControls;
    private DevExpress.XtraEditors.LabelControl selectionLabel;
    private DevExpress.XtraEditors.LabelControl statusLabel;
    private DevExpress.XtraEditors.LabelControl detailsLabel;
    private DevExpress.XtraEditors.SimpleButton chooseButton;
    private DevExpress.XtraEditors.SimpleButton reloadButton;
    private DevExpress.XtraTab.XtraTabControl tabs;
    private DevExpress.XtraTab.XtraTabPage masterTab;
    private DevExpress.XtraEditors.XtraScrollableControl detailsScroll;
}

#nullable disable

namespace WawiCallPro.App.Views;

partial class DatabasePage
{
    private System.ComponentModel.IContainer components;
    private System.Windows.Forms.TableLayoutPanel layout;
    private System.Windows.Forms.TableLayoutPanel actions;
    private DevExpress.XtraEditors.LabelControl titleLabel;
    private DevExpress.XtraEditors.LabelControl statusLabel;
    private DevExpress.XtraEditors.LabelControl detailsLabel;
    private DevExpress.XtraEditors.ProgressBarControl progressBar;
    private DevExpress.XtraEditors.SimpleButton statusButton;
    private DevExpress.XtraEditors.SimpleButton createButton;
    private DevExpress.XtraEditors.SimpleButton migrateButton;
    private DevExpress.XtraEditors.SimpleButton approveButton;
    private DevExpress.XtraEditors.SimpleButton backupButton;
    private DevExpress.XtraEditors.SimpleButton restoreButton;

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
        actions = new System.Windows.Forms.TableLayoutPanel();
        titleLabel = new DevExpress.XtraEditors.LabelControl();
        statusLabel = new DevExpress.XtraEditors.LabelControl();
        detailsLabel = new DevExpress.XtraEditors.LabelControl();
        progressBar = new DevExpress.XtraEditors.ProgressBarControl();
        statusButton = new DevExpress.XtraEditors.SimpleButton();
        createButton = new DevExpress.XtraEditors.SimpleButton();
        migrateButton = new DevExpress.XtraEditors.SimpleButton();
        approveButton = new DevExpress.XtraEditors.SimpleButton();
        backupButton = new DevExpress.XtraEditors.SimpleButton();
        restoreButton = new DevExpress.XtraEditors.SimpleButton();
        SuspendLayout();
        layout.Dock = System.Windows.Forms.DockStyle.Fill;
        layout.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        layout.ColumnCount = 1;
        layout.RowCount = 5;
        layout.Padding = new System.Windows.Forms.Padding(12);
        layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100.0F));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        actions.Dock = System.Windows.Forms.DockStyle.Fill;
        actions.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        actions.ColumnCount = 2;
        actions.RowCount = 3;
        actions.Padding = new System.Windows.Forms.Padding(12);
        actions.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50.0F));
        actions.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50.0F));
        actions.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        actions.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        actions.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        actions.AutoSize = true;
        actions.Dock = System.Windows.Forms.DockStyle.Top;
        actions.Padding = new System.Windows.Forms.Padding(0);
        statusButton.Name = "statusButton";
        statusButton.Text = "Status prüfen";
        statusButton.AutoSize = true;
        statusButton.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        statusButton.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
        statusButton.Click += Status_Click;
        actions.Controls.Add(statusButton, 0, 0);
        createButton.Name = "createButton";
        createButton.Text = "Datenbank anlegen";
        createButton.AutoSize = true;
        createButton.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        createButton.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
        createButton.Click += Create_Click;
        actions.Controls.Add(createButton, 1, 0);
        migrateButton.Name = "migrateButton";
        migrateButton.Text = "Schema aktualisieren";
        migrateButton.AutoSize = true;
        migrateButton.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        migrateButton.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
        migrateButton.Click += Migrate_Click;
        actions.Controls.Add(migrateButton, 0, 1);
        approveButton.Name = "approveButton";
        approveButton.Text = "Leeres Ziel freigeben";
        approveButton.AutoSize = true;
        approveButton.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        approveButton.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
        approveButton.Click += Approve_Click;
        actions.Controls.Add(approveButton, 1, 1);
        backupButton.Name = "backupButton";
        backupButton.Text = "SQLite sichern";
        backupButton.AutoSize = true;
        backupButton.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        backupButton.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
        backupButton.Click += Backup_Click;
        actions.Controls.Add(backupButton, 0, 2);
        restoreButton.Name = "restoreButton";
        restoreButton.Text = "SQLite wiederherstellen";
        restoreButton.AutoSize = true;
        restoreButton.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        restoreButton.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
        restoreButton.Click += Restore_Click;
        actions.Controls.Add(restoreButton, 1, 2);
        titleLabel.Name = "titleLabel";
        titleLabel.Text = "Kommunikationsdatenbank";
        titleLabel.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.Vertical;
        titleLabel.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        titleLabel.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
        titleLabel.Appearance.FontStyleDelta = System.Drawing.FontStyle.Bold;
        titleLabel.Appearance.Options.UseFont = true;
        statusLabel.Name = "statusLabel";
        statusLabel.Text = "Status noch nicht geprüft.";
        statusLabel.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.Vertical;
        statusLabel.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        statusLabel.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
        detailsLabel.Name = "detailsLabel";
        detailsLabel.Text = "Verwaltet die eigene Gesprächshistorie und den Wawi-Cache. Einstellungen bestimmen das Ziel beim nächsten Start.";
        detailsLabel.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.Vertical;
        detailsLabel.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        detailsLabel.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
        layout.Controls.Add(titleLabel, 0, 0);
        layout.Controls.Add(detailsLabel, 0, 1);
        layout.Controls.Add(actions, 0, 2);
        layout.Controls.Add(progressBar, 0, 3);
        layout.Controls.Add(statusLabel, 0, 4);
        progressBar.Dock = System.Windows.Forms.DockStyle.Top;
        Controls.Add(layout);
        Size = new System.Drawing.Size(720, 500);
        AutoScaleDimensions = new System.Drawing.SizeF(7F, 14F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        Name = "DatabasePage";
        ResumeLayout(false);
    }
}

#nullable disable
namespace WawiCallPro.App.Views;
partial class CallNotesPanel
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
        notesLabel = new DevExpress.XtraEditors.LabelControl();
        draftLabel = new DevExpress.XtraEditors.LabelControl();
        statusLabel = new DevExpress.XtraEditors.LabelControl();
        notesEdit = new DevExpress.XtraEditors.MemoEdit();
        draftEdit = new DevExpress.XtraEditors.MemoEdit();
        saveButton = new DevExpress.XtraEditors.SimpleButton();
        reloadButton = new DevExpress.XtraEditors.SimpleButton();
        discardButton = new DevExpress.XtraEditors.SimpleButton();
        SuspendLayout();
        layout.Dock = System.Windows.Forms.DockStyle.Fill;
        layout.Padding = new System.Windows.Forms.Padding(8);
        layout.ColumnCount = 1;
        layout.RowCount = 6;
        layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 60F));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 40F));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        notesLabel.Text = "Gespeicherte Notizen";
        notesLabel.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.Vertical;
        notesLabel.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        notesLabel.Appearance.FontStyleDelta = System.Drawing.FontStyle.Bold;
        draftLabel.Text = "Neue Gesprächsnotiz";
        draftLabel.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.Vertical;
        draftLabel.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        draftLabel.Appearance.FontStyleDelta = System.Drawing.FontStyle.Bold;
        statusLabel.Name = "statusLabel";
        statusLabel.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.Vertical;
        statusLabel.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        statusLabel.Text = "Notizen gehören zu diesem Gespräch, unabhängig von der Firmenzuordnung.";
        notesEdit.Name = "notesEdit";
        notesEdit.Dock = System.Windows.Forms.DockStyle.Fill;
        notesEdit.Properties.ReadOnly = true;
        notesEdit.Text = "Noch keine Gesprächsnotizen gespeichert.";
        notesEdit.TabStop = false;
        draftEdit.Name = "draftEdit";
        draftEdit.Dock = System.Windows.Forms.DockStyle.Fill;
        draftEdit.TabIndex = 0;
        draftEdit.EditValueChanged += Draft_Changed;
        actions.AutoSize = true;
        actions.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        actions.Dock = System.Windows.Forms.DockStyle.Fill;
        actions.ColumnCount = 4;
        actions.RowCount = 1;
        actions.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
        actions.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
        actions.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
        actions.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        actions.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        saveButton.Name = "saveButton";
        saveButton.Text = "Notiz speichern";
        saveButton.Padding = new System.Windows.Forms.Padding(12, 6, 12, 6);
        saveButton.Margin = new System.Windows.Forms.Padding(4);
        saveButton.Enabled = false;
        saveButton.TabIndex = 1;
        saveButton.Click += Save_Click;
        discardButton.Name = "discardButton";
        discardButton.Text = "Entwurf verwerfen";
        discardButton.Padding = new System.Windows.Forms.Padding(12, 6, 12, 6);
        discardButton.Margin = new System.Windows.Forms.Padding(4);
        discardButton.Enabled = false;
        discardButton.TabIndex = 2;
        discardButton.Click += Discard_Click;
        reloadButton.Name = "reloadButton";
        reloadButton.Text = "Aktualisieren";
        reloadButton.Padding = new System.Windows.Forms.Padding(12, 6, 12, 6);
        reloadButton.Margin = new System.Windows.Forms.Padding(4);
        reloadButton.Enabled = false;
        reloadButton.TabIndex = 3;
        reloadButton.Click += Reload_Click;
        actions.Controls.Add(saveButton, 0, 0);
        actions.Controls.Add(discardButton, 1, 0);
        actions.Controls.Add(reloadButton, 2, 0);
        layout.Controls.Add(notesLabel, 0, 0);
        layout.Controls.Add(notesEdit, 0, 1);
        layout.Controls.Add(draftLabel, 0, 2);
        layout.Controls.Add(draftEdit, 0, 3);
        layout.Controls.Add(actions, 0, 4);
        layout.Controls.Add(statusLabel, 0, 5);
        Controls.Add(layout);
        AutoScaleDimensions = new System.Drawing.SizeF(7F, 14F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        Name = "CallNotesPanel";
        Size = new System.Drawing.Size(750, 420);
        ResumeLayout(false);
    }
    private System.Windows.Forms.TableLayoutPanel layout;
    private System.Windows.Forms.TableLayoutPanel actions;
    private DevExpress.XtraEditors.LabelControl notesLabel;
    private DevExpress.XtraEditors.LabelControl draftLabel;
    private DevExpress.XtraEditors.LabelControl statusLabel;
    private DevExpress.XtraEditors.MemoEdit notesEdit;
    private DevExpress.XtraEditors.MemoEdit draftEdit;
    private DevExpress.XtraEditors.SimpleButton saveButton;
    private DevExpress.XtraEditors.SimpleButton reloadButton;
    private DevExpress.XtraEditors.SimpleButton discardButton;
}

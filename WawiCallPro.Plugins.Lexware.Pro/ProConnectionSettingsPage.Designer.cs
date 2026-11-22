namespace WawiCallPro.Plugins.Lexware.Pro;

partial class ProConnectionSettingsPage
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
        enabledEdit = new DevExpress.XtraEditors.CheckEdit();
        hintLabel = new DevExpress.XtraEditors.LabelControl();
        usernameLabel = new DevExpress.XtraEditors.LabelControl();
        usernameEdit = new DevExpress.XtraEditors.TextEdit();
        passwordLabel = new DevExpress.XtraEditors.LabelControl();
        passwordEdit = new DevExpress.XtraEditors.TextEdit();
        clientKeyLabel = new DevExpress.XtraEditors.LabelControl();
        clientKeyEdit = new DevExpress.XtraEditors.TextEdit();
        ((System.ComponentModel.ISupportInitialize)enabledEdit.Properties).BeginInit();
        ((System.ComponentModel.ISupportInitialize)usernameEdit.Properties).BeginInit();
        ((System.ComponentModel.ISupportInitialize)passwordEdit.Properties).BeginInit();
        ((System.ComponentModel.ISupportInitialize)clientKeyEdit.Properties).BeginInit();
        SuspendLayout();
        layout.Name = "layout";
        layout.Dock = System.Windows.Forms.DockStyle.Fill;
        layout.AutoSize = true;
        layout.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        layout.ColumnCount = 2;
        layout.RowCount = 5;
        layout.Padding = new System.Windows.Forms.Padding(6);
        layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
        layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));

        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        enabledEdit.Name = "enabledEdit";
        enabledEdit.Text = "Lexware-Pro-Zugang verwenden";
        enabledEdit.Properties.AutoWidth = true;
        enabledEdit.Margin = new System.Windows.Forms.Padding(6);
        enabledEdit.CheckedChanged += Enabled_Changed;
        layout.Controls.Add(enabledEdit, 0, 0);
        layout.SetColumnSpan(enabledEdit, 2);
        usernameLabel.Name = "usernameLabel";
        usernameLabel.Text = "API-Benutzer:";
        usernameLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
        usernameLabel.Appearance.FontStyleDelta = System.Drawing.FontStyle.Bold;
        usernameLabel.Appearance.Options.UseFont = true;
        usernameLabel.Margin = new System.Windows.Forms.Padding(6, 6, 18, 6);
        usernameEdit.Name = "usernameEdit";
        usernameEdit.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        usernameEdit.Margin = new System.Windows.Forms.Padding(6);
        layout.Controls.Add(usernameLabel, 0, 1);
        layout.Controls.Add(usernameEdit, 1, 1);
        passwordLabel.Name = "passwordLabel";
        passwordLabel.Text = "Kennwort:";
        passwordLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
        passwordLabel.Appearance.FontStyleDelta = System.Drawing.FontStyle.Bold;
        passwordLabel.Appearance.Options.UseFont = true;
        passwordLabel.Margin = new System.Windows.Forms.Padding(6, 6, 18, 6);
        passwordEdit.Name = "passwordEdit";
        passwordEdit.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        passwordEdit.Margin = new System.Windows.Forms.Padding(6);
        layout.Controls.Add(passwordLabel, 0, 2);
        layout.Controls.Add(passwordEdit, 1, 2);
        clientKeyLabel.Name = "clientKeyLabel";
        clientKeyLabel.Text = "Client-Key:";
        clientKeyLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
        clientKeyLabel.Appearance.FontStyleDelta = System.Drawing.FontStyle.Bold;
        clientKeyLabel.Appearance.Options.UseFont = true;
        clientKeyLabel.Margin = new System.Windows.Forms.Padding(6, 6, 18, 6);
        clientKeyEdit.Name = "clientKeyEdit";
        clientKeyEdit.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        clientKeyEdit.Margin = new System.Windows.Forms.Padding(6);
        layout.Controls.Add(clientKeyLabel, 0, 3);
        layout.Controls.Add(clientKeyEdit, 1, 3);
        usernameEdit.Properties.MaxLength = 256;
        passwordEdit.Properties.MaxLength = 4096;
        passwordEdit.Properties.UseSystemPasswordChar = true;
        passwordEdit.Properties.NullValuePrompt = "Leer: gespeichertes Kennwort behalten";
        clientKeyEdit.Properties.MaxLength = 128;
        clientKeyEdit.Properties.UseSystemPasswordChar = true;
        clientKeyEdit.Properties.NullValuePrompt = "Leer: gespeicherten Client-Key behalten";
        hintLabel.Name = "hintLabel";
        hintLabel.Text = "Die API-Adresse wird aus der installierten Lexware-API-Service-Konfiguration ermittelt. Leere Kennwort- und Client-Key-Felder behalten gespeicherte Werte. „Aktualisieren“ prüft die aktuellen Eingaben. Speichern übernimmt den Zugang ohne Programmneustart.";
        hintLabel.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.Vertical;
        hintLabel.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        hintLabel.Margin = new System.Windows.Forms.Padding(6, 12, 6, 6);
        layout.Controls.Add(hintLabel, 0, 4);
        layout.SetColumnSpan(hintLabel, 2);
        Controls.Add(layout);
        AutoSize = true;
        AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        AutoScaleDimensions = new System.Drawing.SizeF(7F, 14F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        Name = "ProConnectionSettingsPage";
        Size = new System.Drawing.Size(680, 440);
        ((System.ComponentModel.ISupportInitialize)enabledEdit.Properties).EndInit();
        ((System.ComponentModel.ISupportInitialize)usernameEdit.Properties).EndInit();
        ((System.ComponentModel.ISupportInitialize)passwordEdit.Properties).EndInit();
        ((System.ComponentModel.ISupportInitialize)clientKeyEdit.Properties).EndInit();
        ResumeLayout(false);
        PerformLayout();
    }

    private System.Windows.Forms.TableLayoutPanel layout;
    private DevExpress.XtraEditors.CheckEdit enabledEdit;
    private DevExpress.XtraEditors.LabelControl hintLabel;
    private DevExpress.XtraEditors.LabelControl usernameLabel;
    private DevExpress.XtraEditors.TextEdit usernameEdit;
    private DevExpress.XtraEditors.LabelControl passwordLabel;
    private DevExpress.XtraEditors.TextEdit passwordEdit;
    private DevExpress.XtraEditors.LabelControl clientKeyLabel;
    private DevExpress.XtraEditors.TextEdit clientKeyEdit;
}

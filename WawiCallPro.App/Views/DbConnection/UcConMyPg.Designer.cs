#nullable disable
namespace WawiCallPro.App.Views.DbConnection
{
    partial class UcConMyPg
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(UcConMyPg));
            tablePanelRoot = new System.Windows.Forms.TableLayoutPanel();
            lblHost = new DevExpress.XtraEditors.LabelControl();
            txtHost = new DevExpress.XtraEditors.TextEdit();
            lblPort = new DevExpress.XtraEditors.LabelControl();
            txtPort = new DevExpress.XtraEditors.TextEdit();
            lblDbName = new DevExpress.XtraEditors.LabelControl();
            txtDbName = new DevExpress.XtraEditors.TextEdit();
            lblUser = new DevExpress.XtraEditors.LabelControl();
            txtUser = new DevExpress.XtraEditors.TextEdit();
            lblPwd = new DevExpress.XtraEditors.LabelControl();
            txtPwd = new DevExpress.XtraEditors.TextEdit();
            lblSslMode = new DevExpress.XtraEditors.LabelControl();
            cmbSslMode = new DevExpress.XtraEditors.ComboBoxEdit();

            ((System.ComponentModel.ISupportInitialize)txtHost.Properties).BeginInit();
            ((System.ComponentModel.ISupportInitialize)txtPort.Properties).BeginInit();
            ((System.ComponentModel.ISupportInitialize)txtDbName.Properties).BeginInit();
            ((System.ComponentModel.ISupportInitialize)txtUser.Properties).BeginInit();
            ((System.ComponentModel.ISupportInitialize)txtPwd.Properties).BeginInit();
            ((System.ComponentModel.ISupportInitialize)cmbSslMode.Properties).BeginInit();
            SuspendLayout();
            //
            // tablePanelRoot
            //
            tablePanelRoot.ColumnCount = 4;
            tablePanelRoot.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            tablePanelRoot.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 66.6667F));
            tablePanelRoot.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            tablePanelRoot.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.3333F));
            tablePanelRoot.Controls.Add(lblHost, 0, 0);
            tablePanelRoot.Controls.Add(txtHost, 1, 0);
            tablePanelRoot.Controls.Add(lblPort, 2, 0);
            tablePanelRoot.Controls.Add(txtPort, 3, 0);
            tablePanelRoot.Controls.Add(lblDbName, 0, 1);
            tablePanelRoot.Controls.Add(txtDbName, 1, 1);
            tablePanelRoot.Controls.Add(lblUser, 0, 2);
            tablePanelRoot.Controls.Add(txtUser, 1, 2);
            tablePanelRoot.Controls.Add(lblPwd, 0, 3);
            tablePanelRoot.Controls.Add(txtPwd, 1, 3);
            tablePanelRoot.Controls.Add(lblSslMode, 0, 4);
            tablePanelRoot.Controls.Add(cmbSslMode, 1, 4);
            tablePanelRoot.Dock = System.Windows.Forms.DockStyle.Fill;
            tablePanelRoot.Location = new System.Drawing.Point(0, 0);
            tablePanelRoot.Name = "tablePanelRoot";
            tablePanelRoot.Padding = new System.Windows.Forms.Padding(3);
            tablePanelRoot.RowCount = 6;
            tablePanelRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            tablePanelRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            tablePanelRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            tablePanelRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            tablePanelRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            tablePanelRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            tablePanelRoot.Size = new System.Drawing.Size(791, 345);
            tablePanelRoot.TabIndex = 0;
            //
            // lblHost
            //
            lblHost.Appearance.Options.UseTextOptions = true;
            lblHost.Appearance.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            lblHost.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.Default;

            lblHost.Anchor = System.Windows.Forms.AnchorStyles.Left;
            lblHost.Location = new System.Drawing.Point(6, 6);
            lblHost.Name = "lblHost";

            lblHost.Size = new System.Drawing.Size(124, 30);
            lblHost.TabIndex = 0;
            lblHost.Text = "Host / Server:";
            //
            // txtHost
            //

            txtHost.Location = new System.Drawing.Point(136, 8);
            txtHost.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            txtHost.Name = "txtHost";

            txtHost.Size = new System.Drawing.Size(377, 26);
            txtHost.TabIndex = 1;
            //
            // lblPort
            //
            lblPort.Appearance.Options.UseTextOptions = true;
            lblPort.Appearance.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            lblPort.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.Default;

            lblPort.Anchor = System.Windows.Forms.AnchorStyles.Left;
            lblPort.Location = new System.Drawing.Point(519, 6);
            lblPort.Name = "lblPort";

            lblPort.Size = new System.Drawing.Size(74, 30);
            lblPort.TabIndex = 2;
            lblPort.Text = "Port:";
            //
            // txtPort
            //

            txtPort.Location = new System.Drawing.Point(599, 8);
            txtPort.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            txtPort.Name = "txtPort";

            txtPort.Size = new System.Drawing.Size(186, 26);
            txtPort.TabIndex = 3;
            //
            // lblDbName
            //
            lblDbName.Appearance.Options.UseTextOptions = true;
            lblDbName.Appearance.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            lblDbName.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.Default;

            lblDbName.Anchor = System.Windows.Forms.AnchorStyles.Left;
            lblDbName.Location = new System.Drawing.Point(6, 42);
            lblDbName.Name = "lblDbName";

            lblDbName.Size = new System.Drawing.Size(124, 30);
            lblDbName.TabIndex = 4;
            lblDbName.Text = "Datenbankname:";
            //
            // txtDbName
            //

            tablePanelRoot.SetColumnSpan(txtDbName, 2);
            txtDbName.Location = new System.Drawing.Point(136, 44);
            txtDbName.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            txtDbName.Name = "txtDbName";

            txtDbName.Size = new System.Drawing.Size(457, 26);
            txtDbName.TabIndex = 5;
            //
            // lblUser
            //
            lblUser.Appearance.Options.UseTextOptions = true;
            lblUser.Appearance.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            lblUser.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.Default;

            lblUser.Anchor = System.Windows.Forms.AnchorStyles.Left;
            lblUser.Location = new System.Drawing.Point(6, 78);
            lblUser.Name = "lblUser";

            lblUser.Size = new System.Drawing.Size(124, 30);
            lblUser.TabIndex = 6;
            lblUser.Text = "Benutzer:";
            //
            // txtUser
            //

            tablePanelRoot.SetColumnSpan(txtUser, 2);
            txtUser.Location = new System.Drawing.Point(136, 80);
            txtUser.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            txtUser.Name = "txtUser";

            txtUser.Size = new System.Drawing.Size(457, 26);
            txtUser.TabIndex = 7;
            //
            // lblPwd
            //
            lblPwd.Appearance.Options.UseTextOptions = true;
            lblPwd.Appearance.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            lblPwd.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.Default;

            lblPwd.Anchor = System.Windows.Forms.AnchorStyles.Left;
            lblPwd.Location = new System.Drawing.Point(6, 114);
            lblPwd.Name = "lblPwd";

            lblPwd.Size = new System.Drawing.Size(124, 30);
            lblPwd.TabIndex = 8;
            lblPwd.Text = "Passwort:";
            //
            // txtPwd
            //

            tablePanelRoot.SetColumnSpan(txtPwd, 2);
            txtPwd.Location = new System.Drawing.Point(136, 116);
            txtPwd.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            txtPwd.Name = "txtPwd";
            txtPwd.Properties.PasswordChar = '●';

            txtPwd.Size = new System.Drawing.Size(457, 26);
            txtPwd.TabIndex = 9;
            //
            // lblSslMode
            //
            lblSslMode.Appearance.Options.UseTextOptions = true;
            lblSslMode.Appearance.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            lblSslMode.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.Default;

            lblSslMode.Anchor = System.Windows.Forms.AnchorStyles.Left;
            lblSslMode.Location = new System.Drawing.Point(6, 150);
            lblSslMode.Name = "lblSslMode";

            lblSslMode.Size = new System.Drawing.Size(124, 30);
            lblSslMode.TabIndex = 10;
            lblSslMode.Text = "SSL-Modus:";
            //
            // cmbSslMode
            //

            tablePanelRoot.SetColumnSpan(cmbSslMode, 2);
            cmbSslMode.Location = new System.Drawing.Point(136, 152);
            cmbSslMode.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            cmbSslMode.Name = "cmbSslMode";
            cmbSslMode.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] { new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.DropDown) });
            cmbSslMode.Properties.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.DisableTextEditor;

            cmbSslMode.Size = new System.Drawing.Size(457, 26);
            cmbSslMode.TabIndex = 11;
            //
            // UcConMyPg
            //
            AutoScaleDimensions = new System.Drawing.SizeF(9F, 19F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            Controls.Add(tablePanelRoot);
            Margin = new System.Windows.Forms.Padding(4);
            Size = new System.Drawing.Size(791, 345);

            ((System.ComponentModel.ISupportInitialize)txtHost.Properties).EndInit();
            ((System.ComponentModel.ISupportInitialize)txtPort.Properties).EndInit();
            ((System.ComponentModel.ISupportInitialize)txtDbName.Properties).EndInit();
            ((System.ComponentModel.ISupportInitialize)txtUser.Properties).EndInit();
            ((System.ComponentModel.ISupportInitialize)txtPwd.Properties).EndInit();
            ((System.ComponentModel.ISupportInitialize)cmbSslMode.Properties).EndInit();
            resources.ApplyResources(lblHost, "lblHost");
            resources.ApplyResources(lblPort, "lblPort");
            resources.ApplyResources(lblDbName, "lblDbName");
            resources.ApplyResources(lblUser, "lblUser");
            resources.ApplyResources(lblPwd, "lblPwd");
            resources.ApplyResources(lblSslMode, "lblSslMode");
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tablePanelRoot;
        private DevExpress.XtraEditors.LabelControl lblHost;
        private DevExpress.XtraEditors.TextEdit txtHost;
        private DevExpress.XtraEditors.LabelControl lblPort;
        private DevExpress.XtraEditors.TextEdit txtPort;
        private DevExpress.XtraEditors.LabelControl lblDbName;
        private DevExpress.XtraEditors.TextEdit txtDbName;
        private DevExpress.XtraEditors.LabelControl lblUser;
        private DevExpress.XtraEditors.TextEdit txtUser;
        private DevExpress.XtraEditors.LabelControl lblPwd;
        private DevExpress.XtraEditors.TextEdit txtPwd;
        private DevExpress.XtraEditors.LabelControl lblSslMode;
        private DevExpress.XtraEditors.ComboBoxEdit cmbSslMode;
    }
}

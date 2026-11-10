#nullable disable
namespace WawiCallPro.App.Views.DbConnection
{
    partial class UcConSqlServer
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(UcConSqlServer));
            tablePanelRoot = new System.Windows.Forms.TableLayoutPanel();
            lblSqlServer = new DevExpress.XtraEditors.LabelControl();
            txtSqlServer = new DevExpress.XtraEditors.TextEdit();
            lblSqlPort = new DevExpress.XtraEditors.LabelControl();
            txtSqlPort = new DevExpress.XtraEditors.TextEdit();
            lblSqlDb = new DevExpress.XtraEditors.LabelControl();
            txtSqlDb = new DevExpress.XtraEditors.TextEdit();
            chkEncrypt = new DevExpress.XtraEditors.CheckEdit();
            chkTrustCert = new DevExpress.XtraEditors.CheckEdit();
            lblSqlUser = new DevExpress.XtraEditors.LabelControl();
            txtSqlUser = new DevExpress.XtraEditors.TextEdit();
            lblSqlPwd = new DevExpress.XtraEditors.LabelControl();
            txtSqlPwd = new DevExpress.XtraEditors.TextEdit();
            chkWinAuth = new DevExpress.XtraEditors.CheckEdit();

            ((System.ComponentModel.ISupportInitialize)txtSqlServer.Properties).BeginInit();
            ((System.ComponentModel.ISupportInitialize)txtSqlPort.Properties).BeginInit();
            ((System.ComponentModel.ISupportInitialize)txtSqlDb.Properties).BeginInit();
            ((System.ComponentModel.ISupportInitialize)chkEncrypt.Properties).BeginInit();
            ((System.ComponentModel.ISupportInitialize)chkTrustCert.Properties).BeginInit();
            ((System.ComponentModel.ISupportInitialize)txtSqlUser.Properties).BeginInit();
            ((System.ComponentModel.ISupportInitialize)txtSqlPwd.Properties).BeginInit();
            ((System.ComponentModel.ISupportInitialize)chkWinAuth.Properties).BeginInit();
            SuspendLayout();
            //
            // tablePanelRoot
            //
            tablePanelRoot.ColumnCount = 4;
            tablePanelRoot.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            tablePanelRoot.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            tablePanelRoot.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            tablePanelRoot.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            tablePanelRoot.Controls.Add(lblSqlServer, 0, 0);
            tablePanelRoot.Controls.Add(txtSqlServer, 1, 0);
            tablePanelRoot.Controls.Add(lblSqlPort, 2, 0);
            tablePanelRoot.Controls.Add(txtSqlPort, 3, 0);
            tablePanelRoot.Controls.Add(lblSqlDb, 0, 1);
            tablePanelRoot.Controls.Add(txtSqlDb, 1, 1);
            tablePanelRoot.Controls.Add(chkEncrypt, 0, 2);
            tablePanelRoot.Controls.Add(chkTrustCert, 2, 2);
            tablePanelRoot.Controls.Add(lblSqlUser, 0, 3);
            tablePanelRoot.Controls.Add(txtSqlUser, 1, 3);
            tablePanelRoot.Controls.Add(lblSqlPwd, 2, 3);
            tablePanelRoot.Controls.Add(txtSqlPwd, 3, 3);
            tablePanelRoot.Controls.Add(chkWinAuth, 0, 4);
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
            tablePanelRoot.Size = new System.Drawing.Size(898, 377);
            tablePanelRoot.TabIndex = 0;
            //
            // lblSqlServer
            //
            lblSqlServer.Appearance.Options.UseTextOptions = true;
            lblSqlServer.Appearance.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            lblSqlServer.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.Default;

            lblSqlServer.Anchor = System.Windows.Forms.AnchorStyles.Left;
            lblSqlServer.Location = new System.Drawing.Point(6, 6);
            lblSqlServer.Name = "lblSqlServer";

            lblSqlServer.Size = new System.Drawing.Size(124, 30);
            lblSqlServer.TabIndex = 0;
            lblSqlServer.Text = "Server / Instanz:";
            //
            // txtSqlServer
            //

            txtSqlServer.Location = new System.Drawing.Point(136, 8);
            txtSqlServer.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            txtSqlServer.Name = "txtSqlServer";

            txtSqlServer.Size = new System.Drawing.Size(335, 26);
            txtSqlServer.TabIndex = 1;
            //
            // lblSqlPort
            //
            lblSqlPort.Appearance.Options.UseTextOptions = true;
            lblSqlPort.Appearance.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            lblSqlPort.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.Default;

            lblSqlPort.Anchor = System.Windows.Forms.AnchorStyles.Left;
            lblSqlPort.Location = new System.Drawing.Point(477, 6);
            lblSqlPort.Name = "lblSqlPort";

            lblSqlPort.Size = new System.Drawing.Size(74, 30);
            lblSqlPort.TabIndex = 2;
            lblSqlPort.Text = "Port:";
            //
            // txtSqlPort
            //

            txtSqlPort.Location = new System.Drawing.Point(557, 8);
            txtSqlPort.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            txtSqlPort.Name = "txtSqlPort";

            txtSqlPort.Size = new System.Drawing.Size(335, 26);
            txtSqlPort.TabIndex = 3;
            //
            // lblSqlDb
            //
            lblSqlDb.Appearance.Options.UseTextOptions = true;
            lblSqlDb.Appearance.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            lblSqlDb.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.Default;

            lblSqlDb.Anchor = System.Windows.Forms.AnchorStyles.Left;
            lblSqlDb.Location = new System.Drawing.Point(6, 42);
            lblSqlDb.Name = "lblSqlDb";

            lblSqlDb.Size = new System.Drawing.Size(124, 30);
            lblSqlDb.TabIndex = 4;
            lblSqlDb.Text = "Datenbankname:";
            //
            // txtSqlDb
            //

            txtSqlDb.Location = new System.Drawing.Point(136, 44);
            txtSqlDb.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            txtSqlDb.Name = "txtSqlDb";

            txtSqlDb.Size = new System.Drawing.Size(335, 26);
            txtSqlDb.TabIndex = 5;
            //
            // chkEncrypt
            //

            tablePanelRoot.SetColumnSpan(chkEncrypt, 2);
            chkEncrypt.Location = new System.Drawing.Point(6, 78);
            chkEncrypt.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            chkEncrypt.Name = "chkEncrypt";
            chkEncrypt.Properties.Caption = "Encrypt";

            chkEncrypt.Size = new System.Drawing.Size(465, 27);
            chkEncrypt.TabIndex = 6;
            //
            // chkTrustCert
            //

            tablePanelRoot.SetColumnSpan(chkTrustCert, 2);
            chkTrustCert.Location = new System.Drawing.Point(6, 112);
            chkTrustCert.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            chkTrustCert.Name = "chkTrustCert";
            chkTrustCert.Properties.Caption = "TrustServerCertificate";

            chkTrustCert.Size = new System.Drawing.Size(465, 27);
            chkTrustCert.TabIndex = 7;
            //
            // lblSqlUser
            //
            lblSqlUser.Appearance.Options.UseTextOptions = true;
            lblSqlUser.Appearance.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            lblSqlUser.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.Default;

            lblSqlUser.Anchor = System.Windows.Forms.AnchorStyles.Left;
            lblSqlUser.Location = new System.Drawing.Point(6, 146);
            lblSqlUser.Name = "lblSqlUser";

            lblSqlUser.Size = new System.Drawing.Size(124, 30);
            lblSqlUser.TabIndex = 8;
            lblSqlUser.Text = "Benutzer:";
            //
            // txtSqlUser
            //

            txtSqlUser.Location = new System.Drawing.Point(136, 148);
            txtSqlUser.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            txtSqlUser.Name = "txtSqlUser";

            txtSqlUser.Size = new System.Drawing.Size(335, 26);
            txtSqlUser.TabIndex = 9;
            //
            // lblSqlPwd
            //
            lblSqlPwd.Appearance.Options.UseTextOptions = true;
            lblSqlPwd.Appearance.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            lblSqlPwd.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.Default;

            lblSqlPwd.Anchor = System.Windows.Forms.AnchorStyles.Left;
            lblSqlPwd.Location = new System.Drawing.Point(6, 182);
            lblSqlPwd.Name = "lblSqlPwd";

            lblSqlPwd.Size = new System.Drawing.Size(124, 30);
            lblSqlPwd.TabIndex = 10;
            lblSqlPwd.Text = "Passwort:";
            //
            // txtSqlPwd
            //

            txtSqlPwd.Location = new System.Drawing.Point(136, 184);
            txtSqlPwd.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            txtSqlPwd.Name = "txtSqlPwd";
            txtSqlPwd.Properties.PasswordChar = '●';

            txtSqlPwd.Size = new System.Drawing.Size(335, 26);
            txtSqlPwd.TabIndex = 11;
            //
            // chkWinAuth
            //

            tablePanelRoot.SetColumnSpan(chkWinAuth, 4);
            chkWinAuth.Dock = System.Windows.Forms.DockStyle.Top;
            chkWinAuth.Location = new System.Drawing.Point(6, 218);
            chkWinAuth.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            chkWinAuth.Name = "chkWinAuth";
            chkWinAuth.Properties.Caption = "Windows-Authentifizierung";

            chkWinAuth.Size = new System.Drawing.Size(465, 27);
            chkWinAuth.TabIndex = 12;
            //
            // UcConSqlServer
            //
            AutoScaleDimensions = new System.Drawing.SizeF(9F, 19F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            Controls.Add(tablePanelRoot);
            Margin = new System.Windows.Forms.Padding(4);
            Size = new System.Drawing.Size(898, 377);

            ((System.ComponentModel.ISupportInitialize)txtSqlServer.Properties).EndInit();
            ((System.ComponentModel.ISupportInitialize)txtSqlPort.Properties).EndInit();
            ((System.ComponentModel.ISupportInitialize)txtSqlDb.Properties).EndInit();
            ((System.ComponentModel.ISupportInitialize)chkEncrypt.Properties).EndInit();
            ((System.ComponentModel.ISupportInitialize)chkTrustCert.Properties).EndInit();
            ((System.ComponentModel.ISupportInitialize)txtSqlUser.Properties).EndInit();
            ((System.ComponentModel.ISupportInitialize)txtSqlPwd.Properties).EndInit();
            ((System.ComponentModel.ISupportInitialize)chkWinAuth.Properties).EndInit();
            resources.ApplyResources(lblSqlServer, "lblSqlServer");
            resources.ApplyResources(lblSqlPort, "lblSqlPort");
            resources.ApplyResources(lblSqlDb, "lblSqlDb");
            resources.ApplyResources(chkEncrypt.Properties, "chkEncrypt.Properties");
            resources.ApplyResources(chkTrustCert.Properties, "chkTrustCert.Properties");
            resources.ApplyResources(lblSqlUser, "lblSqlUser");
            resources.ApplyResources(lblSqlPwd, "lblSqlPwd");
            resources.ApplyResources(chkWinAuth.Properties, "chkWinAuth.Properties");
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tablePanelRoot;
        private DevExpress.XtraEditors.LabelControl lblSqlServer;
        private DevExpress.XtraEditors.TextEdit txtSqlServer;
        private DevExpress.XtraEditors.LabelControl lblSqlPort;
        private DevExpress.XtraEditors.TextEdit txtSqlPort;
        private DevExpress.XtraEditors.LabelControl lblSqlDb;
        private DevExpress.XtraEditors.TextEdit txtSqlDb;
        private DevExpress.XtraEditors.CheckEdit chkEncrypt;
        private DevExpress.XtraEditors.CheckEdit chkTrustCert;
        private DevExpress.XtraEditors.LabelControl lblSqlUser;
        private DevExpress.XtraEditors.TextEdit txtSqlUser;
        private DevExpress.XtraEditors.LabelControl lblSqlPwd;
        private DevExpress.XtraEditors.TextEdit txtSqlPwd;
        private DevExpress.XtraEditors.CheckEdit chkWinAuth;
    }
}

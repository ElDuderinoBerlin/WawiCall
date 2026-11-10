#nullable disable
namespace WawiCallPro.App.Views.DbConnection
{
    partial class UcConSqlite
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(UcConSqlite));
            tablePanelRoot = new System.Windows.Forms.TableLayoutPanel();
            lblSqliteFile = new DevExpress.XtraEditors.LabelControl();
            txtSqliteFile = new DevExpress.XtraEditors.TextEdit();
            btnSqliteBrowse = new DevExpress.XtraEditors.SimpleButton();

            ((System.ComponentModel.ISupportInitialize)txtSqliteFile.Properties).BeginInit();
            SuspendLayout();
            //
            // tablePanelRoot
            //
            tablePanelRoot.ColumnCount = 3;
            tablePanelRoot.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            tablePanelRoot.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            tablePanelRoot.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            tablePanelRoot.Controls.Add(lblSqliteFile, 0, 0);
            tablePanelRoot.Controls.Add(txtSqliteFile, 1, 0);
            tablePanelRoot.Controls.Add(btnSqliteBrowse, 2, 0);
            tablePanelRoot.Dock = System.Windows.Forms.DockStyle.Fill;
            tablePanelRoot.Location = new System.Drawing.Point(0, 0);
            tablePanelRoot.Name = "tablePanelRoot";
            tablePanelRoot.Padding = new System.Windows.Forms.Padding(5);
            tablePanelRoot.RowCount = 2;
            tablePanelRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            tablePanelRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            tablePanelRoot.Size = new System.Drawing.Size(908, 177);
            tablePanelRoot.TabIndex = 0;
            //
            // lblSqliteFile
            //
            lblSqliteFile.Appearance.Options.UseTextOptions = true;
            lblSqliteFile.Appearance.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            lblSqliteFile.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.Default;

            lblSqliteFile.Anchor = System.Windows.Forms.AnchorStyles.Left;
            lblSqliteFile.Location = new System.Drawing.Point(8, 8);
            lblSqliteFile.Name = "lblSqliteFile";

            lblSqliteFile.Size = new System.Drawing.Size(124, 32);
            lblSqliteFile.TabIndex = 0;
            lblSqliteFile.Text = "Datenbankdatei:";
            //
            // txtSqliteFile
            //

            txtSqliteFile.Location = new System.Drawing.Point(138, 11);
            txtSqliteFile.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            txtSqliteFile.Name = "txtSqliteFile";

            txtSqliteFile.Size = new System.Drawing.Size(632, 26);
            txtSqliteFile.TabIndex = 1;
            //
            // btnSqliteBrowse
            //

            btnSqliteBrowse.Location = new System.Drawing.Point(776, 8);
            btnSqliteBrowse.AutoSize = true;
            btnSqliteBrowse.Padding = new System.Windows.Forms.Padding(12, 6, 12, 6);
            btnSqliteBrowse.Name = "btnSqliteBrowse";

            btnSqliteBrowse.Size = new System.Drawing.Size(124, 31);
            btnSqliteBrowse.TabIndex = 2;
            btnSqliteBrowse.Text = "Durchsuchen...";
            btnSqliteBrowse.Click += BtnSqliteBrowse_Click;
            //
            // UcConSqlite
            //
            AutoScaleDimensions = new System.Drawing.SizeF(9F, 19F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            Controls.Add(tablePanelRoot);
            Margin = new System.Windows.Forms.Padding(4);
            Size = new System.Drawing.Size(908, 177);

            ((System.ComponentModel.ISupportInitialize)txtSqliteFile.Properties).EndInit();
            resources.ApplyResources(lblSqliteFile, "lblSqliteFile");
            resources.ApplyResources(btnSqliteBrowse, "btnSqliteBrowse");
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tablePanelRoot;
        private DevExpress.XtraEditors.LabelControl lblSqliteFile;
        private DevExpress.XtraEditors.TextEdit txtSqliteFile;
        private DevExpress.XtraEditors.SimpleButton btnSqliteBrowse;
    }
}

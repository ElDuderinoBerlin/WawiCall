#nullable disable
namespace WawiCallPro.App.Views
{
    partial class UcDbStep2Connection
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
            components = new System.ComponentModel.Container();
            _errorProvider = new DevExpress.XtraEditors.DXErrorProvider.DXErrorProvider(components);
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(UcDbStep2Connection));
            tablePanelRoot = new System.Windows.Forms.TableLayoutPanel();
            navFrame = new DevExpress.XtraBars.Navigation.NavigationFrame();
            pageSqlite = new DevExpress.XtraBars.Navigation.NavigationPage();
            ucSqlite = new WawiCallPro.App.Views.DbConnection.UcConSqlite();
            pageSqlServer = new DevExpress.XtraBars.Navigation.NavigationPage();
            ucSqlServer = new WawiCallPro.App.Views.DbConnection.UcConSqlServer();
            pageMyPg = new DevExpress.XtraBars.Navigation.NavigationPage();
            ucMyPg = new WawiCallPro.App.Views.DbConnection.UcConMyPg();
            pagePostgres = new DevExpress.XtraBars.Navigation.NavigationPage();
            ucPostgres = new WawiCallPro.App.Views.DbConnection.UcConMyPg();
            lblPreview = new DevExpress.XtraEditors.LabelControl();
            txtCsPreview = new DevExpress.XtraEditors.TextEdit();

            ((System.ComponentModel.ISupportInitialize)navFrame).BeginInit();
            navFrame.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)txtCsPreview.Properties).BeginInit();
            SuspendLayout();
            //
            // tablePanelRoot
            //
            tablePanelRoot.ColumnCount = 2;
            tablePanelRoot.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            tablePanelRoot.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            tablePanelRoot.Controls.Add(navFrame, 0, 0);
            tablePanelRoot.Controls.Add(lblPreview, 0, 1);
            tablePanelRoot.Controls.Add(txtCsPreview, 1, 1);
            tablePanelRoot.Dock = System.Windows.Forms.DockStyle.Fill;
            tablePanelRoot.Location = new System.Drawing.Point(0, 0);
            tablePanelRoot.Name = "tablePanelRoot";
            tablePanelRoot.Padding = new System.Windows.Forms.Padding(15);
            tablePanelRoot.RowCount = 2;
            tablePanelRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            tablePanelRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            tablePanelRoot.Size = new System.Drawing.Size(916, 379);
            tablePanelRoot.TabIndex = 0;
            //
            // navFrame
            //

            tablePanelRoot.SetColumnSpan(navFrame, 2);
            navFrame.Controls.Add(pageSqlite);
            navFrame.Controls.Add(pageSqlServer);
            navFrame.Controls.Add(pageMyPg);
            navFrame.Controls.Add(pagePostgres);
            navFrame.Dock = System.Windows.Forms.DockStyle.Fill;
            navFrame.Location = new System.Drawing.Point(18, 18);
            navFrame.Name = "navFrame";
            navFrame.Pages.AddRange(new DevExpress.XtraBars.Navigation.NavigationPageBase[] { pageSqlite, pageSqlServer, pageMyPg, pagePostgres });

            navFrame.SelectedPage = pageSqlite;
            navFrame.Size = new System.Drawing.Size(880, 303);
            navFrame.TabIndex = 0;
            navFrame.Text = "navFrame";
            //
            // pageSqlite
            //
            pageSqlite.Controls.Add(ucSqlite);
            pageSqlite.Name = "pageSqlite";
            pageSqlite.Size = new System.Drawing.Size(880, 303);
            //
            // ucSqlite
            //
            ucSqlite.Dock = System.Windows.Forms.DockStyle.Fill;
            ucSqlite.Location = new System.Drawing.Point(0, 0);
            ucSqlite.Margin = new System.Windows.Forms.Padding(4);
            ucSqlite.Name = "ucSqlite";
            ucSqlite.Size = new System.Drawing.Size(880, 303);
            ucSqlite.TabIndex = 0;
            //
            // pageSqlServer
            //
            pageSqlServer.Controls.Add(ucSqlServer);
            pageSqlServer.Name = "pageSqlServer";
            pageSqlServer.Size = new System.Drawing.Size(880, 303);
            //
            // ucSqlServer
            //
            ucSqlServer.Dock = System.Windows.Forms.DockStyle.Fill;
            ucSqlServer.Location = new System.Drawing.Point(0, 0);
            ucSqlServer.Margin = new System.Windows.Forms.Padding(4);
            ucSqlServer.Name = "ucSqlServer";
            ucSqlServer.Size = new System.Drawing.Size(880, 303);
            ucSqlServer.TabIndex = 0;
            //
            // pageMyPg
            //
            pagePostgres.Name = "pagePostgres";
            pagePostgres.Size = new System.Drawing.Size(880, 303);
            pagePostgres.Controls.Add(ucPostgres);
            ucPostgres.Dock = System.Windows.Forms.DockStyle.Fill;
            ucPostgres.Name = "ucPostgres";
            ucPostgres.Size = new System.Drawing.Size(880, 303);
            pageMyPg.Controls.Add(ucMyPg);
            pageMyPg.Name = "pageMyPg";
            pageMyPg.Size = new System.Drawing.Size(880, 303);
            //
            // ucMyPg
            //
            ucMyPg.Dock = System.Windows.Forms.DockStyle.Fill;
            ucMyPg.Location = new System.Drawing.Point(0, 0);
            ucMyPg.Margin = new System.Windows.Forms.Padding(4);
            ucMyPg.Name = "ucMyPg";
            ucMyPg.Size = new System.Drawing.Size(880, 303);
            ucMyPg.TabIndex = 0;
            //
            // lblPreview
            //
            lblPreview.Appearance.Options.UseTextOptions = true;
            lblPreview.Appearance.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            lblPreview.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.Default;

            lblPreview.Anchor = System.Windows.Forms.AnchorStyles.Left;
            lblPreview.Location = new System.Drawing.Point(18, 327);
            lblPreview.Name = "lblPreview";

            lblPreview.Size = new System.Drawing.Size(224, 34);
            lblPreview.TabIndex = 1;
            lblPreview.Text = "Verbindung (ohne Kennwort):";
            //
            // txtCsPreview
            //

            txtCsPreview.Location = new System.Drawing.Point(248, 331);
            txtCsPreview.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            txtCsPreview.Name = "txtCsPreview";
            txtCsPreview.Properties.Appearance.ForeColor = System.Drawing.Color.Gray;
            txtCsPreview.Properties.Appearance.Options.UseForeColor = true;
            txtCsPreview.Properties.ReadOnly = true;

            txtCsPreview.Size = new System.Drawing.Size(650, 26);
            txtCsPreview.TabIndex = 2;
            //
            // UcDbStep2Connection
            //
            AutoScaleDimensions = new System.Drawing.SizeF(9F, 19F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            Controls.Add(tablePanelRoot);
            Margin = new System.Windows.Forms.Padding(4);
            Size = new System.Drawing.Size(916, 379);

            ((System.ComponentModel.ISupportInitialize)navFrame).EndInit();
            navFrame.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)txtCsPreview.Properties).EndInit();
            resources.ApplyResources(navFrame, "navFrame");
            resources.ApplyResources(lblPreview, "lblPreview");
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tablePanelRoot;
        private DevExpress.XtraBars.Navigation.NavigationFrame navFrame;
        private DevExpress.XtraBars.Navigation.NavigationPage pageSqlite;
        private DevExpress.XtraBars.Navigation.NavigationPage pageSqlServer;
        private DevExpress.XtraBars.Navigation.NavigationPage pageMyPg;
        private DevExpress.XtraBars.Navigation.NavigationPage pagePostgres;
        private WawiCallPro.App.Views.DbConnection.UcConMyPg ucPostgres;
        private WawiCallPro.App.Views.DbConnection.UcConSqlite ucSqlite;
        private WawiCallPro.App.Views.DbConnection.UcConSqlServer ucSqlServer;
        private WawiCallPro.App.Views.DbConnection.UcConMyPg ucMyPg;
        private DevExpress.XtraEditors.LabelControl lblPreview;
        private DevExpress.XtraEditors.TextEdit txtCsPreview;
        private DevExpress.XtraEditors.DXErrorProvider.DXErrorProvider _errorProvider;
    }
}

#nullable disable
namespace WawiCallPro.App.Views
{
    partial class FrmDatabaseSetup
    {
        private System.ComponentModel.IContainer components = null;
        private WawiCallPro.App.Views.UcDbStep1DbType _ucStep1;
        private WawiCallPro.App.Views.UcDbStep2Connection _ucStep2;
        private WawiCallPro.App.Views.UcDbStep3Migration _ucStep3;
        private DevExpress.XtraBars.Navigation.NavigationPage pgDbType;
        private DevExpress.XtraBars.Navigation.NavigationPage pgConnection;
        private DevExpress.XtraBars.Navigation.NavigationPage pgMigration;


        protected override void Dispose(bool disposing)
        {
            if (disposing) DisposeRuntime();
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FrmDatabaseSetup));
            _ucStep1 = new WawiCallPro.App.Views.UcDbStep1DbType();
            _ucStep2 = new WawiCallPro.App.Views.UcDbStep2Connection();
            _ucStep3 = new WawiCallPro.App.Views.UcDbStep3Migration();
            pgDbType = new DevExpress.XtraBars.Navigation.NavigationPage();
            pgConnection = new DevExpress.XtraBars.Navigation.NavigationPage();
            pgMigration = new DevExpress.XtraBars.Navigation.NavigationPage();
            setupMessage = new DevExpress.XtraEditors.LabelControl();
            navFrame = new DevExpress.XtraBars.Navigation.NavigationFrame();
            pnlNav = new System.Windows.Forms.TableLayoutPanel();
            btnCancel = new DevExpress.XtraEditors.SimpleButton();
            btnFinish = new DevExpress.XtraEditors.SimpleButton();
            btnNext = new DevExpress.XtraEditors.SimpleButton();
            btnTestConnection = new DevExpress.XtraEditors.SimpleButton();
            btnBack = new DevExpress.XtraEditors.SimpleButton();
            ((System.ComponentModel.ISupportInitialize)navFrame).BeginInit();

            SuspendLayout();
            //
            // navFrame
            //
            navFrame.Dock = System.Windows.Forms.DockStyle.Fill;
            navFrame.Location = new System.Drawing.Point(0, 0);
            navFrame.Name = "navFrame";
            navFrame.Size = new System.Drawing.Size(893, 479);
            navFrame.TabIndex = 1;
            //
            // pnlNav
            //
            pnlNav.Controls.Add(btnCancel, 5, 0);
            pnlNav.Controls.Add(btnFinish, 4, 0);
            pnlNav.Controls.Add(btnNext, 3, 0);
            pnlNav.Controls.Add(btnTestConnection, 2, 0);
            pnlNav.Controls.Add(btnBack, 1, 0);
            pnlNav.Dock = System.Windows.Forms.DockStyle.Bottom;
            pnlNav.Location = new System.Drawing.Point(0, 479);
            pnlNav.Name = "pnlNav";
            pnlNav.Padding = new System.Windows.Forms.Padding(8);
            pnlNav.Size = new System.Drawing.Size(893, 58);
            pnlNav.TabIndex = 0;
            //
            // btnCancel
            //
            btnCancel.Anchor = System.Windows.Forms.AnchorStyles.Right;
            btnCancel.Location = new System.Drawing.Point(2828, -50);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new System.Drawing.Size(112, 32);
            btnCancel.TabIndex = 3;
            btnCancel.Text = "Abbrechen";
            btnCancel.Click += BtnCancel_Click;
            //
            // btnFinish
            //
            btnFinish.Anchor = System.Windows.Forms.AnchorStyles.Right;
            btnFinish.Location = new System.Drawing.Point(2702, -50);
            btnFinish.Name = "btnFinish";
            btnFinish.Size = new System.Drawing.Size(120, 32);
            btnFinish.TabIndex = 2;
            btnFinish.Text = "Fertig stellen";
            btnFinish.Visible = false;
            btnFinish.Click += BtnFinish_Click;
            //
            // btnNext
            //
            btnNext.Anchor = System.Windows.Forms.AnchorStyles.Right;
            btnNext.Location = new System.Drawing.Point(2718, -50);
            btnNext.Name = "btnNext";
            btnNext.Size = new System.Drawing.Size(104, 32);
            btnNext.TabIndex = 1;
            btnNext.Text = "Weiter >";
            btnNext.Click += BtnNext_Click;
            //
            // btnTestConnection
            //
            btnTestConnection.Anchor = System.Windows.Forms.AnchorStyles.Right;
            btnTestConnection.Location = new System.Drawing.Point(2566, -50);
            btnTestConnection.Name = "btnTestConnection";
            btnTestConnection.Size = new System.Drawing.Size(146, 32);
            btnTestConnection.TabIndex = 4;
            btnTestConnection.Text = "Testen";
            btnTestConnection.Visible = false;
            btnTestConnection.Click += BtnTestConnection_Click;
            //
            // btnBack
            //
            btnBack.Anchor = System.Windows.Forms.AnchorStyles.Right;
            btnBack.Location = new System.Drawing.Point(2608, -50);
            btnBack.Name = "btnBack";
            btnBack.Size = new System.Drawing.Size(104, 32);
            btnBack.TabIndex = 0;
            btnBack.Text = "< Zurück";
            btnBack.Visible = false;
            btnBack.Click += BtnBack_Click;
            //
            // FrmDatabaseSetup
            //
            AutoScaleDimensions = new System.Drawing.SizeF(9F, 19F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(893, 537);
            Controls.Add(navFrame);
            Controls.Add(pnlNav);
            setupMessage.Dock = System.Windows.Forms.DockStyle.Top;
            setupMessage.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.Vertical;
            setupMessage.Appearance.TextOptions.WordWrap = DevExpress.Utils.WordWrap.Wrap;
            setupMessage.Padding = new System.Windows.Forms.Padding(16, 12, 16, 12);
            setupMessage.Name = "setupMessage";
            setupMessage.Text = "Datenbank einrichten";
            Controls.Add(setupMessage);
            MinimumSize = new System.Drawing.Size(760, 560);
            IconOptions.SvgImage = ((DevExpress.Utils.Svg.SvgImage)resources.GetObject("FrmDatabaseSetup.IconOptions.SvgImage"));
            Margin = new System.Windows.Forms.Padding(4);
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            Text = "Datenbank einrichten";
            ((System.ComponentModel.ISupportInitialize)navFrame).EndInit();

            resources.ApplyResources(btnCancel, "btnCancel");
            resources.ApplyResources(btnFinish, "btnFinish");
            resources.ApplyResources(btnNext, "btnNext");
            resources.ApplyResources(btnTestConnection, "btnTestConnection");
            resources.ApplyResources(btnBack, "btnBack");
            resources.ApplyResources(this, "$this");
            pnlNav.AutoSize = true;
            pnlNav.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            pnlNav.ColumnCount = 6;
            pnlNav.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            pnlNav.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            pnlNav.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            pnlNav.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            pnlNav.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            pnlNav.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            pnlNav.RowCount = 1;
            pnlNav.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            btnBack.AutoSize = true;
            btnBack.Padding = new System.Windows.Forms.Padding(12, 6, 12, 6);
            btnBack.Margin = new System.Windows.Forms.Padding(4);
            btnTestConnection.AutoSize = true;
            btnTestConnection.Padding = new System.Windows.Forms.Padding(12, 6, 12, 6);
            btnTestConnection.Margin = new System.Windows.Forms.Padding(4);
            btnNext.AutoSize = true;
            btnNext.Padding = new System.Windows.Forms.Padding(12, 6, 12, 6);
            btnNext.Margin = new System.Windows.Forms.Padding(4);
            btnFinish.AutoSize = true;
            btnFinish.Padding = new System.Windows.Forms.Padding(12, 6, 12, 6);
            btnFinish.Margin = new System.Windows.Forms.Padding(4);
            btnCancel.AutoSize = true;
            btnCancel.Padding = new System.Windows.Forms.Padding(12, 6, 12, 6);
            btnCancel.Margin = new System.Windows.Forms.Padding(4);
            btnFinish.Text = "Übernehmen";
            pgDbType.Name = "pgDbType";
            pgDbType.Caption = "Datenbanktyp";
            _ucStep1.Dock = System.Windows.Forms.DockStyle.Fill;
            pgDbType.Controls.Add(_ucStep1);
            pgConnection.Name = "pgConnection";
            pgConnection.Caption = "Verbindung";
            _ucStep2.Dock = System.Windows.Forms.DockStyle.Fill;
            pgConnection.Controls.Add(_ucStep2);
            pgMigration.Name = "pgMigration";
            pgMigration.Caption = "Migration";
            _ucStep3.Dock = System.Windows.Forms.DockStyle.Fill;
            pgMigration.Controls.Add(_ucStep3);
            navFrame.Pages.AddRange(new DevExpress.XtraBars.Navigation.NavigationPageBase[] { pgDbType, pgConnection, pgMigration });
            navFrame.SelectedPage = pgDbType;
            ResumeLayout(false);
        }

        #endregion

        private DevExpress.XtraEditors.LabelControl setupMessage;
        private DevExpress.XtraBars.Navigation.NavigationFrame navFrame;
        private System.Windows.Forms.TableLayoutPanel pnlNav;
        private DevExpress.XtraEditors.SimpleButton btnBack;
        private DevExpress.XtraEditors.SimpleButton btnNext;
        private DevExpress.XtraEditors.SimpleButton btnTestConnection;
        private DevExpress.XtraEditors.SimpleButton btnFinish;
        private DevExpress.XtraEditors.SimpleButton btnCancel;
    }
}

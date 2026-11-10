#nullable disable
namespace WawiCallPro.App.Views
{
    partial class UcDbStep1DbType
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null) components.Dispose();
            base.Dispose(disposing);
        }

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(UcDbStep1DbType));
            tablePanelRoot = new System.Windows.Forms.TableLayoutPanel();
            lblDbType = new DevExpress.XtraEditors.LabelControl();
            cmbDbType = new DevExpress.XtraEditors.ImageComboBoxEdit();
            lblDbInfo = new DevExpress.XtraEditors.LabelControl();
            lblDbDescription = new DevExpress.XtraEditors.LabelControl();

            ((System.ComponentModel.ISupportInitialize)cmbDbType.Properties).BeginInit();
            SuspendLayout();
            //
            // tablePanelRoot
            //
            tablePanelRoot.ColumnCount = 2;
            tablePanelRoot.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            tablePanelRoot.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));

            tablePanelRoot.Controls.Add(lblDbType, 0, 0);
            tablePanelRoot.Controls.Add(cmbDbType, 1, 0);
            tablePanelRoot.Controls.Add(lblDbInfo, 0, 1);
            tablePanelRoot.Controls.Add(lblDbDescription, 1, 1);
            tablePanelRoot.Dock = System.Windows.Forms.DockStyle.Fill;
            tablePanelRoot.Location = new System.Drawing.Point(0, 0);
            tablePanelRoot.Name = "tablePanelRoot";
            tablePanelRoot.Padding = new System.Windows.Forms.Padding(5);
            tablePanelRoot.RowCount = 3;
            tablePanelRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            tablePanelRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            tablePanelRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            tablePanelRoot.Size = new System.Drawing.Size(957, 160);
            tablePanelRoot.TabIndex = 0;
            //
            // lblDbType
            //
            lblDbType.Appearance.Options.UseTextOptions = true;
            lblDbType.Appearance.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            lblDbType.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.Default;

            lblDbType.Anchor = System.Windows.Forms.AnchorStyles.Left;
            lblDbType.Location = new System.Drawing.Point(8, 8);
            lblDbType.Name = "lblDbType";

            lblDbType.Size = new System.Drawing.Size(134, 30);
            lblDbType.TabIndex = 0;
            lblDbType.Text = "Datenbanktyp:";
            //
            // cmbDbType
            //

            cmbDbType.Location = new System.Drawing.Point(148, 10);
            cmbDbType.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            cmbDbType.Name = "cmbDbType";
            cmbDbType.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] { new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.DropDown) });

            cmbDbType.Size = new System.Drawing.Size(622, 26);
            cmbDbType.TabIndex = 1;
            //
            // lblDbInfo
            //
            lblDbInfo.Appearance.Options.UseTextOptions = true;
            lblDbInfo.Appearance.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Top;
            lblDbInfo.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.Default;

            lblDbInfo.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            lblDbInfo.Location = new System.Drawing.Point(8, 44);
            lblDbInfo.Name = "lblDbInfo";
            lblDbInfo.Padding = new System.Windows.Forms.Padding(0, 8, 0, 0);

            lblDbInfo.Size = new System.Drawing.Size(134, 108);
            lblDbInfo.TabIndex = 2;
            lblDbInfo.Text = "Informationen:";
            //
            // lblDbDescription
            //
            lblDbDescription.AllowHtmlString = true;
            lblDbDescription.Appearance.Options.UseTextOptions = true;
            lblDbDescription.Appearance.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Top;
            lblDbDescription.Appearance.TextOptions.WordWrap = DevExpress.Utils.WordWrap.Wrap;
            lblDbDescription.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.Vertical;

            lblDbDescription.Anchor = System.Windows.Forms.AnchorStyles.Left;
            lblDbDescription.Location = new System.Drawing.Point(148, 44);
            lblDbDescription.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            lblDbDescription.Appearance.TextOptions.WordWrap = DevExpress.Utils.WordWrap.Wrap;
            lblDbDescription.Name = "lblDbDescription";
            lblDbDescription.Padding = new System.Windows.Forms.Padding(10, 8, 10, 8);

            lblDbDescription.Size = new System.Drawing.Size(622, 108);
            lblDbDescription.TabIndex = 3;
            //
            // UcDbStep1DbType
            //
            AutoScaleDimensions = new System.Drawing.SizeF(9F, 19F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            Controls.Add(tablePanelRoot);
            Size = new System.Drawing.Size(957, 160);

            ((System.ComponentModel.ISupportInitialize)cmbDbType.Properties).EndInit();
            resources.ApplyResources(lblDbType, "lblDbType");
            resources.ApplyResources(lblDbInfo, "lblDbInfo");
            ResumeLayout(false);
        }

        private System.Windows.Forms.TableLayoutPanel tablePanelRoot;
        private DevExpress.XtraEditors.LabelControl lblDbType;
        private DevExpress.XtraEditors.ImageComboBoxEdit cmbDbType;
        private DevExpress.XtraEditors.LabelControl lblDbInfo;
        private DevExpress.XtraEditors.LabelControl lblDbDescription;
    }
}

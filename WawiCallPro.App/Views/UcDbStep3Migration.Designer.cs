#nullable disable
namespace WawiCallPro.App.Views
{
    partial class UcDbStep3Migration
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(UcDbStep3Migration));
            tablePanelRoot = new System.Windows.Forms.TableLayoutPanel();
            picResult = new System.Windows.Forms.PictureBox();
            memoResult = new DevExpress.XtraEditors.MemoEdit();
            progressPanel = new DevExpress.XtraWaitForm.ProgressPanel();

            ((System.ComponentModel.ISupportInitialize)picResult).BeginInit();
            ((System.ComponentModel.ISupportInitialize)memoResult.Properties).BeginInit();
            SuspendLayout();
            //
            // tablePanelRoot
            //
            tablePanelRoot.ColumnCount = 2;
            tablePanelRoot.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            tablePanelRoot.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            tablePanelRoot.Controls.Add(picResult, 0, 0);
            tablePanelRoot.Controls.Add(memoResult, 1, 0);
            tablePanelRoot.Controls.Add(progressPanel, 0, 0);
            tablePanelRoot.Dock = System.Windows.Forms.DockStyle.Fill;
            tablePanelRoot.Location = new System.Drawing.Point(0, 0);
            tablePanelRoot.Name = "tablePanelRoot";
            tablePanelRoot.Padding = new System.Windows.Forms.Padding(5);
            tablePanelRoot.RowCount = 1;
            tablePanelRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            tablePanelRoot.Size = new System.Drawing.Size(880, 527);
            tablePanelRoot.TabIndex = 0;
            //
            // picResult
            //

            picResult.Dock = System.Windows.Forms.DockStyle.Fill;
            picResult.Location = new System.Drawing.Point(8, 8);
            picResult.Name = "picResult";

            picResult.Size = new System.Drawing.Size(66, 511);
            picResult.SizeMode = System.Windows.Forms.PictureBoxSizeMode.CenterImage;
            picResult.TabIndex = 0;
            picResult.TabStop = false;
            //
            // memoResult
            //

            memoResult.Dock = System.Windows.Forms.DockStyle.Fill;
            memoResult.Location = new System.Drawing.Point(80, 8);
            memoResult.Name = "memoResult";
            memoResult.Properties.ReadOnly = true;

            memoResult.Size = new System.Drawing.Size(792, 511);
            memoResult.TabIndex = 1;
            //
            // progressPanel
            //

            tablePanelRoot.SetColumnSpan(progressPanel, 2);
            progressPanel.Anchor = System.Windows.Forms.AnchorStyles.None;
            progressPanel.Caption = "Datenbank wird eingerichtet";
            progressPanel.Description = "Migration wird ausgeführt. Bitte warten...";
            progressPanel.Location = new System.Drawing.Point(180, 206);
            progressPanel.Name = "progressPanel";

            progressPanel.Size = new System.Drawing.Size(520, 100);
            progressPanel.TabIndex = 2;
            progressPanel.Visible = false;
            //
            // UcDbStep3Migration
            //
            AutoScaleDimensions = new System.Drawing.SizeF(9F, 19F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            Controls.Add(tablePanelRoot);
            Margin = new System.Windows.Forms.Padding(4);
            Size = new System.Drawing.Size(880, 527);

            ((System.ComponentModel.ISupportInitialize)picResult).EndInit();
            ((System.ComponentModel.ISupportInitialize)memoResult.Properties).EndInit();
            resources.ApplyResources(progressPanel, "progressPanel");
            ResumeLayout(false);
        }

        private System.Windows.Forms.TableLayoutPanel tablePanelRoot;
        private PictureBox picResult;
        private DevExpress.XtraEditors.MemoEdit memoResult;
        private DevExpress.XtraWaitForm.ProgressPanel progressPanel;
    }
}

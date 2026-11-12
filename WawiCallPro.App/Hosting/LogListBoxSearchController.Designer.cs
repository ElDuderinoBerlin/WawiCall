namespace WawiCallPro.App.Hosting;

partial class LogListBoxSearchController
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null)
            components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        searchLayout = new System.Windows.Forms.TableLayoutPanel();
        searchLabel = new DevExpress.XtraEditors.LabelControl();
        _txtSearch = new DevExpress.XtraEditors.ButtonEdit();
        _lblResult = new DevExpress.XtraEditors.LabelControl();
        closeButton = new DevExpress.XtraEditors.Controls.EditorButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Clear);
        ((System.ComponentModel.ISupportInitialize)_txtSearch.Properties).BeginInit();
        searchLayout.SuspendLayout();
        SuspendLayout();
        searchLayout.Name = "searchLayout";
        searchLayout.AutoSize = true;
        searchLayout.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        searchLayout.Dock = System.Windows.Forms.DockStyle.Fill;
        searchLayout.ColumnCount = 3;
        searchLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
        searchLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        searchLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
        searchLayout.Margin = new System.Windows.Forms.Padding(0);
        searchLayout.RowCount = 1;
        searchLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        searchLayout.Controls.Add(searchLabel, 0, 0);
        searchLayout.Controls.Add(_txtSearch, 1, 0);
        searchLayout.Controls.Add(_lblResult, 2, 0);
        searchLabel.Name = "searchLabel";
        searchLabel.Text = "Suche";
        searchLabel.Margin = new System.Windows.Forms.Padding(0, 0, 6, 0);
        searchLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
        _txtSearch.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        _txtSearch.Name = "txtSearch";
        _txtSearch.Margin = new System.Windows.Forms.Padding(0);

        closeButton.ToolTip = "Suche schließen";
        _txtSearch.Properties.Buttons.Clear();
        _txtSearch.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] { closeButton });
        _txtSearch.Properties.NullValuePrompt = "Suchtext";
        _txtSearch.Properties.NullValuePromptShowForEmptyValue = true;
        _txtSearch.ToolTip = "Mindestens 2 Zeichen eingeben.";
        _txtSearch.ButtonClick += CloseSearch_Click;
        _txtSearch.EditValueChanged += SearchText_EditValueChanged;
        _txtSearch.KeyDown += SearchKeyDown;
        _lblResult.Name = "lblResult";
        _lblResult.Visible = false;
        _lblResult.Margin = new System.Windows.Forms.Padding(6, 0, 0, 0);
        _lblResult.Anchor = System.Windows.Forms.AnchorStyles.Left;
        AutoScaleDimensions = new System.Drawing.SizeF(9F, 19F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        AutoSize = true;
        AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        Dock = System.Windows.Forms.DockStyle.Bottom;
        Padding = new System.Windows.Forms.Padding(4, 2, 0, 2);
        Visible = false;
        Controls.Add(searchLayout);
        ((System.ComponentModel.ISupportInitialize)_txtSearch.Properties).EndInit();
        searchLayout.ResumeLayout(false);
        searchLayout.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }

    private System.Windows.Forms.TableLayoutPanel searchLayout;
    private DevExpress.XtraEditors.LabelControl searchLabel;
    private DevExpress.XtraEditors.ButtonEdit _txtSearch;
    private DevExpress.XtraEditors.LabelControl _lblResult;
    private DevExpress.XtraEditors.Controls.EditorButton closeButton;
}

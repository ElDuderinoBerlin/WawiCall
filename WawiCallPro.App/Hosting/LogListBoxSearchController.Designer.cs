namespace WawiCallPro.App.Hosting;

partial class LogListBoxSearchController
{
    private System.ComponentModel.IContainer components = null;
    private System.Windows.Forms.TableLayoutPanel searchLayout;
    private DevExpress.XtraEditors.LabelControl searchLabel;
    private DevExpress.XtraEditors.TextEdit _txtSearch;
    private DevExpress.XtraEditors.LabelControl _lblResult;
    private DevExpress.XtraEditors.SimpleButton closeButton;

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
        _txtSearch = new DevExpress.XtraEditors.TextEdit();
        _lblResult = new DevExpress.XtraEditors.LabelControl();
        closeButton = new DevExpress.XtraEditors.SimpleButton();
        SuspendLayout();
        searchLayout.Name = "searchLayout";
        searchLayout.AutoSize = true;
        searchLayout.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        searchLayout.Dock = System.Windows.Forms.DockStyle.Fill;
        searchLayout.ColumnCount = 4;
        searchLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
        searchLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        searchLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
        searchLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
        searchLayout.RowCount = 1;
        searchLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        searchLayout.Controls.Add(searchLabel, 0, 0);
        searchLayout.Controls.Add(_txtSearch, 1, 0);
        searchLayout.Controls.Add(_lblResult, 2, 0);
        searchLayout.Controls.Add(closeButton, 3, 0);
        searchLabel.Name = "searchLabel";
        searchLabel.Text = "Suche";
        searchLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
        _txtSearch.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            _txtSearch.Name = "txtSearch";

        _txtSearch.Properties.NullValuePrompt = "Mindestens 2 Zeichen";
        _txtSearch.Properties.NullValuePromptShowForEmptyValue = true;
        _txtSearch.EditValueChanged += SearchText_EditValueChanged;
        _txtSearch.KeyDown += SearchKeyDown;
        _lblResult.Name = "lblResult";
        _lblResult.Anchor = System.Windows.Forms.AnchorStyles.Left;
        closeButton.Name = "closeButton";
        closeButton.Text = "X";
        closeButton.AutoSize = true;
        closeButton.Anchor = System.Windows.Forms.AnchorStyles.Right;
        closeButton.ToolTip = "Suche schließen";
        closeButton.Click += CloseSearch_Click;
        AutoScaleDimensions = new System.Drawing.SizeF(9F, 19F);
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        AutoSize = true;
        AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        Dock = System.Windows.Forms.DockStyle.Bottom;
        Padding = new System.Windows.Forms.Padding(8);
        Visible = false;
        Controls.Add(searchLayout);
        ResumeLayout(false);
        PerformLayout();
    }
}

namespace WawiCallPro.App.Views;

public partial class FunctionPluginsPage
{
    protected override void Dispose(bool disposing)
    {
        if (disposing) DisposePage();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        layout = new System.Windows.Forms.TableLayoutPanel();
        actions = new System.Windows.Forms.TableLayoutPanel();
        plugins = new DevExpress.XtraEditors.ListBoxControl();
        toggleButton = new DevExpress.XtraEditors.SimpleButton();
        executeButton = new DevExpress.XtraEditors.SimpleButton();
        ((System.ComponentModel.ISupportInitialize)plugins).BeginInit();
        layout.SuspendLayout();
        actions.SuspendLayout();
        SuspendLayout();
        layout.ColumnCount = 1;
        layout.RowCount = 2;
        layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
        layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        layout.Dock = System.Windows.Forms.DockStyle.Fill;
        layout.Padding = new System.Windows.Forms.Padding(10);
        layout.Controls.Add(plugins, 0, 0);
        layout.Controls.Add(actions, 0, 1);
        actions.ColumnCount = 3;
        actions.RowCount = 1;
        actions.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
        actions.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
        actions.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        actions.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        actions.AutoSize = true;
        actions.Dock = System.Windows.Forms.DockStyle.Top;
        actions.Controls.Add(toggleButton, 0, 0);
        actions.Controls.Add(executeButton, 1, 0);
        plugins.Dock = System.Windows.Forms.DockStyle.Fill;
        plugins.AccessibleName = "Installierte Funktionsplugins";
        plugins.DisplayMember = "Text";
        plugins.SelectedIndexChanged += Plugins_SelectedIndexChanged;
        toggleButton.Text = "Aktivieren";
        toggleButton.Click += Toggle_Click;
        executeButton.Text = "Ausführen";
        executeButton.Click += Execute_Click;
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
        Controls.Add(layout);
        ((System.ComponentModel.ISupportInitialize)plugins).EndInit();
        actions.ResumeLayout(false);
        layout.ResumeLayout(false);
        ResumeLayout(false);
    }

    private System.Windows.Forms.TableLayoutPanel layout;
    private System.Windows.Forms.TableLayoutPanel actions;
    private DevExpress.XtraEditors.ListBoxControl plugins;
    private DevExpress.XtraEditors.SimpleButton toggleButton;
    private DevExpress.XtraEditors.SimpleButton executeButton;
}

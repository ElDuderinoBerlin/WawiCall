using DevExpress.XtraEditors;

namespace WawiCallPro.App.Views;

public partial class StartPage : XtraUserControl
{
    public StartPage() { InitializeComponent(); }
    internal event Action<string>? Navigate;

    internal void SetContent(string title, string body, string status, bool showActions = true)
    {
        titleLabel.Text = title;
        bodyLabel.Text = body;
        statusLabel.Text = status;
        if (!showActions)
        {
            layout.Controls.Remove(settingsButton);
            layout.Controls.Remove(historyButton);
        }
    }

    private void Settings_Click(object? sender, EventArgs e) => Navigate?.Invoke("settings");
    private void History_Click(object? sender, EventArgs e) => Navigate?.Invoke("history");
    private void DisposePage()
    {
        settingsButton.Dispose(); historyButton.Dispose();
    }
}

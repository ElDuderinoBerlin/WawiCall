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
            layout.Controls.Remove(databaseButton);
            layout.Controls.Remove(settingsButton);
            layout.Controls.Remove(pluginButton);
            layout.Controls.Remove(historyButton);
        }
    }

    private void Database_Click(object? sender, EventArgs e) => Navigate?.Invoke("database");
    private void Settings_Click(object? sender, EventArgs e) => Navigate?.Invoke("settings");
    private void Plugin_Click(object? sender, EventArgs e) => Navigate?.Invoke("plugin");
    private void History_Click(object? sender, EventArgs e) => Navigate?.Invoke("history");
    private void DisposePage()
    {
        databaseButton.Dispose(); settingsButton.Dispose(); pluginButton.Dispose(); historyButton.Dispose();
    }
}

namespace WawiCallPro.App.Hosting;

/// <summary>Views that can open against an isolated profile without a business or SIP connection.</summary>
internal sealed record AppDocumentationView(string NavigationId, string AuditSurface)
{
    internal static AppDocumentationView Parse(string name) => name.Trim().ToLowerInvariant() switch
    {
        "home" => new("home", "MainForm"),
        "history" => new("history", "HistoryPage"),
        "mail" => new("mail", "MailPage"),
        "settings" => new("settings", "FrmSettings"),
        "plugin" => new("plugin", "PluginPage"),
        "dial" => new("dial", "FrmDial"),
        _ => throw new ArgumentException("Unbekannte Ansicht für die Dokumentationsautomation.")
    };

    internal void Open(MainForm main)
    {
        if (NavigationId == "dial") main.CreateDialDialog().Show(main);
        else main.Navigate(NavigationId);
    }
}

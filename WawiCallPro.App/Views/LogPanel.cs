using System.Net;
using DevExpress.XtraEditors;
using Microsoft.Extensions.Logging;
using WawiCallPro.App.Hosting;

namespace WawiCallPro.App.Views;

public partial class LogPanel : XtraUserControl
{
    private ILogWindowService? _logs;
    public LogPanel()
    {
        InitializeComponent();
        searchController.Bind(listLog);
        LogListBoxClipboardHelper.AttachContextMenu(listLog);
        LogListBoxToolTipHelper.Attach(listLog, components!);
    }
    internal void Bind(ILogWindowService logs) { _logs = logs; Drain(); refreshTimer.Start(); }
    internal void Append(string message, LogLevel level = LogLevel.Information)
    {
        Append(new(DateTimeOffset.Now, level, message));
        LogListBoxSearchController.ApplyCurrentSearch(listLog);
        listLog.TopIndex = listLog.ItemCount - 1;
    }
    private void Append(UiLogEntry entry)
    {
        var text = WebUtility.HtmlDecode(entry.Message).Replace("<", "‹", StringComparison.Ordinal);
        var body = entry.Level switch
        {
            LogLevel.Warning => $"<b><color=Gold>Warnung:</color></b> <color=Gold>{text}</color>",
            LogLevel.Error or LogLevel.Critical => $"<b><color=Red>Fehler:</color></b> <color=Red>{text}</color>",
            _ => text
        };
        listLog.Items.Add(new LogListBoxItem($"<color=Gray>{entry.Timestamp:HH:mm:ss}</color>: {body}"));
        while (listLog.ItemCount > 2000) listLog.Items.RemoveAt(0);
    }
    internal void Drain()
    {
        if (_logs is null) return;
        var entries = _logs.Drain();
        if (entries.Count == 0) return;
        listLog.BeginUpdate();
        try { foreach (var entry in entries) Append(entry); }
        finally { listLog.EndUpdate(); }
        LogListBoxSearchController.ApplyCurrentSearch(listLog);
        listLog.TopIndex = listLog.ItemCount - 1;
    }
    private void Refresh_Tick(object? sender, EventArgs e) => Drain();
    private void Log_KeyDown(object? sender, KeyEventArgs e)
    {
        if (!searchController.HandleLogKeyDown(e)) LogListBoxClipboardHelper.HandleKeyDown(listLog, e);
    }
}

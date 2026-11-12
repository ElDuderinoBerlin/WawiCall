using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using DevExpress.Utils;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;

namespace WawiCallPro.App.Hosting;

internal sealed partial class LogListBoxSearchController : XtraUserControl
{
    private static readonly ConditionalWeakTable<ListBoxControl, LogListBoxSearchController> Controllers = new();

    private ListBoxControl _listLog;

    public LogListBoxSearchController()
    {
        _listLog = null!;
        InitializeComponent();
    }

    internal void Bind(ListBoxControl listLog)
    {
        _listLog = listLog;
        Controllers.Add(listLog, this);
    }

    private void SearchText_EditValueChanged(object? sender, EventArgs e)
    {
        if (_listLog is not null)
            ApplySearch(selectFirstMatch: true);
    }

    private void CloseSearch_Click(object? sender, EventArgs e) => HideSearch();

    public bool HandleLogKeyDown(KeyEventArgs e)
    {
        if (e.KeyData != (Keys.Control | Keys.F))
            return false;

        if (Visible) HideSearch();
        else ShowSearch();
        e.SuppressKeyPress = true;
        e.Handled = true;
        return true;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (HandleLogKeyDown(new KeyEventArgs(keyData))) return true;
        return base.ProcessCmdKey(ref msg, keyData);
    }

    public static void ApplyCurrentSearch(ListBoxControl listLog)
    {
        if (Controllers.TryGetValue(listLog, out var controller))
            controller.ApplySearch(selectFirstMatch: false);
    }

    private void ShowSearch()
    {
        this.Visible = true;
        this.BringToFront();
        _listLog.SendToBack();
        this.Parent?.PerformLayout();
        _txtSearch.Focus();
        _txtSearch.SelectAll();
    }

    private void HideSearch()
    {
        _txtSearch.Text = string.Empty;
        this.Visible = false;
        ClearHighlights();
        _listLog.Focus();
    }

    private void SearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            HideSearch();
            e.SuppressKeyPress = true;
            e.Handled = true;
            return;
        }

        if (e.KeyCode == Keys.Enter)
        {
            SelectNextMatch();
            e.SuppressKeyPress = true;
            e.Handled = true;
        }
    }

    private void ApplySearch(bool selectFirstMatch)
    {
        var searchText = _txtSearch.Text.Trim();
        if (searchText.Length < 2)
        {
            ClearHighlights();
            return;
        }

        var matchCount = 0;
        var firstMatchIndex = -1;

        _listLog.BeginUpdate();
        try
        {
            for (var i = 0; i < _listLog.ItemCount; i++)
            {
                var item = GetItem(i);
                var (highlighted, count) = Highlight(item.HtmlText, searchText);
                item.DisplayText = highlighted;
                matchCount += count;
                if (firstMatchIndex < 0 && count > 0)
                    firstMatchIndex = i;
            }
        }
        finally
        {
            _listLog.EndUpdate();
        }

        _lblResult.Text = matchCount == 1 ? "1 Treffer" : $"{matchCount} Treffer";

        if (!selectFirstMatch || firstMatchIndex < 0)
            return;

        _listLog.SelectedIndex = firstMatchIndex;
        _listLog.TopIndex = firstMatchIndex;
    }

    private void SelectNextMatch()
    {
        var searchText = _txtSearch.Text.Trim();
        if (searchText.Length < 2 || _listLog.ItemCount == 0)
            return;

        var start = Math.Max(0, _listLog.SelectedIndex + 1);
        for (var offset = 0; offset < _listLog.ItemCount; offset++)
        {
            var index = (start + offset) % _listLog.ItemCount;
            if (!GetItem(index).HtmlText.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                continue;

            _listLog.SelectedIndex = index;
            _listLog.TopIndex = index;
            return;
        }
    }

    private void ClearHighlights()
    {
        _listLog.BeginUpdate();
        try
        {
            for (var i = 0; i < _listLog.ItemCount; i++)
            {
                var item = GetItem(i);
                item.DisplayText = item.HtmlText;
            }
        }
        finally
        {
            _listLog.EndUpdate();
        }

        _lblResult.Text = string.Empty;
    }

    private LogListBoxItem GetItem(int index)
    {
        if (_listLog.Items[index] is LogListBoxItem item)
            return item;

        item = new LogListBoxItem(_listLog.Items[index]?.ToString() ?? string.Empty);
        _listLog.Items[index] = item;
        return item;
    }

    private static (string Text, int Count) Highlight(string htmlText, string searchText)
    {
        var sb = new StringBuilder(htmlText.Length + 64);
        var count = 0;

        for (var i = 0; i < htmlText.Length;)
        {
            if (htmlText[i] == '<')
            {
                var end = htmlText.IndexOf('>', i);
                if (end < 0)
                {
                    sb.Append(htmlText, i, htmlText.Length - i);
                    break;
                }

                sb.Append(htmlText, i, end - i + 1);
                i = end + 1;
                continue;
            }

            var nextTag = htmlText.IndexOf('<', i);
            var length = nextTag < 0 ? htmlText.Length - i : nextTag - i;
            var segment = htmlText.Substring(i, length);
            sb.Append(SearchRegex(searchText).Replace(segment, match =>
            {
                count++;
                return $"<backcolor=Yellow><color=Black>{match.Value}</color></backcolor>";
            }));
            i += length;
        }

        return (sb.ToString(), count);
    }

    private static Regex SearchRegex(string searchText)
        => new(Regex.Escape(searchText), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}

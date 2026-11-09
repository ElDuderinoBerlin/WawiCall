using DevExpress.LookAndFeel.Design;
using DevExpress.Utils.Menu;
using DevExpress.XtraEditors;

namespace WawiCallPro.App.Hosting;

internal static partial class LogListBoxClipboardHelper
{
    public static void AttachContextMenu(ListBoxControl listLog)
    {
        listLog.MouseUp += (_, e) =>
        {
            if (e.Button != MouseButtons.Right) return;

            var index = listLog.IndexFromPoint(e.Location);
            if (index >= 0)
            {
                var item = listLog.Items[index];
                if (!listLog.SelectedItems.Contains(item))
                    listLog.SelectedIndex = index;
            }

            ShowContextMenu(listLog, e.Location);
        };
    }

    public static void HandleKeyDown(ListBoxControl listLog, KeyEventArgs e)
    {
        switch (e.Control)
        {
            case true when e.KeyCode == Keys.A:
            {
                SelectAll(listLog);
                e.Handled = true;
                break;
            }
            case true when e.KeyCode == Keys.C:
            {
                CopySelected(listLog);
                e.Handled = true;
                break;
            }
            case false when e.KeyCode == Keys.Delete:
            {
                ConfirmClearAllSelected(listLog);
                e.Handled = true;
                break;
            }
        }
    }

    private static void SelectAll(ListBoxControl listLog)
    {
        for (var i = 0; i < listLog.ItemCount; i++)
            listLog.SetSelected(i, true);
    }

    private static void CopySelected(ListBoxControl listLog)
    {
        CopyItems(listLog.SelectedItems);
    }

    private static void CopyAll(ListBoxControl listLog)
    {
        CopyItems(listLog.Items.Cast<object>());
    }

    private static void ClearAll(ListBoxControl listLog)
    {
        listLog.Items.Clear();
        LogListBoxSearchController.ApplyCurrentSearch(listLog);
    }

    private static void ConfirmClearAllSelected(ListBoxControl listLog)
    {
        if (listLog.ItemCount == 0 || listLog.SelectedItems.Count != listLog.ItemCount)
            return;

        var result = XtraMessageBox.Show(
            listLog,
            "Alle Logeinträge löschen?",
            "Log löschen",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result == DialogResult.Yes)
            ClearAll(listLog);
    }

    private static void CopyItems(IEnumerable<object> items)
    {
        var lines = items
            .Select(item => LogListBoxText.ToPlainText(item.ToString() ?? string.Empty))
            .ToList();

        if (lines.Count == 0) return;

        Clipboard.SetText(string.Join(Environment.NewLine, lines));
    }

    private static void ShowContextMenu(ListBoxControl listLog, Point location)
    {
        var hasSelected = listLog.SelectedItems.Count > 0;
        var hasItems = listLog.ItemCount > 0;

        if (!hasSelected && !hasItems) return;

        var menu = new DXPopupMenu();
        menu.Items.Add(new DXMenuItem("In die Zwischenablage kopieren", (_, _) => CopySelected(listLog))
        {
            Enabled = hasSelected
        });
        menu.Items.Add(new DXMenuItem("Alle in die Zwischenablage kopieren", (_, _) => CopyAll(listLog))
        {
            Enabled = hasItems
        });
        menu.Items.Add(new DXMenuItem("Alle löschen", (_, _) => ClearAll(listLog))
        {
            BeginGroup = true,
            Enabled = hasItems
        });

        ((IDXDropDownControl)menu).Show(new SkinMenuManager(UserLookAndFeelDefault.Default), listLog, location);
    }
}

using System.Collections.Immutable;
using DevExpress.Office.Services;
using DevExpress.XtraEditors;
using WawiCallPro.App.Hosting;
using WawiCallPro.Interfaces.Office;

namespace WawiCallPro.App.Views;

public partial class MailPage : XtraUserControl
{
    private IMailViewSource? _source;
    private AppUiState? _state;
    private readonly CancellationTokenSource _lifetime = new();
    private CancellationTokenSource? _catalogCancellation, _listCancellation, _previewCancellation;
    private readonly List<Task> _pending = [];
    private Task? _stopTask;
    private bool _binding, _stopping, _disposed, _arranging, _preferBottom;
    private MailFolderKey? _folder;
    private string _search = "";
    private ImmutableArray<MailFolder> _folders = [];

    public MailPage()
    {
        InitializeComponent();
        UiButtonSizing.Attach(this);
        body.ReplaceService<IUriStreamService>(new BlockedMailResources());
        FontChanged += Appearance_Changed; SizeChanged += Appearance_Changed; DpiChangedAfterParent += Appearance_Changed;
        var appearance = LookAndFeel; appearance.StyleChanged += Appearance_Changed;
        Disposed += (_, _) => appearance.StyleChanged -= Appearance_Changed;
        Appearance_Changed(this, EventArgs.Empty);
    }

    internal void Bind(IMailViewSource source, AppUiState? state)
    {
        if (_source is not null) throw new InvalidOperationException("The mail page is already bound.");
        _source = source ?? throw new ArgumentNullException(nameof(source)); _state = state;
        _state?.Restore(messagesView, "MailMessages_v1"); reloadButton.Enabled = true; searchTimer.Start();
    }

    internal Task ReloadAsync()
    {
        if (_source is null || _stopping) return Task.CompletedTask;
        var token = Renew(ref _catalogCancellation);
        Cancel(ref _listCancellation); Cancel(ref _previewCancellation); searchTimer.Stop();
        return Track(LoadCatalogAsync(token));
    }
    private async Task LoadCatalogAsync(CancellationToken token)
    {
        reloadButton.Enabled = false; folderTree.Enabled = messagesGrid.Enabled = false; statusLabel.Text = "Postfachordner werden geladen …";
        ClearPreview("Ordner werden geladen …");
        try
        {
            var catalog = await _source!.GetFoldersAsync(token); token.ThrowIfCancellationRequested();
            ValidateFolders(catalog.Folders);
            _folders = catalog.Folders;
            var selected = _folders.FirstOrDefault(folder => folder.Key == _folder) ?? _folders.FirstOrDefault(folder => folder.Parent is not null) ?? _folders.FirstOrDefault();
            _binding = true;
            try
            {
                folderTree.DataSource = _folders.Select(folder => new FolderRow(folder.Key, folder.Parent,
                    folder.UnreadCount is > 0 ? $"{folder.Name} ({folder.UnreadCount})" : folder.Name)).ToArray();
                folderTree.ExpandAll(); _folder = selected?.Key;
                if (_folder is not null) folderTree.FocusedNode = folderTree.FindNodeByKeyID(_folder);
            }
            finally { _binding = false; }
            if (_folder is null)
            {
                messagesGrid.DataSource = Array.Empty<MessageRow>(); folderLabel.Text = "Postfach";
                statusLabel.Text = catalog.Status; ClearPreview("Kein Postfach verbunden."); return;
            }
            await LoadCurrentFolderAsync();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception exception)
        {
            if (!token.IsCancellationRequested)
            {
                _binding = true;
                try { folderTree.DataSource = Array.Empty<FolderRow>(); _folders = []; _folder = null; }
                finally { _binding = false; }
                ClearMailList(); statusLabel.Text = PageOperation.Describe(exception);
            }
        }
        finally
        {
            if (!_stopping && !token.IsCancellationRequested)
            { reloadButton.Enabled = folderTree.Enabled = messagesGrid.Enabled = true; searchTimer.Start(); }
        }
    }

    internal Task LoadCurrentFolderAsync()
    {
        if (_source is null || _folder is null || _stopping) return Task.CompletedTask;
        var token = Renew(ref _listCancellation); Cancel(ref _previewCancellation);
        _search = messagesView.FindFilterText ?? "";
        return Track(LoadMessagesAsync(_folder, _search, token));
    }
    private async Task LoadMessagesAsync(MailFolderKey folder, string search, CancellationToken token)
    {
        var previous = (messagesView.GetFocusedRow() as MessageRow)?.Key;
        folderLabel.Text = _folders.First(item => item.Key == folder).Name;
        ClearPreview("Nachrichten werden geladen …"); statusLabel.Text = "Nachrichten werden geladen …";
        try
        {
            var items = await _source!.GetMessagesAsync(folder, search, token); token.ThrowIfCancellationRequested();
            if (search != (messagesView.FindFilterText ?? "")) return;
            if (items.IsDefault || items.Any(item => item is null || item.Folder != folder || item.Key is null
                || item.Key.Provider != folder.Provider || item.Key.Account != folder.Account || string.IsNullOrWhiteSpace(item.Key.Id))
                || items.Select(item => item.Key).Distinct().Count() != items.Length)
                throw new InvalidOperationException("Invalid mail source snapshot.");
            _binding = true;
            try
            {
                messagesGrid.DataSource = items.OrderByDescending(item => item.Date).Select(item => new MessageRow(item)).ToArray();
                if (previous is not null && messagesView.LocateByValue("Key", previous) is var handle && handle >= 0) messagesView.FocusedRowHandle = handle;
            }
            finally { _binding = false; }
            statusLabel.Text = items.IsEmpty ? "Keine Nachrichten für diese Auswahl." : $"{items.Length} Nachrichten · Strg+F zum Suchen";
            await LoadFocusedMessageAsync();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception exception) { if (!token.IsCancellationRequested) { ClearMailList(); statusLabel.Text = PageOperation.Describe(exception); } }
    }

    internal Task LoadFocusedMessageAsync()
    {
        var token = Renew(ref _previewCancellation);
        if (_source is null || _stopping || messagesView.GetFocusedRow() is not MessageRow row)
        { ClearPreview("Wählen Sie eine Nachricht aus."); return Task.CompletedTask; }
        return Track(LoadMessageAsync(row.Key, token));
    }
    private async Task LoadMessageAsync(MailMessageKey key, CancellationToken token)
    {
        ClearPreview("Nachricht wird geladen …");
        try
        {
            var message = await _source!.GetMessageAsync(key, token); token.ThrowIfCancellationRequested();
            if (message is null) { ClearPreview("Die Nachricht ist nicht mehr verfügbar."); return; }
            if (message.Key != key || message.Attachments.IsDefault) throw new InvalidOperationException("Unexpected mail identity.");
            subjectLabel.Text = string.IsNullOrWhiteSpace(message.Subject) ? "(Kein Betreff)" : message.Subject;
            senderLabel.Text = "Von: " + message.From;
            recipientsLabel.Text = "An: " + message.To + (string.IsNullOrWhiteSpace(message.Cc) ? "" : "\r\nCc: " + message.Cc);
            dateLabel.Text = message.Date.LocalDateTime.ToString("f");
            attachmentsLabel.Text = message.Attachments.IsEmpty ? "" : (message.Attachments.Length == 1 ? "Anhang: " : "Anhänge: ")
                + string.Join(" · ", message.Attachments.Select(attachment => attachment.Name));
            attachmentsLabel.Visible = !message.Attachments.IsEmpty;
            if (string.IsNullOrWhiteSpace(message.Html)) body.Text = message.Text;
            else body.HtmlText = message.Html;
            ApplyReaderAppearance();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception exception) { if (!token.IsCancellationRequested) { ClearPreview("Nachricht konnte nicht geladen werden."); statusLabel.Text = PageOperation.Describe(exception); } }
    }

    private void ClearMailList()
    {
        _binding = true;
        try { messagesGrid.DataSource = Array.Empty<MessageRow>(); }
        finally { _binding = false; }
        ClearPreview("Keine Nachricht ausgewählt.");
    }
    private void ClearPreview(string text)
    {
        subjectLabel.Text = text; senderLabel.Text = recipientsLabel.Text = dateLabel.Text = attachmentsLabel.Text = "";
        attachmentsLabel.Visible = false; body.Text = ""; ApplyReaderAppearance();
    }
    private static void ValidateFolders(ImmutableArray<MailFolder> folders)
    {
        if (folders.IsDefault || folders.Any(folder => folder is null || folder.Key is null || string.IsNullOrWhiteSpace(folder.Key.Provider)
            || string.IsNullOrWhiteSpace(folder.Key.Account) || string.IsNullOrWhiteSpace(folder.Key.Id))
            || folders.Select(folder => folder.Key).Distinct().Count() != folders.Length) throw new InvalidOperationException("Invalid mail folders.");
        var byKey = folders.ToDictionary(folder => folder.Key);
        foreach (var folder in folders)
        {
            var visited = new HashSet<MailFolderKey> { folder.Key };
            var parent = folder.Parent;
            while (parent is not null)
            {
                if (parent.Provider != folder.Key.Provider || parent.Account != folder.Key.Account || !byKey.TryGetValue(parent, out var ancestor) || !visited.Add(parent))
                    throw new InvalidOperationException("Invalid mail folder hierarchy.");
                parent = ancestor.Parent;
            }
        }
    }
    private CancellationToken Renew(ref CancellationTokenSource? source)
    {
        Cancel(ref source); source = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token); return source.Token;
    }
    private static void Cancel(ref CancellationTokenSource? source) { source?.Cancel(); source?.Dispose(); source = null; }
    private Task Track(Task task) { _pending.RemoveAll(pending => pending.IsCompleted); _pending.Add(task); return task; }
    internal Task StopAsync() => _stopTask ??= _disposed ? Task.WhenAll(_pending.ToArray()) : StopCoreAsync();
    private async Task StopCoreAsync()
    {
        _stopping = true; reloadButton.Enabled = false; folderTree.Enabled = messagesGrid.Enabled = false; searchTimer.Stop();
        _lifetime.Cancel(); await Task.WhenAll(_pending.ToArray());
    }
    private void DisposePage()
    {
        if (_disposed) return;
        _disposed = true;
        _stopping = true; _lifetime.Cancel(); Cancel(ref _catalogCancellation); Cancel(ref _listCancellation); Cancel(ref _previewCancellation);
        _state?.Save(messagesView, "MailMessages_v1"); _lifetime.Dispose();
    }
    private async void MailPage_Load(object? sender, EventArgs e) => await ReloadAsync();
    private async void Reload_Click(object? sender, EventArgs e) => await ReloadAsync();
    private async void Folder_FocusedNodeChanged(object sender, DevExpress.XtraTreeList.FocusedNodeChangedEventArgs e)
    {
        if (_binding || _stopping || folderTree.GetDataRecordByNode(e.Node) is not FolderRow folder) return;
        _folder = folder.Key; await LoadCurrentFolderAsync();
    }
    private async void Message_FocusedRowChanged(object sender, DevExpress.XtraGrid.Views.Base.FocusedRowChangedEventArgs e)
    { if (!_binding && !_stopping) await LoadFocusedMessageAsync(); }
    private void Message_FilterChanged(object? sender, EventArgs e)
    {
        if (_binding || _stopping || _search == (messagesView.FindFilterText ?? "")) return;
        Cancel(ref _listCancellation); Cancel(ref _previewCancellation);
    }
    private async void Search_Tick(object? sender, EventArgs e)
    { if (_source is not null && _folder is not null && !_stopping && _search != (messagesView.FindFilterText ?? "")) await LoadCurrentFolderAsync(); }
    private void Message_RowStyle(object sender, DevExpress.XtraGrid.Views.Grid.RowStyleEventArgs e)
    { if (messagesView.GetRow(e.RowHandle) is MessageRow { IsRead: false }) e.Appearance.FontStyleDelta = FontStyle.Bold; }
    private void ReadingPosition_Click(object? sender, EventArgs e) { _preferBottom = !_preferBottom; Appearance_Changed(this, EventArgs.Empty); }
    private void Body_HyperlinkClick(object sender, DevExpress.XtraRichEdit.HyperlinkClickEventArgs e)
    { e.Handled = true; statusLabel.Text = "Zum Öffnen den Link kopieren."; }
    protected override bool ProcessCmdKey(ref Message message, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.F))
        {
            if (messagesView.IsFindPanelVisible) messagesView.HideFindPanel(); else messagesView.ShowFindPanel();
            return true;
        }
        return base.ProcessCmdKey(ref message, keyData);
    }
    private void Appearance_Changed(object? sender, EventArgs e)
    {
        if (_arranging || IsDisposed) return;
        _arranging = true;
        try
        {
            UiButtonSizing.Apply(reloadButton, readingPositionButton);
            var unit = Math.Max(Font.Height, LogicalToDeviceUnits(16));
            foldersSplit.SplitterPosition = Math.Min(unit * 11, Math.Max(unit * 6, Width / 4));
            var horizontal = !_preferBottom && Width >= unit * 62;
            contentSplit.Horizontal = horizontal;
            contentSplit.SplitterPosition = horizontal ? Math.Max(unit * 15, contentSplit.Width * 42 / 100) : Math.Max(unit * 7, contentSplit.Height * 40 / 100);
            readingPositionButton.Text = _preferBottom ? "Lesebereich rechts" : "Lesebereich unten";
            UiButtonSizing.Apply(reloadButton, readingPositionButton);
            ApplyReaderAppearance();
        }
        finally { _arranging = false; }
    }
    private void ApplyReaderAppearance()
    {
        body.Appearance.Text.ForeColor = ForeColor;
        body.Document.SetPageBackground(BackColor);
        body.Document.DefaultCharacterProperties.FontName = Font.Name;
        body.Document.DefaultCharacterProperties.FontSize = Font.Size;
    }

    private sealed record FolderRow(MailFolderKey Key, MailFolderKey? Parent, string Name);
    private sealed class MessageRow(MailListItem item)
    {
        public MailMessageKey Key => item.Key;
        public string Sender => item.Sender;
        public string Subject => string.IsNullOrWhiteSpace(item.Subject) ? "(Kein Betreff)" : item.Subject;
        public string Summary => Sender + "\r\n" + Subject + (item.AttachmentCount > 0 ? $"\r\n{item.AttachmentCount} " + (item.AttachmentCount == 1 ? "Anhang" : "Anhänge") : "");
        public string DateText => item.Date.LocalDateTime.ToString("dd.MM.yyyy") + "\r\n" + item.Date.LocalDateTime.ToString("HH:mm");
        public string Preview => item.Preview;
        public DateTime Date => item.Date.LocalDateTime;
        public string Attachments => item.AttachmentCount > 0 ? item.AttachmentCount.ToString() : "";
        public bool IsRead => item.IsRead;
    }
}

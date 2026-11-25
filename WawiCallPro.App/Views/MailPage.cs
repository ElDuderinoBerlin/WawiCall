using System.Collections.Immutable;
using System.IO;
using DevExpress.Office.Services;
using DevExpress.XtraEditors;
using DevExpress.XtraBars.Navigation;
using WawiCallPro.App.Hosting;
using WawiCallPro.Interfaces.Office;

namespace WawiCallPro.App.Views;

public partial class MailPage : XtraUserControl
{
    private IMailViewSource? _source;
    private WawiCallPro.Office.MailApplication? _application;
    private MailContent? _message;
    private bool _allowImport;
    private string _catalogStatus = "";
    private AppUiState? _state;
    private readonly CancellationTokenSource _lifetime = new();
    private CancellationTokenSource? _catalogCancellation, _listCancellation, _previewCancellation;
    private readonly List<Task> _pending = [];
    private Task? _stopTask;
    private bool _binding, _stopping, _disposed, _arranging, _preferBottom;
    private MailFolderKey? _folder;
    private string _search = "";
    private ImmutableArray<MailFolder> _folders = [];
    private readonly Dictionary<MailFolderKey, AccordionControlElement> _folderElements = new();
    private string _emptyListText = "Keine Nachrichten.";
    private static readonly Lazy<DevExpress.Utils.Svg.SvgImage> FolderIcon = new(() =>
    {
        using var stream = typeof(MailPage).Assembly.GetManifestResourceStream("WawiCallPro.App.Icons.MailFolder.svg")!;
        return DevExpress.Utils.Svg.SvgImage.FromStream(stream);
    });

    public MailPage()
    {
        InitializeComponent();
        ClearPreview("Wählen Sie eine Nachricht aus.");
        UiButtonSizing.Attach(this);
        body.ReplaceService<IUriStreamService>(new BlockedMailResources());
        FontChanged += Appearance_Changed; SizeChanged += Appearance_Changed; DpiChangedAfterParent += Appearance_Changed;
        var appearance = LookAndFeel; appearance.StyleChanged += Appearance_Changed;
        Disposed += (_, _) => appearance.StyleChanged -= Appearance_Changed;
        Appearance_Changed(this, EventArgs.Empty);
        attachmentEdit.Properties.Columns.Add(new DevExpress.XtraEditors.Controls.LookUpColumnInfo("Name"));
    }

    internal void Bind(IMailViewSource source, AppUiState? state)
    {
        if (_source is not null) throw new InvalidOperationException("The mail page is already bound.");
        _source = source ?? throw new ArgumentNullException(nameof(source)); _state = state;
        ConfigureFilterTabPersistence(state, "MailFilter");
        _state?.Restore(messagesView, "MailTiles_v2"); reloadButton.Enabled = true; searchTimer.Start();
    }

    internal void ConfigureFilterTabPersistence(AppUiState? state, string name)
    {
        if (state is null) return;
        if (state.ReadTab(name) == unreadMessagesTab.Name) filterTabs.SelectedItem = unreadMessagesTab;
        filterTabs.SelectedItemChanged += (_, _) =>
        {
            if (Visible && !Disposing) state.SaveTab(name, filterTabs.SelectedItem?.Name);
        };
    }

    internal Task ReloadAsync()
    {
        if (_source is null || _stopping) return Task.CompletedTask;
        var token = Renew(ref _catalogCancellation);
        Cancel(ref _listCancellation); Cancel(ref _previewCancellation); searchTimer.Stop();
        return Track(LoadCatalogAsync(token));
    }
    internal void ConfigureMailActions(WawiCallPro.Office.MailApplication application, bool allowImport)
    { _application = application; _allowImport = allowImport; }
    internal async Task CancelPendingLoadsAsync()
    {
        if (_disposed) return;
        searchTimer.Stop();
        _catalogCancellation?.Cancel(); _listCancellation?.Cancel(); _previewCancellation?.Cancel();
        await Task.WhenAll(_pending.ToArray());
        if (!_stopping && !_disposed)
        {
            reloadButton.Enabled = foldersAccordion.Enabled = messagesGrid.Enabled = true;
            statusLabel.Text = "Mailabruf abgebrochen."; searchTimer.Start();
        }
    }
    private async Task LoadCatalogAsync(CancellationToken token)
    {
        reloadButton.Enabled = false; foldersAccordion.Enabled = messagesGrid.Enabled = false; statusLabel.Text = "Postfachordner werden geladen …";
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
                BindFolders(); _folder = selected?.Key;
                if (_folder is not null) foldersAccordion.SelectedElement = _folderElements[_folder];
            }
            finally { _binding = false; }
            if (_folder is null)
            {
                messagesGrid.DataSource = Array.Empty<MessageRow>(); folderLabel.Text = "Postfach";
                _emptyListText = "Noch keine Nachrichten";
                foldersEmpty.Text = "Kein Postfach verbunden.";
                statusLabel.Text = catalog.Status; ClearPreview("Kein Postfach verbunden."); return;
            }
            _catalogStatus = catalog.Status;
            await LoadCurrentFolderAsync();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception exception)
        {
            if (!token.IsCancellationRequested)
            {
                _binding = true;
                try { _folders = []; _folder = null; BindFolders(); foldersEmpty.Text = "Postfächer konnten nicht geladen werden."; }
                finally { _binding = false; }
                ClearMailList(); statusLabel.Text = PageOperation.Describe(exception);
            }
        }
        finally
        {
            if (!_stopping && !token.IsCancellationRequested)
            { reloadButton.Enabled = foldersAccordion.Enabled = messagesGrid.Enabled = true; searchTimer.Start(); }
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
                _emptyListText = search.Length == 0 ? "Keine Nachrichten in diesem Ordner" : "Keine Nachrichten gefunden";
                if (previous is not null && messagesView.LocateByValue("Key", previous) is var handle && handle >= 0) messagesView.FocusedRowHandle = handle;
            }
            finally { _binding = false; }
            statusLabel.Text = (items.IsEmpty ? "Keine Nachrichten für diese Auswahl." : $"{items.Length} Nachrichten · Strg+F zum Suchen")
                + (_catalogStatus.Length == 0 ? "" : " · " + _catalogStatus);
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
            _message = message;
            attachmentEdit.Properties.DataSource = message.Attachments.Where(item => item.Id is not null).ToArray();
            attachmentEdit.EditValue = message.Attachments.FirstOrDefault(item => item.Id is not null)?.Id;
            attachmentActions.Visible = _application is not null && message.Attachments.Any(item => item.Id is not null);
            subjectLabel.Visible = senderLabel.Visible = recipientsLabel.Visible = dateLabel.Visible = body.Visible = true;
            readerEmpty.Visible = false;
            if (string.IsNullOrWhiteSpace(message.Html)) body.Text = message.Text;
            else body.HtmlText = message.Html;
            Appearance_Changed(this, EventArgs.Empty);
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
        _emptyListText = "Nachrichten konnten nicht geladen werden.";
    }
    private void ClearPreview(string text)
    {
        _message = null; attachmentActions.Visible = false; attachmentEdit.Properties.DataSource = null;
        subjectLabel.Text = text; senderLabel.Text = recipientsLabel.Text = dateLabel.Text = attachmentsLabel.Text = "";
        attachmentsLabel.Visible = false; body.Text = ""; ApplyReaderAppearance();
        subjectLabel.Visible = senderLabel.Visible = recipientsLabel.Visible = dateLabel.Visible = body.Visible = false;
        readerEmpty.Text = text; readerEmpty.Visible = true;
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
        _stopping = true; reloadButton.Enabled = false; foldersAccordion.Enabled = messagesGrid.Enabled = false; searchTimer.Stop();
        _lifetime.Cancel(); await Task.WhenAll(_pending.ToArray());
    }
    private void DisposePage()
    {
        if (_disposed) return;
        _disposed = true;
        _stopping = true; _lifetime.Cancel(); Cancel(ref _catalogCancellation); Cancel(ref _listCancellation); Cancel(ref _previewCancellation);
        _state?.Save(messagesView, "MailTiles_v2"); _lifetime.Dispose();
    }
    private async void MailPage_Load(object? sender, EventArgs e) => await ReloadAsync();
    private async void Reload_Click(object? sender, EventArgs e) => await Track(ImportAndReloadAsync());
    private async Task ImportAndReloadAsync()
    {
        if (_stopping || !reloadButton.Enabled) return;
        reloadButton.Enabled = false;
        try
        {
            if (_allowImport && _application is not null)
            {
                statusLabel.Text = "Nachrichten werden abgerufen …";
                foreach (var account in (await _application.Accounts.GetAsync(_lifetime.Token)).Where(item => item.Enabled && !item.FolderIds.IsEmpty))
                    await _application.Synchronization.SynchronizeAsync(account.Id, cancellationToken: _lifetime.Token);
            }
            await ReloadAsync();
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception exception) { if (!_stopping) statusLabel.Text = PageOperation.Describe(exception); }
        finally { if (!_stopping) reloadButton.Enabled = true; }
    }
    private async void SaveAttachment_Click(object? sender, EventArgs e) => await Track(SaveAttachmentAsync());
    private async Task SaveAttachmentAsync()
    {
        if (_application is null || _message is not { } message || attachmentEdit.EditValue is not string id || _stopping) return;
        var attachment = message.Attachments.Single(item => item.Id == id); var owner = (Control?)FindForm() ?? this;
        using var waiting = new UiWaitCursor(owner);
        saveAttachmentButton.Enabled = false;
        string? temporary = null;
        try
        {
            using var dialog = new XtraSaveFileDialog { Title = "E-Mail-Anhang speichern", FileName = Path.GetFileName(attachment.Name), OverwritePrompt = true, RestoreDirectory = true, Filter = "Alle Dateien (*.*)|*.*" };
            if (dialog.ShowDialog(owner) != DialogResult.OK) return;
            var bytes = await _application.DownloadForSourceAsync(_source!, message.Key, id, _lifetime.Token);
            var destination = Path.GetFullPath(dialog.FileName);
            temporary = Path.Combine(Path.GetDirectoryName(destination)!, ".wawi-mail-" + Guid.NewGuid().ToString("N") + ".tmp");
            await using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, true))
            { await file.WriteAsync(bytes, _lifetime.Token); await file.FlushAsync(_lifetime.Token); file.Flush(true); }
            _lifetime.Token.ThrowIfCancellationRequested(); File.Move(temporary, destination, true); temporary = null;
            statusLabel.Text = "Anhang gespeichert. Die Datei wurde nicht geöffnet.";
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception exception) { if (!_stopping) statusLabel.Text = PageOperation.Describe(exception); }
        finally
        {
            if (temporary is not null) { try { File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
            if (!_stopping) saveAttachmentButton.Enabled = true;
        }
    }
    private async void Folder_SelectedElementChanged(object sender, SelectedElementChangedEventArgs e)
    {
        if (_binding || _stopping || e.Element?.Tag is not MailFolderKey key) return;
        _folder = key; await LoadCurrentFolderAsync();
    }
    private async void Folder_ElementClick(object sender, ElementClickEventArgs e)
    {
        if (_binding || _stopping || e.Element.Style != ElementStyle.Group || e.Element.Tag is not MailFolderKey key) return;
        _folder = key; await LoadCurrentFolderAsync();
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
    private void Message_ItemCustomize(object sender, DevExpress.XtraGrid.Views.Tile.TileViewItemCustomizeEventArgs e)
    {
        var unread = messagesView.GetRow(e.RowHandle) is MessageRow { IsRead: false };
        e.Item.Elements[0].Appearance.Normal.FontStyleDelta = unread ? FontStyle.Bold : FontStyle.Regular;
        e.Item.Elements[2].Appearance.Normal.FontStyleDelta = unread ? FontStyle.Bold : FontStyle.Regular;
        e.Item.Elements[2].Appearance.Normal.ForeColor = unread ? DevExpress.LookAndFeel.DXSkinColors.ForeColors.Question : ForeColor;
        e.Item.Elements[2].Appearance.Normal.Options.UseForeColor = true;
    }
    private void Message_DrawEmpty(object sender, DevExpress.XtraGrid.Views.Base.CustomDrawEventArgs e)
    {
        TextRenderer.DrawText(e.Cache.Graphics, _emptyListText, Font, e.Bounds, ForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
        e.Handled = true;
    }
    private void Message_CustomRowFilter(object sender, DevExpress.XtraGrid.Views.Base.RowFilterEventArgs e)
    {
        // The provider searches complete messages. TileView must not filter those backend hits by their list snippet again.
        e.Visible = !ReferenceEquals(filterTabs.SelectedItem, unreadMessagesTab)
            || messagesGrid.DataSource is MessageRow[] rows && e.ListSourceRow >= 0 && e.ListSourceRow < rows.Length && !rows[e.ListSourceRow].IsRead;
        e.Handled = true;
    }
    private async void MailFilter_SelectedItemChanged(object sender, NavigationBarItemEventArgs e)
    {
        if (_binding || _stopping || _source is null) return;
        _binding = true;
        try { messagesView.ActiveFilterString = ReferenceEquals(filterTabs.SelectedItem, unreadMessagesTab) ? "[IsRead] = False" : ""; }
        finally { _binding = false; }
        await LoadFocusedMessageAsync();
    }
    private void BindFolders()
    {
        foldersAccordion.Elements.Clear(); _folderElements.Clear();
        foreach (var folder in _folders)
        {
            var group = _folders.Any(candidate => candidate.Parent == folder.Key);
            var element = new AccordionControlElement(group ? ElementStyle.Group : ElementStyle.Item)
            { Text = folder.UnreadCount is > 0 ? $"{folder.Name} ({folder.UnreadCount})" : folder.Name, Tag = folder.Key, Expanded = true };
            element.ImageOptions.SvgImage = FolderIcon.Value;
            element.ImageOptions.SvgImageSize = new Size(16, 16);
            _folderElements.Add(folder.Key, element);
        }
        foreach (var folder in _folders)
        {
            if (folder.Parent is null) foldersAccordion.Elements.Add(_folderElements[folder.Key]);
            else _folderElements[folder.Parent].Elements.Add(_folderElements[folder.Key]);
        }
        foldersAccordion.Visible = !_folders.IsEmpty; foldersEmpty.Visible = _folders.IsEmpty;
        Appearance_Changed(this, EventArgs.Empty);
    }
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
            UiButtonSizing.Apply(saveAttachmentButton);
            var unit = Math.Max(Font.Height, LogicalToDeviceUnits(16));
            filterTabs.AppearanceItem.Normal.Font = Font;
            filterTabs.AppearanceItem.Hovered.Font = Font;
            filterTabs.AppearanceItem.Pressed.Font = Font;
            filterTabs.AppearanceItem.Normal.Options.UseFont = true;
            filterTabs.AppearanceItem.Hovered.Options.UseFont = true;
            filterTabs.AppearanceItem.Pressed.Options.UseFont = true;
            filterTabs.Font = Font;
            filterTabs.MinimumSize = new Size(0, TextRenderer.MeasureText("Ungelesen", Font).Height + LogicalToDeviceUnits(16));
            var folderWidth = _folderElements.Values.Select(element => TextRenderer.MeasureText(element.Text, Font).Width).DefaultIfEmpty(unit * 8).Max() + unit * 3;
            foldersSplit.SplitterPosition = Math.Min(Math.Max(unit * 11, folderWidth), Math.Max(unit * 6, Width * 35 / 100));
            var readerHeight = RequiredReaderHeight(contentSplit.Width);
            var splitter = contentSplit.Horizontal ? contentSplit.Panel2.Left - contentSplit.Panel1.Right : contentSplit.Panel2.Top - contentSplit.Panel1.Bottom;
            var availableHeight = Math.Max(0, contentSplit.ClientSize.Height - Math.Max(0, splitter));
            var listHeight = filterTabs.GetPreferredSize(new(contentSplit.Width, 0)).Height + folderLabel.GetPreferredSize(new(contentSplit.Width, 0)).Height + unit * 3;
            var horizontal = !_preferBottom && (Width >= unit * 62 || availableHeight < readerHeight + listHeight);
            contentSplit.Horizontal = horizontal;
            contentSplit.SplitterPosition = horizontal ? Math.Min(unit * 23, Math.Max(unit * 8, contentSplit.Width * 42 / 100))
                : Math.Min(Math.Max(unit * 4, availableHeight * 40 / 100), Math.Max(unit * 3, availableHeight - readerHeight));
            messagesView.OptionsTiles.ItemPadding = new Padding(LogicalToDeviceUnits(12), LogicalToDeviceUnits(8), LogicalToDeviceUnits(12), LogicalToDeviceUnits(8));
            messagesView.OptionsTiles.ItemSize = new Size(Math.Max(unit * 12, messagesGrid.Width), Font.Height * 4 + messagesView.OptionsTiles.ItemPadding.Vertical);
            messagesView.TileColumns[1].Length.Value = TextRenderer.MeasureText("00.00.", Font).Width + LogicalToDeviceUnits(6);
            readingPositionButton.Text = _preferBottom ? "Lesebereich rechts" : "Lesebereich unten";
            UiButtonSizing.Apply(reloadButton, readingPositionButton);
            ApplyReaderAppearance();
        }
        finally { _arranging = false; }
    }
    private int RequiredReaderHeight(int width)
    {
        var available = Math.Max(1, width - readerLayout.Padding.Horizontal);
        var height = readerLayout.Padding.Vertical + Math.Max(Font.Height, LogicalToDeviceUnits(16)) * 3;
        if (_message is null) return height;
        foreach (var label in new[] { subjectLabel, senderLabel, recipientsLabel, dateLabel, attachmentsLabel })
            if (!string.IsNullOrEmpty(label.Text)) height += label.GetPreferredSize(new(Math.Max(1, available - label.Margin.Horizontal), 0)).Height + label.Margin.Vertical;
        if (_application is not null && _message.Attachments.Any(attachment => attachment.Id is not null))
            height += attachmentActions.GetPreferredSize(new(available, 0)).Height + attachmentActions.Margin.Vertical;
        return height;
    }
    private void ApplyReaderAppearance()
    {
        body.Appearance.Text.ForeColor = ForeColor;
        body.Document.SetPageBackground(BackColor);
        body.Document.DefaultCharacterProperties.FontName = Font.Name;
        body.Document.DefaultCharacterProperties.FontSize = Font.Size;
    }

    private sealed class MessageRow(MailListItem item)
    {
        public MailMessageKey Key => item.Key;
        public string Sender => item.Sender;
        public string Subject => string.IsNullOrWhiteSpace(item.Subject) ? "(Kein Betreff)" : item.Subject;
        public string DateText => item.Date.LocalDateTime.Date == DateTime.Today ? item.Date.LocalDateTime.ToString("HH:mm") : item.Date.LocalDateTime.ToString("dd.MM.");
        public string PreviewText => item.Preview + (item.AttachmentCount > 0 ? $"\r\n{item.AttachmentCount} " + (item.AttachmentCount == 1 ? "Anhang" : "Anhänge") : "");
        public string Preview => item.Preview;
        public DateTime Date => item.Date.LocalDateTime;
        public string Attachments => item.AttachmentCount > 0 ? item.AttachmentCount.ToString() : "";
        public bool IsRead => item.IsRead;
    }
}

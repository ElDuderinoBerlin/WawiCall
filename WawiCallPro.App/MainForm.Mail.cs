using DevExpress.XtraBars;
using WawiCallPro.App.Hosting;
using WawiCallPro.App.Views;
using WawiCallPro.Office;

namespace WawiCallPro.App;

public partial class MainForm
{
    private readonly CancellationTokenSource _mailStopping = new();
    private CancellationTokenSource? _mailCancellation;
    private Task _mailOperation = Task.CompletedTask;
    private bool _mailDisposed;
    private async void MailState_Load(object? sender, EventArgs e) => await UpdateMailActionsAsync();
    private async void RibbonMail_SelectedPageChanged(object? sender, EventArgs e) => await UpdateMailActionsAsync();
    private async void MailSettingsClosed(object? sender, FormClosedEventArgs e) => await UpdateMailActionsAsync();
    private async Task UpdateMailActionsAsync()
    {
        if (IsDisposed || _closing || _mailDisposed) return;
        try
        {
            var available = _workspace?.Mail is not null;
            var enabled = available && await _workspace!.Mail.Cache.IsEnabledAsync(_mailStopping.Token);
            if (IsDisposed || _closing || _mailDisposed) return;
            var running = !_mailOperation.IsCompleted;
            mailRefreshItem.Enabled = mailSettingsItem.Enabled = available && !running;
            mailCacheRebuildItem.Enabled = enabled && !running;
            mailCacheCancelItem.Enabled = running;
        }
        catch (OperationCanceledException) when (_mailStopping.IsCancellationRequested) { }
        catch (Exception) { if (!IsDisposed) mailCacheRebuildItem.Enabled = false; }
    }
    private async void MailRefresh_Click(object sender, ItemClickEventArgs e) => await RunMailOperationAsync(async token =>
    {
        foreach (var account in (await _workspace!.Mail.Accounts.GetAsync(token)).Where(item => item.Enabled && !item.FolderIds.IsEmpty))
            await _workspace.Mail.Synchronization.SynchronizeAsync(account.Id, cancellationToken: token);
    }, "Nachrichten wurden abgerufen.");
    private async void MailCacheRebuild_Click(object sender, ItemClickEventArgs e) => await RebuildMailCacheAsync();
    internal Task RebuildMailCacheAsync() => RunMailOperationAsync(token =>
    {
        var progress = new Progress<MailSyncProgress>(value =>
        {
            if (!_closing && !IsDisposed && _mailCancellation?.Token == token) connectionLabel.Text = "Mailcache: " + value.FoldersCompleted + "/" + value.FoldersTotal + " Ordner · " + value.MessagesConfirmed + " Nachrichten bestätigt";
        });
        return _workspace!.Mail.Cache.RebuildAsync(progress, token);
    }, "Mailcache einschließlich Anhängen neu erstellt.");
    internal Task RunMailOperationAsync(Func<CancellationToken, Task> action, string completed)
    {
        if (_workspace?.Mail is null || _closing || !_mailOperation.IsCompleted) return _mailOperation;
        _mailOperation = RunMailCoreAsync(action, completed); return _mailOperation;
    }
    private async Task RunMailCoreAsync(Func<CancellationToken, Task> action, string completed)
    {
        MailPage? loadingPage = null;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_mailStopping.Token); _mailCancellation = cancellation;
        mailRefreshItem.Enabled = mailSettingsItem.Enabled = mailCacheRebuildItem.Enabled = false; mailCacheCancelItem.Enabled = true;
        connectionLabel.Text = "Mailvorgang läuft …";
        try
        {
            await Task.Yield(); await action(cancellation.Token); cancellation.Token.ThrowIfCancellationRequested();
            if (!_closing)
            {
                Navigate("mail");
                if (GetActiveDocumentView() is MailPage page)
                {
                    loadingPage = page;
                    await page.ReloadAsync().WaitAsync(cancellation.Token);
                }
                connectionLabel.Text = completed;
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            if (loadingPage is not null) await loadingPage.CancelPendingLoadsAsync();
            if (!_closing) connectionLabel.Text = "Mailvorgang abgebrochen. Ein nicht abgeschlossener Neuaufbau ersetzt den bisherigen Cache nicht.";
        }
        catch (Exception exception) { if (!_closing) connectionLabel.Text = PageOperation.Describe(exception); }
        finally
        {
            _mailCancellation = null;
            if (!IsDisposed && !_closing)
            {
                mailRefreshItem.Enabled = mailSettingsItem.Enabled = _workspace?.Mail is not null; mailCacheCancelItem.Enabled = false;
                try { mailCacheRebuildItem.Enabled = await _workspace!.Mail.Cache.IsEnabledAsync(_mailStopping.Token); }
                catch (Exception) { mailCacheRebuildItem.Enabled = false; }
            }
        }
    }
    private void MailCacheCancel_Click(object sender, ItemClickEventArgs e) => _mailCancellation?.Cancel();
    private async Task StopMailOperationAsync() { await _mailStopping.CancelAsync(); await _mailOperation; }
    private void DisposeMailOperation()
    {
        if (_mailDisposed) return;
        _mailDisposed = true; _mailStopping.Cancel(); _mailStopping.Dispose();
    }
    private async void MailSettings_Click(object sender, ItemClickEventArgs e)
    {
        using var waiting = new UiWaitCursor(this);
        using var settings = CreateSettingsDialog(); settings.SelectMailTab();
        settings.ShowDialog(this);
        if (settings.RestartRequested) { RestartRequested = true; Close(); return; }
        await UpdateMailActionsAsync();
    }
}

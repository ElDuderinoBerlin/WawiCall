using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WawiCallPro.Office;

namespace WawiCallPro.App.Hosting;

/// <summary>Optional one-shot background import. It never opens an OAuth browser or changes mailbox state.</summary>
internal sealed class MailStartupImport(MailApplication mail, ILogger<MailStartupImport> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            foreach (var account in (await mail.Accounts.GetAsync(stoppingToken)).Where(item => item.Enabled && item.SynchronizeOnStartup && !item.FolderIds.IsEmpty))
            {
                try { await mail.Synchronization.SynchronizeAsync(account.Id, cancellationToken: stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                catch (Exception exception) { logger.LogWarning("E-Mail-Startimport nicht abgeschlossen: {Failure}", PageOperation.Describe(exception)); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception) { logger.LogWarning("Die E-Mail-Einstellungen konnten für den Startimport nicht geladen werden."); }
    }
}

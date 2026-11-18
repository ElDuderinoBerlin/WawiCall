using System.Collections.Immutable;
using System.Text.Json;
using WawiCallPro.Interfaces.Office;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Security;

namespace WawiCallPro.Office;

public interface IMailConnectorFactory { IMailConnector Create(MailAccountSettings account); }
public sealed class MailConnectorFactory(MailHttp http) : IMailConnectorFactory
{
    public IMailConnector Create(MailAccountSettings account)
    {
        IMailConnector connector = account.Provider switch
        {
            MailProviderKind.Microsoft365 => new GraphMailConnector(account, http),
            MailProviderKind.Gmail => new GmailConnector(account, http),
            MailProviderKind.OutlookDesktop when OperatingSystem.IsWindows() => new OutlookDesktopConnector(account),
            _ => throw new MailException(MailFailure.NotSupported)
        };
        return new GuardedConnector(connector);
    }
    private sealed class GuardedConnector(IMailConnector inner) : IMailConnector
    {
        public Guid AccountId => inner.AccountId; public string Provider => inner.Provider;
        public Task<MailFolderCatalog> GetFoldersAsync(CancellationToken token = default) => Run(() => inner.GetFoldersAsync(token), token);
        public Task<ImmutableArray<MailListItem>> GetMessagesAsync(string folder, string search, CancellationToken token = default) => Run(() => inner.GetMessagesAsync(folder, search, token), token);
        public Task<MailEnvelope?> GetMessageAsync(string message, CancellationToken token = default) => Run(() => inner.GetMessageAsync(message, token), token);
        public Task<MailChangePage> GetChangesAsync(string folder, string? checkpoint, string? page, CancellationToken token = default) => Run(() => inner.GetChangesAsync(folder, checkpoint, page, token), token);
        public Task<byte[]> GetAttachmentAsync(string message, string attachment, CancellationToken token = default) => Run(() => inner.GetAttachmentAsync(message, attachment, token), token);
        private static async Task<T> Run<T>(Func<Task<T>> read, CancellationToken token)
        { try { return await read().ConfigureAwait(false); } catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; } catch (MailException) { throw; } catch (Exception) { throw new MailException(MailFailure.ProviderUnavailable); } }
    }
}

public sealed class MailAccounts(
    ConfigurationStore settings,
    ApplicationOptionsValidator validator,
    ISecretProtector secrets,
    IMailAuthentication authentication,
    IMailConnectorFactory connectors,
    LxPrintStudioMailImport lxImport,
    MailHttp http)
{
    public async Task<ImmutableArray<MailAccountSettings>> GetAsync(CancellationToken token = default) => (await settings.LoadAsync(token)).Options.MailAccounts;
    public async Task<MailAccountSettings> GetAsync(Guid id, CancellationToken token = default) => (await GetAsync(token)).SingleOrDefault(item => item.Id == id) ?? throw new MailException(MailFailure.Configuration);
    public async Task<MailAccountSettings> ConnectAsync(Guid id, CancellationToken token = default)
    {
        var snapshot = await settings.LoadAsync(token); var account = snapshot.Options.MailAccounts.SingleOrDefault(item => item.Id == id) ?? throw new MailException(MailFailure.Configuration);
        var connected = account with { Enabled = true };
        validator.Validate(snapshot.Options with { MailAccounts = snapshot.Options.MailAccounts.Select(item => item.Id == id ? connected : item).ToImmutableArray() });
        if (account.Provider != MailProviderKind.OutlookDesktop)
        {
            var access = await authentication.ConnectAsync(account, token);
            connected = connected with { RemoteIdentity = access.Identity };
            if (account.Provider == MailProviderKind.Gmail)
                connected = connected with { RemoteIdentity = await new GmailConnector(connected, GetHttp()).GetMailboxAsync(token) };
            if (account.RemoteIdentity is not null && !string.Equals(account.RemoteIdentity, connected.RemoteIdentity, StringComparison.OrdinalIgnoreCase))
                throw new MailException(MailFailure.Conflict);
        }
        await connectors.Create(connected).GetFoldersAsync(token); // Actual read permission, not just a successful token acquisition.
        await settings.SaveAsync(snapshot.Options with { MailAccounts = snapshot.Options.MailAccounts.Select(item => item.Id == id ? connected : item).ToImmutableArray() }, snapshot, token);
        return connected;
    }
    private MailHttp GetHttp() => http;
    public async Task DisconnectAsync(Guid id, CancellationToken token = default)
    {
        var snapshot = await settings.LoadAsync(token); var account = snapshot.Options.MailAccounts.SingleOrDefault(item => item.Id == id) ?? throw new MailException(MailFailure.Configuration);
        await authentication.DisconnectAsync(account, token);
        await settings.SaveAsync(snapshot.Options with { MailAccounts = snapshot.Options.MailAccounts.Select(item => item.Id == id ? item with { Enabled = false } : item).ToImmutableArray() }, snapshot, token);
    }
    public Task<MailFolderCatalog> GetFoldersAsync(MailAccountSettings account, CancellationToken token = default) => connectors.Create(account).GetFoldersAsync(token);
    public Task ForgetAuthenticationAsync(MailAccountSettings account, CancellationToken token = default) => authentication.DisconnectAsync(account, token);
    public Task<ImmutableArray<MailStore>> GetOutlookStoresAsync(MailAccountSettings account, CancellationToken token = default) =>
        OperatingSystem.IsWindows() ? new OutlookDesktopConnector(account).GetStoresAsync(token) : throw new MailException(MailFailure.NotSupported);
    public async Task<MailAccountSettings> ReadGoogleClientAsync(string path, CancellationToken token = default)
    {
        try
        {
            await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
            if (input.Length is < 1 or > 65536) throw new MailException(MailFailure.Configuration);
            using var document = await JsonDocument.ParseAsync(input, new JsonDocumentOptions { MaxDepth = 8, AllowDuplicateProperties = false }, token);
            var installed = document.RootElement.GetProperty("installed");
            return new() { Id = Guid.NewGuid(), Provider = MailProviderKind.Gmail, DisplayName = "Gmail", ClientId = installed.Required("client_id"),
                GoogleClientSecret = secrets.Protect(installed.Required("client_secret"), MailAccountSettings.GoogleSecretPurpose) };
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception) { throw new MailException(MailFailure.Configuration); }
    }
    public Task<IReadOnlyList<MailAccountSettings>> ReadLxPrintStudioAsync(string path, CancellationToken token = default) => lxImport.ReadAsync(path, token);
}

/// <summary>Live configured providers behind the existing browser contract; no demonstration or implicit login.</summary>
public sealed class ConfiguredMailViewSource(MailAccounts accounts, IMailConnectorFactory connectors) : IMailViewSource
{
    public async Task<MailFolderCatalog> GetFoldersAsync(CancellationToken cancellationToken = default)
    {
        var configured = (await accounts.GetAsync(cancellationToken)).Where(account => account.Enabled).ToArray();
        var folders = new List<MailFolder>(); var failures = 0;
        foreach (var account in configured)
        {
            try
            {
                var connector = connectors.Create(account); var catalog = await connector.GetFoldersAsync(cancellationToken);
                var root = new MailFolderKey(connector.Provider, account.Id.ToString("N"), "account:" + account.Id.ToString("N"));
                folders.Add(new(root, null, account.DisplayName));
                folders.AddRange(catalog.Folders.Select(folder => folder.Parent is null ? folder with { Parent = root } : folder));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (MailException) { failures++; }
        }
        if (failures > 0 && folders.Count == 0) throw new MailException(MailFailure.SignInRequired);
        return new([.. folders], configured.Length == 0 ? "Kein Postfach eingerichtet. Unter Einstellungen → Office / Mail ein Postfach hinzufügen." :
            failures > 0 ? "Einige Postfächer konnten nicht geladen werden. Bitte die E-Mail-Einstellungen prüfen." : configured.Length + " Postfächer verbunden.");
    }
    public async Task<ImmutableArray<MailListItem>> GetMessagesAsync(MailFolderKey folder, string search, CancellationToken cancellationToken = default)
    {
        var connector = await Resolve(folder.Provider, folder.Account, cancellationToken);
        return folder.Id.StartsWith("account:", StringComparison.Ordinal) ? [] : await connector.GetMessagesAsync(folder.Id, search, cancellationToken);
    }
    public async Task<MailContent?> GetMessageAsync(MailMessageKey message, CancellationToken cancellationToken = default) =>
        (await (await Resolve(message.Provider, message.Account, cancellationToken)).GetMessageAsync(message.Id, cancellationToken))?.Content;
    private async Task<IMailConnector> Resolve(string provider, string identity, CancellationToken token)
    {
        if (!Guid.TryParseExact(identity, "N", out var id)) throw new MailException(MailFailure.Configuration);
        var account = await accounts.GetAsync(id, token); var connector = connectors.Create(account);
        if (!account.Enabled || connector.Provider != provider) throw new MailException(MailFailure.AccessDenied);
        return connector;
    }
}

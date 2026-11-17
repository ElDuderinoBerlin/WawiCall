using System.Text.Json;
using System.Text.Json.Serialization;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Util.Store;
using Microsoft.Identity.Client;
using WawiCallPro.Interfaces.Office;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Security;

namespace WawiCallPro.Office;

public sealed record MailAccessToken([property: JsonIgnore] string Value, string Identity)
{ public override string ToString() => "[mail-access-token]"; }
public interface IMailAuthentication
{
    Task<MailAccessToken> GetAsync(MailAccountSettings account, bool forceRefresh, CancellationToken cancellationToken);
    Task<MailAccessToken> ConnectAsync(MailAccountSettings account, CancellationToken cancellationToken);
    Task DisconnectAsync(MailAccountSettings account, CancellationToken cancellationToken);
}

/// <summary>Interactive auth only on Connect. Ordinary reads refresh silently or require an explicit sign-in.</summary>
public sealed class MailAuthentication(MailTokenStore store, ISecretProtector secrets) : IMailAuthentication
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, IConfidentialClientApplication> _applications = new();
    public const string GmailReadScope = "https://www.googleapis.com/auth/gmail.readonly";
    private static readonly string[] GraphScopes = ["https://graph.microsoft.com/Mail.Read", "https://graph.microsoft.com/User.Read"];
    public Task<MailAccessToken> GetAsync(MailAccountSettings account, bool forceRefresh, CancellationToken cancellationToken) => AcquireAsync(account, false, forceRefresh, cancellationToken);
    public Task<MailAccessToken> ConnectAsync(MailAccountSettings account, CancellationToken cancellationToken) => AcquireAsync(account, true, false, cancellationToken);
    public async Task DisconnectAsync(MailAccountSettings account, CancellationToken cancellationToken)
    { var key = MailTokenStore.Key(account); await using var lease = await store.AcquireAsync(key, cancellationToken); store.Delete(key); foreach (var cached in _applications.Keys.Where(item => item.StartsWith(key + "-", StringComparison.Ordinal))) _applications.TryRemove(cached, out _); }
    private async Task<MailAccessToken> AcquireAsync(MailAccountSettings account, bool interactive, bool refresh, CancellationToken token)
    {
        try
        {
            var key = MailTokenStore.Key(account); await using var lease = await store.AcquireAsync(key, token);
            return account.Provider switch
            {
                MailProviderKind.Microsoft365 => await MicrosoftAsync(account, key, interactive, refresh, token),
                MailProviderKind.Gmail => await GoogleAsync(account, key, interactive, refresh, token),
                _ => throw new MailException(MailFailure.NotSupported)
            };
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (MailException) { throw; }
        catch (Exception) { throw new MailException(MailFailure.SignInRequired); }
    }
    private async Task<MailAccessToken> MicrosoftAsync(MailAccountSettings account, string key, bool interactive, bool refresh, CancellationToken token)
    {
        if (!Guid.TryParse(account.ClientId, out var client) || client == Guid.Empty ||
            account.Tenant is not ("common" or "organizations" or "consumers") && !Guid.TryParse(account.Tenant, out _)) throw new MailException(MailFailure.Configuration);
        if (account.ApplicationAuthentication)
        {
            if (!Guid.TryParse(account.Tenant, out _) || account.MicrosoftClientSecret is null || string.IsNullOrWhiteSpace(account.Mailbox)) throw new MailException(MailFailure.Configuration);
            using var secret = secrets.Unprotect(account.MicrosoftClientSecret, MailAccountSettings.MicrosoftSecretPurpose);
            var credentialKey = key + "-" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(account.MicrosoftClientSecret))));
            foreach (var cached in _applications.Keys.Where(item => item.StartsWith(key + "-", StringComparison.Ordinal) && item != credentialKey)) _applications.TryRemove(cached, out _);
            var confidential = _applications.GetOrAdd(credentialKey, _ => ConfidentialClientApplicationBuilder.Create(account.ClientId).WithAuthority(AzureCloudInstance.AzurePublic, account.Tenant).WithClientSecret(secret.Reveal()).Build());
            var applicationToken = await confidential.AcquireTokenForClient(["https://graph.microsoft.com/.default"]).WithForceRefresh(refresh).ExecuteAsync(token);
            return new(applicationToken.AccessToken, account.Mailbox.ToLowerInvariant());
        }
        var app = PublicClientApplicationBuilder.Create(account.ClientId).WithAuthority(AzureCloudInstance.AzurePublic, account.Tenant).WithRedirectUri("http://localhost").Build();
        app.UserTokenCache.SetBeforeAccessAsync(async args => { var saved = await store.ReadAsync(key, token); if (saved is not null) args.TokenCache.DeserializeMsalV3(Convert.FromBase64String(saved)); });
        app.UserTokenCache.SetAfterAccessAsync(async args => { if (args.HasStateChanged) await store.WriteAsync(key, Convert.ToBase64String(args.TokenCache.SerializeMsalV3()), token); });
        AuthenticationResult result;
        if (interactive) result = await app.AcquireTokenInteractive(GraphScopes).WithPrompt(Prompt.SelectAccount).WithUseEmbeddedWebView(false).ExecuteAsync(token);
        else
        {
            var accounts = (await app.GetAccountsAsync()).ToArray();
            var selected = accounts.SingleOrDefault(item => item.HomeAccountId.Identifier == account.RemoteIdentity);
            if (selected is null) throw new MailException(MailFailure.SignInRequired);
            result = await app.AcquireTokenSilent(GraphScopes, selected).WithForceRefresh(refresh).ExecuteAsync(token);
        }
        if (!interactive && result.Account.HomeAccountId.Identifier != account.RemoteIdentity) throw new MailException(MailFailure.AccessDenied);
        return new(result.AccessToken, result.Account.HomeAccountId.Identifier);
    }
    private async Task<MailAccessToken> GoogleAsync(MailAccountSettings account, string key, bool interactive, bool refresh, CancellationToken token)
    {
        if (account.GoogleClientSecret is null || string.IsNullOrWhiteSpace(account.ClientId) || !account.ClientId.EndsWith(".apps.googleusercontent.com", StringComparison.Ordinal)) throw new MailException(MailFailure.Configuration);
        using var secret = secrets.Unprotect(account.GoogleClientSecret, MailAccountSettings.GoogleSecretPurpose);
        var data = new GoogleStore(store, key, token);
        var initializer = new GoogleAuthorizationCodeFlow.Initializer { ClientSecrets = new ClientSecrets { ClientId = account.ClientId, ClientSecret = secret.Reveal() }, Scopes = [GmailReadScope], DataStore = data };
        UserCredential credential;
        if (interactive)
            credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(initializer, new[] { GmailReadScope }, key, true, token, data, new LocalServerCodeReceiver());
        else
        {
            var saved = await data.GetAsync<TokenResponse>(key) ?? throw new MailException(MailFailure.SignInRequired);
            var flow = new PkceGoogleAuthorizationCodeFlow(initializer);
            credential = new UserCredential(flow, key, saved);
            if (refresh && !await credential.RefreshTokenAsync(token)) throw new MailException(MailFailure.SignInRequired);
        }
        var value = await credential.GetAccessTokenForRequestAsync(cancellationToken: token);
        return new(value, key); // Gmail profile binding is checked by the connector before accepting the account.
    }
    private sealed class GoogleStore(MailTokenStore store, string key, CancellationToken token) : IDataStore
    {
        public Task StoreAsync<T>(string ignored, T value) => store.WriteAsync(key, JsonSerializer.Serialize(value), token);
        public async Task<T> GetAsync<T>(string ignored) { var text = await store.ReadAsync(key, token); return text is null ? default! : JsonSerializer.Deserialize<T>(text)!; }
        public Task DeleteAsync<T>(string ignored) { store.Delete(key); return Task.CompletedTask; }
        public Task ClearAsync() { store.Delete(key); return Task.CompletedTask; }
    }
}

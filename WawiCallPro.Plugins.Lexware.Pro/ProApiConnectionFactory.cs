using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Quantarix.Lexware.APIService;

namespace WawiCallPro.Plugins.Lexware.Pro;

public interface IProApiConnection : IDisposable
{
    ILexwareApiServiceClient Client { get; }
    ProFailure? Failure { get; }
    void Reset(bool expectCompanies = false);
}

public interface IProApiConnectionFactory
{
    IProApiConnection Create(ProInstallation installation);
}

/// <summary>Composition boundary for the upstream client and its caller-owned, guarded HTTP transport.</summary>
public sealed class ProApiConnectionFactory(ProConnectionOptions options, IProHttpHandlerFactory handlers) : IProApiConnectionFactory
{
    public IProApiConnection Create(ProInstallation installation)
    {
        if (string.IsNullOrWhiteSpace(options.Username) || options.Password is null
            || !Guid.TryParseExact(options.ClientKey, "B", out var key) || key == Guid.Empty
            || options.TimeoutSeconds is < 1 or > 300 || options.TokenTimeoutSeconds is < 1 or > 300)
            throw new ProAdapterException(ProFailure.ConfigurationRequired);
        var guard = new ProResponseGuard(handlers.Create());
        var http = new HttpClient(guard) { Timeout = Timeout.InfiniteTimeSpan };
        try
        {
            var apiOptions = new LexwareApiOptions
            {
                ApiUrl = installation.ApiUrl.AbsoluteUri.TrimEnd('/'),
                Username = options.Username,
                Password = options.Password,
                ClientKey = options.ClientKey!,
                CompanyId = "", // Never select the library's default company 1 implicitly.
                TimeoutSeconds = options.TimeoutSeconds,
                TokenTimeoutSeconds = options.TokenTimeoutSeconds,
                MaxConcurrentRequests = 1
            };
            // Upstream logs include raw response bodies and login data. Only our fixed failure codes escape.
            var client = new LexwareApiServiceClient(http, Options.Create(apiOptions), NullLogger<LexwareApiServiceClient>.Instance);
            return new Connection(http, guard, client);
        }
        catch { http.Dispose(); throw; }
    }

    private sealed class Connection(HttpClient http, ProResponseGuard guard, ILexwareApiServiceClient client) : IProApiConnection
    {
        public ILexwareApiServiceClient Client => client;
        public ProFailure? Failure => guard.Failure;
        public void Reset(bool expectCompanies = false) => guard.Reset(expectCompanies);
        public void Dispose() => http.Dispose();
    }
}

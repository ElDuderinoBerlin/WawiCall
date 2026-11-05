using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Quantarix.License.Client.Keys;
using Quantarix.License.Client.Security;
using WawiCallPro.Utilities.Operations;

namespace WawiCallPro.Utilities.Licensing;

public sealed record LicenseProductIdentity(string ProductCode, Guid ApplicationGuid, string Version, bool Required)
{
    public const string ProApplicationId = "65c66e82-4607-4c4e-b7db-c9d9abf1ede2";
    public const string FinOfficeApplicationId = "df533b52-a06c-48a5-ad60-9f69fd94a4a9";
    public static LicenseProductIdentity FromAssembly(Assembly assembly)
    {
        var line = ApplicationIdentity.GetProductLine(assembly);
        if (line is not ("Pro" or "FinOffice")) throw new LicenseAccessException(LicenseStatus.ConfigurationMissing);
        var pro = line == "Pro";
        var rawRequired = assembly.GetCustomAttributes<AssemblyMetadataAttribute>().SingleOrDefault(item => item.Key == "WawiLicenseRequired")?.Value;
        var required = !bool.TryParse(rawRequired, out var configuredRequired) || configuredRequired;
        return new(pro ? "WAWICALLPRO_PRO" : "WAWICALLPRO_FINOFFICE", new Guid(pro ? ProApplicationId : FinOfficeApplicationId),
            assembly.GetName().Version?.ToString() ?? "0.0", required);
    }
}

/// <summary>Public deployment settings only. Secrets are never accepted from this file.</summary>
public sealed record StartupSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false
    };
    public Uri? LicenseServerUrl { get; init; }
    public string? ServerThumbprint { get; init; }
    public bool UploadCrashReports { get; init; }
    public int RequestTimeoutSeconds { get; init; } = 10;
    public int LicenseCheckIntervalSeconds { get; init; } = 600;
    public int CrashFlushIntervalSeconds { get; init; } = 30;

    public static StartupSettings Load(ApplicationPaths paths)
    {
        try
        {
            var path = Path.Combine(paths.RootDirectory, "startup.local.json");
            var value = File.Exists(path) ? JsonSerializer.Deserialize<StartupSettings>(File.ReadAllText(path), JsonOptions) : new StartupSettings();
            if (value is null || value.RequestTimeoutSeconds is < 1 or > 120 || value.LicenseCheckIntervalSeconds is < 30 or > 86400
                || value.CrashFlushIntervalSeconds is < 5 or > 3600) throw new LicenseAccessException(LicenseStatus.ConfigurationMissing);
            if (value.LicenseServerUrl is { } url && (!url.IsAbsoluteUri || !string.IsNullOrEmpty(url.UserInfo) || !string.IsNullOrEmpty(url.Query)
                || !string.IsNullOrEmpty(url.Fragment) || url.AbsolutePath != "/" || (url.Scheme != "https" && !(url.IsLoopback && url.Scheme == "http"))))
                throw new LicenseAccessException(LicenseStatus.ConfigurationMissing);
            if (value.UploadCrashReports && value.LicenseServerUrl is null) throw new LicenseAccessException(LicenseStatus.ConfigurationMissing);
            if (value.ServerThumbprint is { } pin && (pin.Length is not (40 or 64) || !pin.All(Uri.IsHexDigit)))
                throw new LicenseAccessException(LicenseStatus.ConfigurationMissing);
            return value;
        }
        catch (LicenseAccessException) { throw; }
        catch { throw new LicenseAccessException(LicenseStatus.ConfigurationMissing); }
    }
}

public sealed class CompiledLicenseKeys : IPublicKeyProvider
{
    private readonly EmbeddedPublicKeyProvider _keys;
    private readonly bool _configured;
    public CompiledLicenseKeys()
    {
        using var resource = typeof(CompiledLicenseKeys).Assembly.GetManifestResourceStream("WawiCallPro.LicensePublicKeys")
            ?? throw new LicenseAccessException(LicenseStatus.ConfigurationMissing);
        var keys = JsonSerializer.Deserialize<Dictionary<string, string>>(resource) ?? throw new LicenseAccessException(LicenseStatus.ConfigurationMissing);
        _configured = keys.Count > 0;
        _keys = new EmbeddedPublicKeyProvider(keys);
    }
    public RSA GetPublicKey(string? keyId) => _configured ? _keys.GetPublicKey(keyId) : throw new LicenseAccessException(LicenseStatus.ConfigurationMissing);
}

public interface IStartupHttpClientFactory { HttpClient Create(); }

public sealed class StartupHttpClientFactory(StartupSettings settings) : IStartupHttpClientFactory
{
    public HttpClient Create()
    {
        if (settings.LicenseServerUrl is null) throw new LicenseAccessException(LicenseStatus.ConfigurationMissing);
        var handler = new HttpClientHandler { AllowAutoRedirect = false };
        if (!string.IsNullOrWhiteSpace(settings.ServerThumbprint))
        {
            var validator = new PinnedServerCertificateValidator(settings.ServerThumbprint);
            handler.ServerCertificateCustomValidationCallback = validator.Validate;
            if (settings.ServerThumbprint.Length == 64)
                handler.ServerCertificateCustomValidationCallback = (_, certificate, _, _) => certificate is not null
                    && string.Equals(certificate.GetCertHashString(HashAlgorithmName.SHA256), settings.ServerThumbprint, StringComparison.OrdinalIgnoreCase);
        }
        return new HttpClient(handler) { BaseAddress = settings.LicenseServerUrl, Timeout = TimeSpan.FromSeconds(settings.RequestTimeoutSeconds) };
    }
}

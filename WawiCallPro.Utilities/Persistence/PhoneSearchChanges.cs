using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.Utilities.Persistence;

/// <summary>Process-local invalidation after durable writes. Never contains a party or a raw phone number.</summary>
public sealed class PhoneSearchChanges
{
    private long _revision;
    private long _pluginEpoch;
    private string? _configuration;
    public long Revision => Interlocked.Read(ref _revision);
    public long PluginEpoch => Interlocked.Read(ref _pluginEpoch);
    internal event Action? Changed;
    public void Invalidate(bool wake = true)
    {
        Interlocked.Increment(ref _revision);
        if (wake) Changed?.Invoke();
    }
    public void PluginChanged() { Interlocked.Increment(ref _pluginEpoch); Invalidate(); }
    internal void ObserveConfiguration(ApplicationOptions options) => Interlocked.CompareExchange(ref _configuration, Identity(options), null);
    internal void ConfigurationSaved(ApplicationOptions options)
    {
        var identity = Identity(options);
        if (Interlocked.Exchange(ref _configuration, identity) != identity) Invalidate();
    }
    private static string Identity(ApplicationOptions options)
    {
        var selected = options.Companies.Where(company => company.IncludeInPhoneCache)
            .OrderBy(company => company.Company.ProductLine).ThenBy(company => company.Company.InstallationId, StringComparer.Ordinal)
            .ThenBy(company => company.Company.ExternalId, StringComparer.Ordinal)
            .Select(company => new { company.Company, company.DefaultPhoneRegion, company.LocalAreaCode });
        var json = JsonSerializer.Serialize(new { Selected = selected, options.Database, options.User.ActivePluginId });
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }
}

namespace WawiCallPro.Utilities.Configuration;

/// <summary>Local cache policy; retention refers to message reception time, independently of provider import settings.</summary>
public sealed record MailCacheSettings
{
    public bool Enabled { get; init; } = true;
    public int Days { get; init; } = 30;
}

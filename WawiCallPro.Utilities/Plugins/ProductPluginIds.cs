namespace WawiCallPro.Utilities.Plugins;

/// <summary>Assembly identities of the product plugins shared by the hosts.</summary>
public static class ProductPluginIds
{
    public const string JtlWawi = "WawiCallPro.Plugins.JtlWawi";
    internal const string LegacyDummy = "WawiCallPro.Plugins.Dummy";

    internal static string? FromSavedSelection(string? id) => id == LegacyDummy ? JtlWawi : id;
}

namespace WawiCallPro.App.Hosting;

/// <summary>Converts logical layout thresholds to device pixels without applying font or DPI scaling twice.</summary>
internal static class DpiLayoutMetrics
{
    internal static int FontThreshold(int logicalPixels, float fontPoints, int dpi) =>
        (int)Math.Ceiling(logicalPixels * (fontPoints / 10d) * dpi / 96d);

    internal static bool MinimizeRibbon(int clientHeight, float fontPoints, int dpi) =>
        clientHeight < FontThreshold(900, 10F, dpi) && fontPoints > 13F;
}

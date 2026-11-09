using DevExpress.Utils.Svg;
using DevExpress.XtraEditors;

namespace WawiCallPro.App.Hosting;

internal static class AppIconHelper
{
    private static readonly Lazy<SvgImage> Svg = new(() =>
    {
        using var stream = typeof(AppIconHelper).Assembly.GetManifestResourceStream("WawiCallPro.App.AppIcon.svg")
            ?? throw new InvalidOperationException("Application SVG is missing.");
        return SvgImage.FromStream(stream);
    });
    internal static SvgImage ApplicationSvg => Svg.Value;
    internal static void Apply(XtraForm form)
    {
        using var stream = typeof(AppIconHelper).Assembly.GetManifestResourceStream("WawiCallPro.App.AppIcon.ico")
            ?? throw new InvalidOperationException("Application icon is missing.");
        using var source = new Icon(stream);
        var icon = (Icon)source.Clone();
        form.IconOptions.Icon = icon;
        form.IconOptions.SvgImage = ApplicationSvg;
        form.Disposed += (_, _) => icon.Dispose();
    }
}

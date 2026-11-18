using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DevExpress.Utils.Svg;

namespace WawiCallPro.App.Hosting;

/// <summary>Neutral, skin-aware action icons preserve the existing glyphs and their white cut-outs.</summary>
internal static class RibbonIconHelper
{
    internal static SvgImage Load(string name)
    {
        using var source = typeof(MainForm).Assembly.GetManifestResourceStream("WawiCallPro.App.Icons." + name + ".svg") ?? throw new InvalidOperationException("Missing ribbon icon.");
        var svg = XDocument.Load(source);
        foreach (var element in svg.Descendants())
        {
            if (element.Attribute("class") is { } classes)
                classes.Value = string.Join(' ', classes.Value.Split(' ').Select(value => value is "Blue" or "Red" or "Yellow" or "Green" or "Orange" or "Purple" or "Gray" ? "Black" : value));
            foreach (var attribute in new[] { "fill", "stroke" })
                if (element.Attribute(attribute) is { } color && color.Value.StartsWith('#') && !IsWhite(color.Value))
                {
                    color.Value = "#727272";
                    if (attribute == "fill")
                    {
                        // Inline-only SVGs also need a palette role for dark skins.
                        element.SetAttributeValue("class", "Black"); color.Remove();
                    }
                }
            if (element.Name.LocalName == "style")
                element.Value = Regex.Replace(element.Value, @"#[0-9a-fA-F]{3,8}\b", match => IsWhite(match.Value) ? match.Value : "#727272");
        }
        svg.Root!.AddFirst(new XElement(svg.Root.Name.Namespace + "style", ".Black{fill:#727272}"));
        using var data = new MemoryStream(Encoding.UTF8.GetBytes(svg.ToString(SaveOptions.DisableFormatting)));
        return SvgImage.FromStream(data);
    }
    private static bool IsWhite(string value) => value.Equals("#fff", StringComparison.OrdinalIgnoreCase) || value.Equals("#ffffff", StringComparison.OrdinalIgnoreCase);
}

using System.Net;
using System.Text.RegularExpressions;

namespace WawiCallPro.App.Hosting;

internal static partial class LogListBoxText
{
    public static string ToPlainText(string htmlText)
    {
        if (string.IsNullOrEmpty(htmlText))
            return string.Empty;

        var withLineBreaks = HtmlLineBreakRegex().Replace(htmlText, Environment.NewLine);
        return WebUtility.HtmlDecode(HtmlTagRegex().Replace(withLineBreaks, string.Empty));
    }

    [GeneratedRegex(@"<br\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex HtmlLineBreakRegex();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex HtmlTagRegex();
}

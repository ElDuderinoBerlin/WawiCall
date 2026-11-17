using System.Net.Mime;
using System.Text;
using System.Text.RegularExpressions;
using WawiCallPro.Interfaces.Office;

namespace WawiCallPro.Office;

internal static class MailMime
{
    static MailMime() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    internal static string Text(byte[] bytes, string contentType)
    {
        try
        {
            var charset = string.IsNullOrWhiteSpace(contentType) ? null : new ContentType(contentType).CharSet;
            var encoding = string.IsNullOrWhiteSpace(charset) ? Encoding.UTF8 : Encoding.GetEncoding(charset.Trim('"'));
            return encoding.GetString(bytes);
        }
        catch (Exception) { throw new MailException(MailFailure.InvalidResponse); }
    }
    internal static string Header(string value)
    {
        value = Regex.Replace(value, @"(?<=\?=)\s+(?==\?)", "", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        return Regex.Replace(value, @"=\?([^?]+)\?([bBqQ])\?([^?]*)\?=", match =>
        {
            try
            {
                byte[] bytes;
                if (match.Groups[2].Value.Equals("B", StringComparison.OrdinalIgnoreCase)) bytes = Convert.FromBase64String(match.Groups[3].Value);
                else
                {
                    var text = match.Groups[3].Value.Replace('_', ' '); using var output = new MemoryStream();
                    for (var index = 0; index < text.Length; index++)
                    { if (text[index] == '=' && index + 2 < text.Length) { output.WriteByte(Convert.ToByte(text.Substring(index + 1, 2), 16)); index += 2; } else output.WriteByte(checked((byte)text[index])); }
                    bytes = output.ToArray();
                }
                return Encoding.GetEncoding(match.Groups[1].Value).GetString(bytes);
            }
            catch (Exception) { throw new MailException(MailFailure.InvalidResponse); }
        }, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    }
}

using WawiCallPro.Utilities.Operations;

namespace WawiCallPro.App.Hosting;

internal sealed class LogListBoxItem(string htmlText)
{
    public string HtmlText { get; } = htmlText;
    public string? FullText { get; init; }
    public FileLogReference? FileRecord { get; init; }
    private string? _fileDetails;

    internal string? GetDetails()
    {
        if (_fileDetails is not null) return _fileDetails;
        var details = FileRecord?.ReadDetails();
        if (details is null) return _fileDetails = FullText;
        return _fileDetails = FullText is null || details.StartsWith(FullText, StringComparison.Ordinal) ? details
            : FullText + Environment.NewLine + details;
    }

    public string DisplayText { get; set; } = htmlText;

    public override string ToString() => DisplayText;
}

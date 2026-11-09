namespace WawiCallPro.App.Hosting;

internal sealed class LogListBoxItem(string htmlText)
{
    public string HtmlText { get; } = htmlText;

    public string DisplayText { get; set; } = htmlText;

    public override string ToString() => DisplayText;
}

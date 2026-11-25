using System.ComponentModel;
using DevExpress.Utils.Html;
using DevExpress.Utils.Svg;
using DevExpress.XtraBars.Alerter;

namespace WawiCallPro.App.Hosting;

/// <summary>Shows transient, skin-aware notifications on the owner form's UI thread.</summary>
public sealed class AppToast : Component
{
    private readonly AlertControl _alerts;

    public AppToast(IContainer? components = null, int durationMilliseconds = 5000,
        AlertFormLocation location = AlertFormLocation.BottomRight)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(durationMilliseconds);
        _alerts = new AlertControl
        {
            AllowHtmlText = false,
            AutoFormDelay = durationMilliseconds,
            FormLocation = location,
            ShowCloseButton = true,
            ShowPinButton = false
        };
        components?.Add(this);
    }

    /// <summary>Shows a plain title and message with an optional icon.</summary>
    public void Show(Form owner, string title, string message, SvgImage? icon = null)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(message);
        _alerts.HtmlTemplate.Set(string.Empty, string.Empty);
        if (icon is null)
            _alerts.Show(owner, title, message);
        else
            _alerts.Show(owner, title, message, icon);
    }

    /// <summary>Shows an application-owned HTML/CSS template with dynamic values supplied through data binding.</summary>
    public void Show(Form owner, HtmlTemplate template, object data)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentException.ThrowIfNullOrWhiteSpace(template.Template);
        _alerts.HtmlTemplate.Assign(template);
        _alerts.Show(data, owner);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _alerts.Dispose();
        base.Dispose(disposing);
    }
}

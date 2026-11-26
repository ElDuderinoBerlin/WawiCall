using System.ComponentModel;
using DevExpress.Utils.Html;
using DevExpress.Utils.Svg;
using DevExpress.XtraBars.Alerter;
using WawiCallPro.App.Views;

namespace WawiCallPro.App.Hosting;

internal sealed record AppToastAction(string Caption, Func<CancellationToken, Task> ExecuteAsync);

/// <summary>Shows transient, skin-aware notifications on the owner form's UI thread.</summary>
public sealed class AppToast : Component
{
    private readonly AlertControl _alerts;
    private readonly HashSet<FrmToast> _cards = [];

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

    internal FrmToast ShowCard(Form anchor, string title, string message, string detail,
        IReadOnlyList<AppToastAction> actions, bool backgroundRun)
    {
        var card = new FrmToast();
        try
        {
            card.SetContent(title, message, detail);
            card.BindActions(actions);
            card.FormClosed += (_, _) => _cards.Remove(card);
            _cards.Add(card);
            card.ShowAt(anchor, backgroundRun);
            return card;
        }
        catch
        {
            _cards.Remove(card);
            card.Dispose();
            throw;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var card in _cards.ToArray()) card.Dispose();
            _cards.Clear();
            _alerts.Dispose();
        }
        base.Dispose(disposing);
    }
}

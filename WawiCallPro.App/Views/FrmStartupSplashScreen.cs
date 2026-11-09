using DevExpress.Skins;
using DevExpress.Utils;
using DevExpress.XtraEditors;
using WawiCallPro.App.Hosting;

namespace WawiCallPro.App.Views;

/// <summary>Startup logo, status and animated progress, following the LxTranslate splash.</summary>
public sealed partial class FrmStartupSplashScreen : XtraForm
{
    private bool _backgroundRun;
    private bool _sizing;

    public FrmStartupSplashScreen()
    {
        InitializeComponent();
        AppIconHelper.Apply(this);
        svgLogo.SvgImage = AppIconHelper.ApplicationSvg;
        svgLogo.SvgImageColorizationMode = SvgImageColorizationMode.None;
        lblVersion.Text = "v" + typeof(MainForm).Assembly.GetName().Version?.ToString(3);
        LookAndFeel.StyleChanged += LookAndFeel_StyleChanged;
        Disposed += (_, _) => LookAndFeel.StyleChanged -= LookAndFeel_StyleChanged;
        ApplySplashPalette();
        contentLayout.Layout += ContentLayout_Layout;
        ContentLayout_Layout(this, new LayoutEventArgs(this, null));
    }

    private void ContentLayout_Layout(object? sender, LayoutEventArgs e)
    {
        if (_sizing || contentLayout.Width <= 0) return;
        _sizing = true;
        try
        {
            // Percent spacer rows must never consume the space required by wrapped text.
            var contentHeight = contentLayout.GetPreferredSize(new Size(contentLayout.Width, 0)).Height;
            var height = contentHeight + rootLayout.Padding.Vertical + contentLayout.Margin.Vertical + Height - ClientSize.Height;
            if (MinimumSize.Height != height) MinimumSize = new Size(0, height);
        }
        finally { _sizing = false; }
    }

    public void SetStatus(string text, int step, int totalSteps)
    {
        lblStatus.Text = text;
        var total = Math.Max(1, totalSteps);
        progressBar.Position = (int)Math.Round(Math.Clamp(step, 0, total) * 100d / total);
    }

    private void LookAndFeel_StyleChanged(object? sender, EventArgs e) => ApplySplashPalette();

    private void ApplySplashPalette()
    {
        var colors = CommonSkins.GetSkin(LookAndFeel.ActiveLookAndFeel).Colors;
        var surface = colors.GetColor(CommonColors.Control);
        if (surface.IsEmpty) surface = SystemColors.Control;
        var text = colors.GetColor(CommonColors.ControlText);
        if (text.IsEmpty) text = surface.GetBrightness() < 0.5F ? Color.White : Color.Black;
        BackColor = rootLayout.BackColor = contentLayout.BackColor = progressBar.BackColor = surface;
        SetLabelColor(lblTitle, text);
        SetLabelColor(lblStatus, text);
        SetLabelColor(lblSubtitle, Blend(surface, text, 0.78));
        SetLabelColor(lblVersion, Blend(surface, text, 0.58));
    }

    private static Color Blend(Color background, Color foreground, double amount) => Color.FromArgb(
        (int)Math.Round(background.R + (foreground.R - background.R) * amount),
        (int)Math.Round(background.G + (foreground.G - background.G) * amount),
        (int)Math.Round(background.B + (foreground.B - background.B) * amount));

    private static void SetLabelColor(LabelControl label, Color color)
    {
        label.Appearance.ForeColor = color;
        label.Appearance.Options.UseForeColor = true;
    }

    internal void PrepareForBackgroundRun()
    {
        _backgroundRun = true;
        Opacity = 0;
    }

    protected override bool ShowWithoutActivation => _backgroundRun || base.ShowWithoutActivation;
    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            if (_backgroundRun) parameters.ExStyle |= 0x08000000;
            return parameters;
        }
    }
}

using System.Reflection;
using DevExpress.Utils;
using DevExpress.XtraEditors;
using WawiCallPro.App.Hosting;
using WawiCallPro.Utilities.Licensing;

namespace WawiCallPro.App.Views;

public sealed partial class FrmUeber : XtraForm
{
    private IProductLicenseService? _licenses;
    private Guid? _licenseId;
    private bool _backgroundRun;

    internal void PrepareForBackgroundRun()
    {
        _backgroundRun = true;
        Opacity = 0;
    }

    protected override bool ShowWithoutActivation => _backgroundRun || base.ShowWithoutActivation;

    public FrmUeber()
    {
        InitializeComponent();
        AppIconHelper.Apply(this);
        WindowWorkArea.Attach(this);
        UiButtonSizing.Attach(this);
        var assembly = typeof(MainForm).Assembly;
        lblProduct.Text = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "WawiCallPro";
        var controlsVersion = typeof(XtraForm).Assembly.GetName().Version?.ToString(3);
        lblVersion.Text = $"Version {ApplicationVersion.Display} (DevExpress {controlsVersion})";
        lblDesc.Text = assembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description
            ?? "Telefonie und Warenwirtschaft";
        lblCopyright.Text = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? "";
        Load += FitContents;
        FontChanged += FitContents;
        DpiChangedAfterParent += FitContents;
    }

    internal async Task LoadLicenseAsync(IProductLicenseService licenses, CancellationToken token)
    {
        _licenses = licenses;
        var license = await licenses.ValidateAsync(token);
        lblLicenseInfo.Text = FormatLicense(license);
        _licenseId = license.LicenseId;
        var number = _licenseId is { } id ? await licenses.GetStoredLicenseNumberAsync(id, token) : null;
        linkLicenseNumber.Visible = _licenseId.HasValue;
        linkLicenseNumber.Enabled = !string.IsNullOrWhiteSpace(number);
        linkLicenseNumber.Text = linkLicenseNumber.Enabled ? $"Lizenznummer: {number}" : "Lizenznummer: nicht gespeichert";
        linkMachineKey.Visible = license.Trial is null && license.Status is not (LicenseStatus.DemoInvalid or LicenseStatus.DemoExpired);
    }

    private void FitContents(object? sender, EventArgs e)
    {
        if (!IsHandleCreated || IsDisposed) return;
        var preferred = tableMain.GetPreferredSize(new Size(ClientSize.Width, 0));
        var height = Math.Max(LogicalToDeviceUnits(290), preferred.Height + Height - ClientSize.Height);
        MinimumSize = WindowWorkArea.LimitMinimum(new Size(LogicalToDeviceUnits(440), height), WindowWorkArea.GetArea(this));
    }

    private static string FormatLicense(ProductLicenseSnapshot license) => license switch
    {
        { Status: LicenseStatus.Valid, Trial: { } trial } =>
            $"Demo-Testzeitraum: {DemoTrialService.DurationDays} Tage (Start {trial.StartUtc.LocalDateTime:d}, Ende {trial.EndUtc.LocalDateTime:d}, verbleibende Tage: {trial.RemainingDays})",
        { Status: LicenseStatus.Development } => "Lizenz: Entwicklungsmodus",
        { Status: LicenseStatus.Valid, ValidUntilUtc: { } until } => $"Lizenz: gültig bis {until.LocalDateTime:d}",
        { Status: LicenseStatus.Valid } => "Lizenz: gültig",
        { Status: LicenseStatus.Expired or LicenseStatus.DemoExpired } => "Lizenz: abgelaufen",
        { Status: LicenseStatus.OnlineCheckRequired } => "Lizenz: Online-Prüfung erforderlich",
        { Status: LicenseStatus.Revoked } => "Lizenz: gesperrt",
        { Status: LicenseStatus.Missing } => "Lizenz: keine Lizenz hinterlegt",
        _ => "Lizenzstatus konnte nicht ermittelt werden."
    };

    private async void LicenseNumber_Click(object? sender, HyperlinkClickEventArgs e)
    {
        if (_licenses is not { } licenses || _licenseId is not { } id) return;
        using var waiting = new UiWaitCursor(this);
        try
        {
            var number = await licenses.GetStoredLicenseNumberAsync(id, CancellationToken.None);
            if (IsDisposed) return;
            var content = $"Lizenz-ID: {id:D}";
            if (!string.IsNullOrWhiteSpace(number)) content += $"{Environment.NewLine}Lizenznummer: {number}";
            Clipboard.SetText(content);
            XtraMessageBox.Show(this, "Lizenzdaten wurden in die Zwischenablage kopiert.", "Lizenz");
        }
        catch (Exception)
        {
            if (!IsDisposed) XtraMessageBox.Show(this, "Die Lizenzdaten konnten nicht kopiert werden.", "Lizenz");
        }
    }

    private async void MachineKey_Click(object? sender, HyperlinkClickEventArgs e)
    {
        if (_licenses is not { } licenses) return;
        using var waiting = new UiWaitCursor(this);
        try
        {
            var machineKey = await Task.Run(licenses.GetMachineId);
            if (IsDisposed) return;
            Clipboard.SetText(machineKey);
            XtraMessageBox.Show(this, "Der Machine Key wurde in die Zwischenablage kopiert.", "Lizenz");
        }
        catch (Exception)
        {
            if (!IsDisposed) XtraMessageBox.Show(this, "Der Machine Key konnte nicht kopiert werden.", "Lizenz");
        }
    }
}

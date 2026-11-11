using DevExpress.Skins;
using DevExpress.UserSkins;
using DevExpress.Utils;
using DevExpress.XtraEditors;
using Microsoft.Extensions.Hosting;
using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.App.Hosting;

/// <summary>Initializes available DevExpress skins and dialogs on the UI thread, before licensing or application forms.</summary>
internal sealed class AppUiStartup(IUiDispatcher dispatcher, ApplicationConfigurationService configuration) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => dispatcher.InvokeAsync(() =>
    {
        ApplyRenderingPreference(configuration.Current.Options.User.UseDirectXRendering, WindowsFormsSettings.ForceDirectXPaint);
        Apply();
    }, cancellationToken).AsTask();
    internal static void ApplyRenderingPreference(bool directX, Action enableDirectX)
    {
        if (directX) enableDirectX();
    }
    internal static void Apply()
    {
        BonusSkins.Register();
        SkinManager.EnableFormSkins();
        WindowsFormsSettings.UseDXDialogs = DefaultBoolean.True;
        // Unlike VerifyVersionInfo, the .NET check does not depend on an EXE compatibility manifest.
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
            WindowsFormsSettings.AllowRoundedWindowCorners = DefaultBoolean.True;
    }
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

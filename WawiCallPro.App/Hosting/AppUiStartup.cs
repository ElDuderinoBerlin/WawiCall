using DevExpress.Skins;
using DevExpress.UserSkins;
using DevExpress.Utils;
using DevExpress.XtraEditors;
using Microsoft.Extensions.Hosting;
using Quantarix.Windows.Win32;
using WawiCallPro.Interfaces.Hosting;

namespace WawiCallPro.App.Hosting;

/// <summary>Initializes available DevExpress skins and dialogs on the UI thread, before licensing or application forms.</summary>
internal sealed class AppUiStartup(IUiDispatcher dispatcher) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => dispatcher.InvokeAsync(Apply, cancellationToken).AsTask();
    internal static void Apply()
    {
        BonusSkins.Register();
        SkinManager.EnableFormSkins();
        WindowsFormsSettings.UseDXDialogs = DefaultBoolean.True;
        if (OSVersion.IsWindows11OrGreater) WindowsFormsSettings.AllowRoundedWindowCorners = DefaultBoolean.True;
    }
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

using DevExpress.XtraEditors;
using WawiCallPro.App.Hosting;
using WawiCallPro.App.Views;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    public Task OwnedApplicationWindowsUseSvgAndRasterApplicationIconsWithoutHandles() => RunStaAsync(() =>
    {
        foreach (var type in new[] { typeof(MainForm), typeof(FrmSettings), typeof(FrmDial), typeof(FrmDatabaseSetup), typeof(FrmCallerLookup), typeof(FrmStartupSplashScreen) })
        {
            using var window = (XtraForm)Activator.CreateInstance(type)!;
            Assert.AreSame(AppIconHelper.ApplicationSvg, window.IconOptions.SvgImage, type.Name);
            Assert.IsNotNull(window.IconOptions.Icon, type.Name);
            Assert.IsFalse(window.IsHandleCreated, type.Name);
        }
        return Task.CompletedTask;
    });
}

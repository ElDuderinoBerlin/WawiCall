using DevExpress.LookAndFeel;
using DevExpress.Utils;
using DevExpress.XtraEditors;
using WawiCallPro.App.Diagnostics;
using WawiCallPro.App.Views;
using WawiCallPro.Utilities.Licensing;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    public Task AboutShowsLocalLicenseAndVersionWithTheActiveSkin() => RunStaAsync(async () =>
    {
        var look = UserLookAndFeel.Default;
        var skin = look.ActiveSkinName;
        var palette = look.ActiveSvgPaletteName;
        var originalFont = WindowsFormsSettings.DefaultFont;
        var originalAppearance = AppearanceObject.DefaultFont;
        var folder = Path.Combine(RenderOutput(), "about");
        Directory.CreateDirectory(folder);
        try
        {
            foreach (var (theme, points) in new[] { ("Basic", 10F), ("Office 2019 Black", 16F) })
            {
                using var font = new Font("Segoe UI", points);
                WindowsFormsSettings.DefaultFont = AppearanceObject.DefaultFont = font;
                look.SetSkinStyle(theme);
                using var dialog = new FrmUeber { Font = font };
                dialog.PrepareForBackgroundRun();
                var licenses = new AboutLicenseProbe();
                await dialog.LoadLicenseAsync(licenses, default);
                dialog.Show();
                var product = Field<LabelControl>(dialog, "lblProduct");
                Assert.StartsWith("WawiCallPro", product.Text);
                Assert.Contains("DevExpress", Field<LabelControl>(dialog, "lblVersion").Text);
                Assert.Contains("gültig", Field<LabelControl>(dialog, "lblLicenseInfo").Text);
                Assert.Contains("Not for resale (NFR)", Field<LabelControl>(dialog, "lblLicenseInfo").Text);
                Assert.AreEqual("Lizenznummer: SYNTHETIC-ABOUT-NUMBER", Field<HyperlinkLabelControl>(dialog, "linkLicenseNumber").Text);
                Assert.IsTrue(Field<HyperlinkLabelControl>(dialog, "linkMachineKey").Visible);
                CheckTableBounds(dialog);
                using var shot = UiAudit.Capture(dialog);
                shot.Save(Path.Combine(folder, $"{product.Text}-{points:0}.png"), System.Drawing.Imaging.ImageFormat.Png);
                dialog.Scale(new SizeF(1.5F, 1.5F));
                CheckTableBounds(dialog);
            }
        }
        finally
        {
            WindowsFormsSettings.DefaultFont = originalFont;
            AppearanceObject.DefaultFont = originalAppearance;
            if (string.IsNullOrEmpty(palette)) look.SetSkinStyle(skin); else look.SetSkinStyle(skin, palette);
        }
    });

    private sealed class AboutLicenseProbe : IProductLicenseService
    {
        private readonly Guid _licenseId = Guid.NewGuid();
        public Task<ProductLicenseSnapshot> ValidateAsync(CancellationToken token) =>
            Task.FromResult(new ProductLicenseSnapshot(LicenseStatus.Valid, _licenseId, Online: true)
            { ProductLabel = "WawiCallPro", Type = Quantarix.License.Core.Models.LicenseType.NFR, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30), GraceDays = 7 });
        public Task<string?> GetStoredLicenseNumberAsync(Guid licenseId, CancellationToken token) =>
            Task.FromResult(licenseId == _licenseId ? "SYNTHETIC-ABOUT-NUMBER" : null);
        public Task<ProductLicenseSnapshot> ActivateAsync(string licenseKey, CancellationToken token) => throw new AssertFailedException("Unexpected activation.");
        public Task<ProductLicenseSnapshot> RefreshAsync(CancellationToken token) => throw new AssertFailedException("Unexpected network request.");
        public string GetMachineId() => "synthetic";
        public Task<CrashAuthentication?> GetCrashAuthenticationAsync(CancellationToken token) => Task.FromResult<CrashAuthentication?>(null);
    }
}

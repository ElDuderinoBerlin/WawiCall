using System.Security.Cryptography;
using System.Text.Json;
using DevExpress.XtraEditors;
using DevExpress.XtraTab;
using WawiCallPro.App.Diagnostics;
using WawiCallPro.App.Hosting;
using WawiCallPro.App.Views;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    [DataRow("Basic", 10F, 1.5F)]
    [DataRow("Office 2019 Black", 15F, 1F)]
    public Task AnnouncementCloudSettingsProtectSecretsAndExcludeThemFromExport(string skin, float points, float scale) => WithWorkspace(async (workspace, _, _) =>
    {
        using var key = RSA.Create(2048);
        var credential = JsonSerializer.Serialize(new
        {
            type = "service_account",
            project_id = "example-tts",
            client_email = "fixture@example-tts.iam.gserviceaccount.com",
            private_key_id = "test-only",
            private_key = key.ExportPkcs8PrivateKeyPem(),
            token_uri = "https://oauth2.googleapis.com/token"
        });
        const string azureKey = "not-a-real-azure-access-key";
        workspace.AnnouncementQuota = new AnnouncementCloudQuota(
            Path.Combine(workspace.Paths.RootDirectory, "announcement-quota-fixture.json"), TimeProvider.System);
        workspace.AnnouncementQuota.Reserve(AnnouncementSpeechProvider.AzureSpeech,
            new AnnouncementCloudSettings { AzureMonthlyAllowance = 100 }, "azure", "Hallo", default);
        using var font = new Font("Segoe UI", points);
        using var form = new FrmSettings(workspace);
        form.LookAndFeel.UseDefaultLookAndFeel = false;
        form.LookAndFeel.SetSkinStyle(skin);
        form.Font = font;
        form.PrepareForBackgroundRun();
        await form.PrepareAsync();
        var page = Field<SettingsPage>(form, "settingsPage");
        Field<XtraTabControl>(page, "tabs").SelectedTabPage = Field<XtraTabPage>(page, "audioTab");
        Field<XtraTabControl>(page, "audioPages").SelectedTabPage = Field<XtraTabPage>(page, "speechCloudTab");
        Field<TextEdit>(page, "speechAzureRegionEdit").Text = "GermanyWestCentral";
        Field<TextEdit>(page, "speechAzureKeyEdit").Text = azureKey;
        Field<TextEdit>(page, "speechGoogleProjectEdit").Text = "example-tts";
        Field<SpinEdit>(page, "quotaAzureEdit").Value = 45_000;
        Field<SpinEdit>(page, "quotaNeural2Edit").Value = 50_000;
        Field<SpinEdit>(page, "quotaChirpEdit").Value = 60_000;
        Assert.Throws<FormatException>(() => page.SetGoogleSpeechCredential(credential.Replace(
            "https://oauth2.googleapis.com/token", "https://example.invalid/token", StringComparison.Ordinal)));
        page.SetGoogleSpeechCredential(credential);
        await page.SaveAsync(default);
        var cloud = (await workspace.Settings.LoadAsync()).Options.Devices.Announcements.Cloud;
        Assert.AreEqual("germanywestcentral", cloud.AzureRegion);
        Assert.AreEqual("example-tts", cloud.GoogleProjectId);
        Assert.AreEqual(45_000, cloud.AzureMonthlyAllowance);
        Assert.AreEqual(50_000, cloud.GoogleNeural2MonthlyAllowance);
        Assert.AreEqual(60_000, cloud.GoogleChirpMonthlyAllowance);
        Assert.Contains("lokal reserviert", Field<LabelControl>(page, "quotaAzureUsed").Text);
        Assert.EndsWith("5", Field<LabelControl>(page, "quotaAzureUsed").Text);
        Assert.IsNotNull(cloud.AzureKey);
        Assert.IsNotNull(cloud.GoogleCredential);
        using (var secret = workspace.Secrets.Unprotect(cloud.AzureKey, AnnouncementCloudSettings.AzureKeyPurpose))
            Assert.AreEqual(azureKey, secret.Reveal());
        using (var secret = workspace.Secrets.Unprotect(cloud.GoogleCredential, AnnouncementCloudSettings.GoogleCredentialPurpose))
            Assert.AreEqual(credential, secret.Reveal());
        Assert.AreEqual("", Field<TextEdit>(page, "speechAzureKeyEdit").Text);
        Assert.AreEqual("Gespeichert", Field<LabelControl>(page, "speechGoogleCredentialEdit").Text);
        var output = Path.Combine(RenderOutput(), "announcements", workspace.Application.ProductLine);
        Directory.CreateDirectory(output);
        var portable = Path.Combine(output, $"cloud-preferences-{points:0}.json");
        await workspace.Settings.ExportPortableAsync(portable);
        var exported = await File.ReadAllTextAsync(portable);
        Assert.DoesNotContain("azureKey", exported);
        Assert.DoesNotContain("googleCredential", exported);
        Assert.DoesNotContain("example-tts", exported);
        form.ClientSize = new Size(690, 570);
        form.Show();
        if (scale != 1F) form.Scale(new SizeF(scale, scale));
        form.PerformLayout();
        Application.DoEvents();
        CheckTableBounds(form);
        using (var bitmap = UiAudit.Capture(form)) bitmap.Save(Path.Combine(output, $"cloud-settings-{points:0}.png"));
        Field<CheckEdit>(page, "speechAzureClear").Checked = true;
        Field<CheckEdit>(page, "speechGoogleClear").Checked = true;
        await page.SaveAsync(default);
        cloud = (await workspace.Settings.LoadAsync()).Options.Devices.Announcements.Cloud;
        Assert.IsNull(cloud.AzureKey);
        Assert.IsNull(cloud.GoogleCredential);
        await form.StopAsync();
    });
}

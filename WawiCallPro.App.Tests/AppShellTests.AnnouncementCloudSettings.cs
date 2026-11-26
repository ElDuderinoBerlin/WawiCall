using System.Security.Cryptography;
using System.Text.Json;
using DevExpress.XtraEditors;
using DevExpress.XtraTab;
using Microsoft.Extensions.DependencyInjection;
using WawiCallPro.App.Diagnostics;
using WawiCallPro.App.Hosting;
using WawiCallPro.App.Views;
using WawiCallPro.Audio;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    public Task AnnouncementSettingsLoadPreviewAndSaveTheSelectedVoice()
    {
        var output = new AnnouncementAudioDouble();
        return WithWorkspace(async (workspace, _, _) =>
        {
            var speech = new AnnouncementSpeechDouble { Voice = new("de-DE-Chirp3-HD-Puck", "Puck", "de-DE") };
            var saved = await workspace.Settings.LoadAsync();
            await workspace.Settings.SaveAsync(saved.Options with
            {
                Devices = saved.Options.Devices with { Announcements = new() { PlaybackDeviceId = "speaker" } }
            }, saved);
            using var form = new FrmSettings(workspace);
            form.ConfigureAnnouncementSpeech(provider => provider == AnnouncementSpeechProvider.GoogleCloud ? speech : null);
            form.PrepareForBackgroundRun();
            await form.PrepareAsync();
            var page = Field<SettingsPage>(form, "settingsPage");
            form.SettingsTabs.SelectedTabPage = Field<XtraTabPage>(page, "audioTab");
            Field<XtraTabControl>(page, "audioPages").SelectedTabPage = Field<XtraTabPage>(page, "speechCloudTab");
            form.Show();
            Field<LookUpEdit>(page, "standardSpeechProviderEdit").EditValue = AnnouncementSpeechProvider.GoogleCloud;
            await page.RefreshAnnouncementVoicesAsync();
            Assert.AreEqual("Stimmenliste geladen.", Field<LabelControl>(page, "speechTestStatus").Text);
            var voices = Field<LookUpEdit>(page, "standardSpeechVoiceEdit");
            Assert.AreEqual(DevExpress.XtraEditors.Controls.TextEditStyles.DisableTextEditor, voices.Properties.TextEditStyle);
            Assert.IsTrue(voices.Properties.UseDropDownRowsAsMaxCount);
            Assert.IsFalse(voices.Properties.ReadOnly);
            Assert.IsNotNull(voices.Properties.GetDataSourceRowByKeyValue(speech.Voice.Id));
            voices.EditValue = speech.Voice.Id;
            Field<TextEdit>(page, "speechTestEdit").Text = "Guten Tag.";
            await page.PreviewAnnouncementVoiceAsync();
            Assert.AreEqual("Wiedergabe beendet.", Field<LabelControl>(page, "speechTestStatus").Text);
            Assert.AreEqual(speech.Voice.Id, speech.SelectedVoice);
            Assert.AreEqual("speaker", output.PreviewDevice);
            Assert.IsNotNull(output.Draft);
            Assert.IsFalse(File.Exists(output.Draft));

            await page.SaveAsync(default);
            Assert.AreEqual(AnnouncementSpeechProvider.GoogleCloud, (await workspace.Settings.LoadAsync()).Options.Devices.Announcements.SpeechProvider);
            using var dialog = new FrmAnnouncements(output, new AnnouncementSpeechDouble(), new AnnouncementConversionDouble(),
                new AnnouncementCatalogDouble(), workspace.Settings, null) { ResolveCloudSpeech = _ => speech };
            await dialog.LoadAsync();
            var selectedVoice = Field<LookUpEdit>(dialog, "voiceEdit");
            Assert.AreEqual(speech.Voice.Id, selectedVoice.EditValue);
            Assert.AreEqual(DevExpress.XtraEditors.Controls.TextEditStyles.DisableTextEditor, selectedVoice.Properties.TextEditStyle);
            Assert.IsTrue(selectedVoice.Properties.UseDropDownRowsAsMaxCount);
            await form.StopAsync();
        }, configure: services => services.AddSingleton<IAnnouncementAudio>(output));
    }
    [TestMethod]
    public Task AnnouncementCloudProviderTabSurvivesReopening() => WithWorkspace(async (workspace, _, _) =>
    {
        var state = new AppUiState(workspace.Application, new(false), @"Software\WawiCallPro.Tests\CloudTabs\" + Guid.NewGuid().ToString("N"));
        try
        {
            using (var form = new FrmSettings(workspace))
            {
                form.PrepareForBackgroundRun();
                form.ConfigureLayoutPersistence(state);
                await form.PrepareAsync();
                form.Show();
                var page = Field<SettingsPage>(form, "settingsPage");
                form.SettingsTabs.SelectedTabPage = Field<XtraTabPage>(page, "audioTab");
                Field<XtraTabControl>(page, "audioPages").SelectedTabPage = Field<XtraTabPage>(page, "speechCloudTab");
                Field<XtraTabControl>(page, "speechProviderPages").SelectedTabPage = Field<XtraTabPage>(page, "speechDeepLTab");
                Assert.AreEqual("speechDeepLTab", state.ReadTab("Settings.Audio.Cloud"));
                await form.StopAsync();
            }

            using var reopened = new FrmSettings(workspace);
            reopened.ConfigureLayoutPersistence(state);
            await reopened.PrepareAsync();
            var reopenedPage = Field<SettingsPage>(reopened, "settingsPage");
            Assert.AreEqual("speechDeepLTab", Field<XtraTabControl>(reopenedPage, "speechProviderPages").SelectedTabPage.Name);
            await reopened.StopAsync();
        }
        finally { state.Reset(); }
    });

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
        const string deepLKey = "not-a-real-deepl-access-key";
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
        var providerPages = Field<XtraTabControl>(page, "speechProviderPages");
        Field<TextEdit>(page, "speechAzureRegionEdit").Text = "GermanyWestCentral";
        Field<TextEdit>(page, "speechAzureKeyEdit").Text = azureKey;
        Field<TextEdit>(page, "speechGoogleProjectEdit").Text = "example-tts";
        var deepLEdit = Field<TextEdit>(page, "speechDeepLKeyEdit");
        Assert.IsTrue(deepLEdit.Properties.UseSystemPasswordChar);
        deepLEdit.Text = deepLKey;
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
        Assert.IsNotNull(cloud.DeepLKey);
        using (var secret = workspace.Secrets.Unprotect(cloud.AzureKey, AnnouncementCloudSettings.AzureKeyPurpose))
            Assert.AreEqual(azureKey, secret.Reveal());
        using (var secret = workspace.Secrets.Unprotect(cloud.GoogleCredential, AnnouncementCloudSettings.GoogleCredentialPurpose))
            Assert.AreEqual(credential, secret.Reveal());
        using (var secret = workspace.Secrets.Unprotect(cloud.DeepLKey, AnnouncementCloudSettings.DeepLKeyPurpose))
            Assert.AreEqual(deepLKey, secret.Reveal());
        Assert.Throws<Utilities.Security.SecretProtectionException>(() =>
            workspace.Secrets.Unprotect(cloud.DeepLKey, AnnouncementCloudSettings.AzureKeyPurpose));
        Assert.DoesNotContain(deepLKey, await File.ReadAllTextAsync(Path.Combine(workspace.Paths.RootDirectory, "appsettings.json")));
        Assert.AreEqual("", deepLEdit.Text);
        Assert.AreEqual("Gespeichert", deepLEdit.Properties.NullValuePrompt);
        Assert.AreEqual("", Field<TextEdit>(page, "speechAzureKeyEdit").Text);
        Assert.AreEqual("Gespeichert", Field<LabelControl>(page, "speechGoogleCredentialEdit").Text);
        var output = Path.Combine(RenderOutput(), "announcements", workspace.Application.ProductLine);
        Directory.CreateDirectory(output);
        var portable = Path.Combine(output, $"cloud-preferences-{points:0}.json");
        await workspace.Settings.ExportPortableAsync(portable);
        var exported = await File.ReadAllTextAsync(portable);
        Assert.DoesNotContain("azureKey", exported);
        Assert.DoesNotContain("googleCredential", exported);
        Assert.DoesNotContain("deepLKey", exported);
        Assert.DoesNotContain(deepLKey, exported);
        Assert.DoesNotContain("example-tts", exported);
        form.ClientSize = new Size(690, 570);
        form.Show();
        if (scale != 1F) form.Scale(new SizeF(scale, scale));
        foreach (XtraTabPage provider in providerPages.TabPages)
        {
            providerPages.SelectedTabPage = provider;
            form.PerformLayout();
            Application.DoEvents();
            CheckTableBounds(form);
            using var bitmap = UiAudit.Capture(form);
            bitmap.Save(Path.Combine(output, $"cloud-settings-{provider.Name}-{points:0}.png"));
        }
        Field<CheckEdit>(page, "speechAzureClear").Checked = true;
        Field<CheckEdit>(page, "speechGoogleClear").Checked = true;
        await page.SaveAsync(default);
        var retained = (await workspace.Settings.LoadAsync()).Options.Devices.Announcements.Cloud.DeepLKey;
        Assert.IsNotNull(retained);
        using (var secret = workspace.Secrets.Unprotect(retained, AnnouncementCloudSettings.DeepLKeyPurpose))
            Assert.AreEqual(deepLKey, secret.Reveal());
        Field<CheckEdit>(page, "speechDeepLClear").Checked = true;
        await page.SaveAsync(default);
        cloud = (await workspace.Settings.LoadAsync()).Options.Devices.Announcements.Cloud;
        Assert.IsNull(cloud.AzureKey);
        Assert.IsNull(cloud.GoogleCredential);
        Assert.IsNull(cloud.DeepLKey);
        await form.StopAsync();
    });
}

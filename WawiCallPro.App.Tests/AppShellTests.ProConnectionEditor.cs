#if LX_PRO
using System.ComponentModel;
using System.Reflection;
using System.Drawing.Imaging;
using DevExpress.LookAndFeel;
using DevExpress.XtraEditors;
using Microsoft.Extensions.DependencyInjection;
using WawiCallPro.App.Views;
using WawiCallPro.Interfaces.Plugins;
using WawiCallPro.Plugin.Windows;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Plugins;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    public Task RealProConnectionEditorLoadsFromPluginValidatesAndRendersInCentralSettings() => WithWorkspace(async (workspace, services, plugin) =>
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "WawiCallProSolution.sln"))) root = root.Parent;
        Assert.IsNotNull(root);
        var configuration = typeof(MainForm).Assembly.GetCustomAttribute<System.Reflection.AssemblyConfigurationAttribute>()!.Configuration;
        var directory = Path.Combine(root.FullName, "WawiCallPro.App", "bin", configuration, "net11.0-windows10.0.19041.0", "plugins", "WawiCallPro.Plugins.Lexware.Pro");
        Assert.IsFalse(Directory.GetFiles(directory, "DevExpress.*.dll").Any(), "The plugin must use the host UI assemblies.");
        await using var session = services.GetRequiredService<IPluginSessionFactory>().CreateTrusted();
        await ((ISelectedPluginSession)session).LoadSelectedAsync(directory);
        var provider = session.GetPlugins().Single() as IWawiCallPluginSettings;
        Assert.IsNotNull(provider);
        plugin.SettingsFactory = context =>
        {
            var view = provider.CreateConnectionSettings(context);
            var property = view.View.GetType().GetProperty("Directories", BindingFlags.NonPublic | BindingFlags.Instance)!;
            property.SetValue(view.View, new PreviewFactoryDouble(workspace.Directory));
            view.Reload(context);
            return view;
        };
        plugin.ConnectionSettingsTitle = provider.ConnectionSettingsTitle;
        var oldSkin = UserLookAndFeel.Default.ActiveSkinName;
        var oldFont = WindowsFormsSettings.DefaultFont;
        var oldAppearance = DevExpress.Utils.AppearanceObject.DefaultFont;
        var firstEditor = true;
        try
        {
            foreach (var skin in new[] { "Basic", "Office 2019 Black" })
            foreach (var fontSize in new[] { 9F, 16F })
            {
                UserLookAndFeel.Default.SetSkinStyle(skin);
                using var font = new Font("Segoe UI", fontSize);
                WindowsFormsSettings.DefaultFont = DevExpress.Utils.AppearanceObject.DefaultFont = font;
                using var form = new FrmSettings(workspace);
                form.PrepareForBackgroundRun(); form.Font = font; form.Show();
                var page = Field<SettingsPage>(form, "settingsPage"); await page.ReloadAsync();
                form.SelectAutomationTab("settings-wawi"); Application.DoEvents();
                Assert.AreEqual("Lexware", page.SettingsTabs.SelectedTabPage.Text);
                var exported = Field<Control?>(page, "_pluginSettingsControl"); Assert.IsNotNull(exported);
                Assert.AreEqual("LexwareSettingsPage", exported.GetType().Name);
                Assert.IsNotNull(Field<PluginCompanySettingsPage>(exported, "companyPage"));
                var editor = Field<Control>(exported, "connectionPage");
                var output = Path.Combine(RenderOutput(), "pro-connection-editor"); Directory.CreateDirectory(output);
                Assert.IsTrue(typeof(XtraUserControl).IsAssignableFrom(editor.GetType()), "Default-context DevExpress type identity must be preserved.");
                Assert.AreEqual(skin, ((XtraUserControl)editor).LookAndFeel.ActiveSkinName);
                using (var designInstance = (Control)Activator.CreateInstance(editor.GetType())!) Assert.IsFalse(designInstance.IsHandleCreated);
                var manager = new System.Resources.ResourceManager(editor.GetType());
                try { Assert.IsNotNull(manager.GetResourceSet(System.Globalization.CultureInfo.InvariantCulture, true, false)); }
                finally { manager.ReleaseAllResources(); }
                if (firstEditor)
                {
                    using var emptyPicture = WawiCallPro.App.Diagnostics.UiAudit.Capture(form);
                    emptyPicture.Save(Path.Combine(output, "dialog-new-empty.png"), ImageFormat.Png);
                }
                Field<CheckEdit>(editor, "enabledEdit").Checked = true;
                Field<TextEdit>(editor, "apiUrlEdit").Text = "https://lexware.example.invalid/api";
                Field<TextEdit>(editor, "usernameEdit").Text = "test-api";
                if (firstEditor)
                {
                    Assert.AreEqual(PluginSettingsIssue.PasswordRequired, Assert.ThrowsExactly<PluginSettingsValidationException>(() => page.ReadOptions()).Issue);
                    Field<TextEdit>(editor, "passwordEdit").Text = "isolated-only-secret";
                    Field<TextEdit>(editor, "clientKeyEdit").Text = Guid.NewGuid().ToString("B");
                    var context = Field<WawiCallPro.App.Hosting.PluginSettingsPageContext>(page, "_pluginPageContext");
                    await Field<PluginCompanySettingsPage>(exported, "companyPage").LoadAsync();
                    context.StageSelection((await workspace.Directory.GetCompaniesAsync())[0].Key);
                    await page.SaveAsync(CancellationToken.None);
                    Assert.IsNotNull(workspace.Companies.Current.Company);
                    context.StageSelection(null);
                    firstEditor = false;
                }
                Field<TextEdit>(editor, "passwordEdit").Text = "isolated-only-secret";
                Field<TextEdit>(editor, "clientKeyEdit").Text = Guid.NewGuid().ToString("B");
                Assert.IsTrue(Field<TextEdit>(editor, "passwordEdit").Properties.UseSystemPasswordChar);
                Assert.IsTrue(Field<TextEdit>(editor, "clientKeyEdit").Properties.UseSystemPasswordChar);
                var expected = page.ReadOptions(); Assert.IsNotNull(expected.Erp.Pro?.Password);
                var clientKeyText = Field<TextEdit>(editor, "clientKeyEdit").Text;
                Assert.IsNotNull(page.ReadOptions().Erp.Pro!.Password);
                Field<TextEdit>(editor, "clientKeyEdit").Text = clientKeyText;
                var oldEndpoint = Field<TextEdit>(editor, "apiUrlEdit").Text;
                foreach (var endpoint in new[] { "invalid", "https://user:password@example.invalid", "https://example.invalid/?secret=hidden", "file:///local" })
                {
                    Field<TextEdit>(editor, "apiUrlEdit").Text = endpoint;
                    var error = Assert.ThrowsExactly<PluginSettingsValidationException>(() => page.ReadOptions());
                    Assert.AreEqual(PluginSettingsIssue.InvalidEndpoint, error.Issue);
                    Assert.DoesNotContain(endpoint, error.Message);
                }
                Field<TextEdit>(editor, "apiUrlEdit").Text = oldEndpoint;
                Field<TextEdit>(editor, "usernameEdit").Text = "white space";
                Assert.AreEqual(PluginSettingsIssue.UsernameRequired, Assert.ThrowsExactly<PluginSettingsValidationException>(() => page.ReadOptions()).Issue);
                Field<TextEdit>(editor, "usernameEdit").Text = "test-api";
                Field<TextEdit>(editor, "clientKeyEdit").Text = "invalid-key";
                Assert.AreEqual(PluginSettingsIssue.InvalidClientKey, Assert.ThrowsExactly<PluginSettingsValidationException>(() => page.ReadOptions()).Issue);
                Field<TextEdit>(editor, "clientKeyEdit").Text = Guid.NewGuid().ToString("B");
                form.ClientSize = new Size(900, 740); form.PerformLayout(); Application.DoEvents();
                CheckTableBounds(editor);
                using (var picture = new Bitmap(exported.Width, exported.Height))
                {
                    exported.DrawToBitmap(picture, exported.ClientRectangle);
                    picture.Save(Path.Combine(output, $"editor-{skin.Replace(' ', '-')}-{fontSize}.png"), ImageFormat.Png);
                }
                foreach (var name in new[] { "saveButton", "cancelButton", "importButton", "exportButton" })
                {
                    var button = Field<SimpleButton>(page, name);
                    Assert.IsTrue(form.ClientRectangle.Contains(form.RectangleToClient(button.RectangleToScreen(button.ClientRectangle))), name);
                }
                using (var picture = WawiCallPro.App.Diagnostics.UiAudit.Capture(form))
                    picture.Save(Path.Combine(output, $"dialog-{skin.Replace(' ', '-')}-{fontSize}.png"), ImageFormat.Png);
                if (fontSize == 16F)
                {
                    var tab = page.SettingsTabs.SelectedTabPage;
                    var hint = Field<LabelControl>(editor, "hintLabel");
                    tab.ScrollControlIntoView(hint); Application.DoEvents();
                    using var scrolledPicture = WawiCallPro.App.Diagnostics.UiAudit.Capture(form);
                    scrolledPicture.Save(Path.Combine(output, $"dialog-{skin.Replace(' ', '-')}-16-scrolled.png"), ImageFormat.Png);
                    Assert.IsTrue(tab.ClientRectangle.Contains(tab.RectangleToClient(hint.RectangleToScreen(hint.ClientRectangle))), $"All content must remain reachable. Tab={tab.ClientRectangle}; Hint={tab.RectangleToClient(hint.RectangleToScreen(hint.ClientRectangle))}; Scroll={tab.AutoScrollPosition}; Display={tab.DisplayRectangle}; Vertical={tab.VerticalScroll.Visible}");
                }
                await File.WriteAllTextAsync(Path.Combine(output, $"metadata-{skin.Replace(' ', '-')}-{fontSize}.json"),
                    System.Text.Json.JsonSerializer.Serialize(new { FontSize = fontSize, DeviceDpi = editor.DeviceDpi,
                        EditorSize = editor.Size, DialogSize = form.ClientSize, PasswordMasked = Field<TextEdit>(editor, "passwordEdit").Properties.UseSystemPasswordChar,
                        ClientKeyMasked = Field<TextEdit>(editor, "clientKeyEdit").Properties.UseSystemPasswordChar, SharedHostUiTypes = true }));
                if (skin == "Basic" && fontSize == 9F)
                {
                    form.Scale(new SizeF(1.5F, 1.5F)); form.PerformLayout(); Application.DoEvents(); CheckTableBounds(editor);
                    using var scaledPicture = WawiCallPro.App.Diagnostics.UiAudit.Capture(form);
                    scaledPicture.Save(Path.Combine(output, "dialog-scale150.png"), ImageFormat.Png);
                }
                await page.SaveAndOfferRestartAsync(() => false, CancellationToken.None);
                Assert.AreEqual("", Field<TextEdit>(editor, "passwordEdit").Text);
                Assert.AreEqual("", Field<TextEdit>(editor, "clientKeyEdit").Text);
                var confirmations = 0;
                await page.SaveAndOfferRestartAsync(() => { confirmations++; return false; }, CancellationToken.None);
                Assert.AreEqual(0, confirmations, "Unchanged saved ciphertext must not cause another restart prompt.");
                await form.StopAsync(); form.Close();
            }
        }
        finally
        {
            WindowsFormsSettings.DefaultFont = oldFont;
            DevExpress.Utils.AppearanceObject.DefaultFont = oldAppearance;
            UserLookAndFeel.Default.SetSkinStyle(oldSkin);
        }
    });
    private sealed class PreviewFactoryDouble(WawiCallPro.Interfaces.Directory.IBusinessDirectory source) : IPluginConnectionDirectoryFactory
    {
        public IPluginConnectionDirectory Create(ProConnectionConfiguration settings) => new PreviewLeaseDouble(source);
    }
    private sealed class PreviewLeaseDouble(WawiCallPro.Interfaces.Directory.IBusinessDirectory source) : IPluginConnectionDirectory
    {
        public WawiCallPro.Interfaces.Directory.IBusinessDirectory Directory => source;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

}
#endif

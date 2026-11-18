using System.Collections.Immutable;
using System.Drawing.Imaging;
using System.IO;
using DevExpress.Utils;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid.Views.Tile;
using Microsoft.Extensions.DependencyInjection;
using WawiCallPro.App.Hosting;
using WawiCallPro.App.Views;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Office;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Office;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    [DataRow(1240, 10F, 1F, "Office 2019 Colorful")]
    [DataRow(900, 16F, 1F, "Office 2019 Black")]
    [DataRow(1240, 10F, 1.5F, "Office 2019 Colorful")]
    public Task IncomingContactCallShowsMailFromItsSecondAddressThroughTheActualCallPage(int width, float size, float scale, string skin) => WithWorkspace(async (workspace, services, plugin) =>
    {
        await SeedCallerCachesAsync(workspace, services);
        var companies = await workspace.Directory.GetCompaniesAsync(); var company = companies[0].Key;
        plugin.ProductLine = company.ProductLine;
        var repository = services.GetRequiredService<IPartyCacheRepository>(); var state = await repository.GetStateAsync(company);
        var original = (await repository.GetPageAsync(new(company, new()))).Page.Items;
        var rows = original.Select(party => party with
        {
            ContactFieldsVersion = 1,
            Phones = party.Key.Kind == PartyKind.SupplierContact ? party.Phones : [],
            Emails = party.Key.Kind == PartyKind.SupplierContact
                ? [new PartyEmail("first@example.test", new("contact", "03", "email1")), new PartyEmail("second@example.test", new("contact", "03", "email2"))]
                : [new PartyEmail("owner@example.test", new("owner", party.Key.ExternalId, "email"))]
        }).ToImmutableArray();
        await repository.ReplaceAsync(new(company, state.Revision, rows, DateTimeOffset.UtcNow, state.ParserVersion, state.ContextVersion));
        var contact = rows.Single(party => party.Key.Kind == PartyKind.SupplierContact);
        var account = new MailAccountSettings { Id = Guid.NewGuid(), DisplayName = "Synthetic", Provider = MailProviderKind.OutlookDesktop, Company = company, FolderIds = ["inbox"], ImportDays = 0 };
        var settings = await workspace.Settings.LoadAsync();
        await workspace.Settings.SaveAsync(settings.Options with
        { MailAccounts = [account], Companies = settings.Options.Companies.Select(item => item with { IncludeInPhoneCache = item.Company == company }).ToImmutableArray() }, settings);
        var provider = new OwnerMailConnector(account.Id); var factory = new OwnerMailFactory(provider);
        var direct = ActivatorUtilities.CreateInstance<DirectMailViewSource>(services, factory);
        var browser = ActivatorUtilities.CreateInstance<MailCacheService>(services, factory, direct);
        var mail = new MailApplication(services.GetRequiredService<MailAccounts>(), browser, browser, factory, services.GetRequiredService<PartyMailScopeResolver>());
        var scopedWorkspace = ActivatorUtilities.CreateInstance<AppWorkspace>(services, mail);
        await browser.SynchronizeAsync(account.Id);
        ((TestDirectory)workspace.Directory).ReadParty = (key, token) => repository.GetAsync(key, token);
        var decision = await BuildWorkspaceDecisionAsync(workspace);
        Assert.AreEqual(contact.Key, decision.Selection!.Party);
        var oldFont = WindowsFormsSettings.DefaultFont; var oldAppearance = AppearanceObject.DefaultFont;
        var oldSkin = DevExpress.LookAndFeel.UserLookAndFeel.Default.ActiveSkinName; var oldPalette = DevExpress.LookAndFeel.UserLookAndFeel.Default.ActiveSvgPaletteName;
        try
        {
            DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(skin);
            using var font = new Font("Segoe UI", size); WindowsFormsSettings.DefaultFont = AppearanceObject.DefaultFont = font;
            using var host = new FrmCallerLookup(); host.PrepareForBackgroundRun(); host.Font = font; host.ClientSize = new(width, 900);
            foreach (Control control in host.Controls.Cast<Control>().ToArray()) control.Dispose(); host.Controls.Clear();
            using var page = new CallDetailsPage { Dock = DockStyle.Fill }; page.Bind(scopedWorkspace, await CreateWorkspaceCallAsync(services)); host.Controls.Add(page); host.Show(); CreateHandles(page);
            if (scale != 1F) host.Scale(new SizeF(scale, scale));
            await page.AcceptAsync(decision, CancellationToken.None); await page.PendingLoads;
            var view = Field<Dictionary<PartyKey, MailPage>>(page, "_mailViews")[contact.Key];
            var tab = (DevExpress.XtraTab.XtraTabPage)view.Parent!; Field<DevExpress.XtraTab.XtraTabControl>(page, "tabs").SelectedTabPage = tab;
            host.PerformLayout(); CreateHandles(view); await view.ReloadAsync(); CheckVisibleMailBounds(view);
            Assert.AreEqual(1, Field<TileView>(view, "messagesView").DataRowCount, Field<LabelControl>(view, "statusLabel").Text);
            Assert.AreEqual("Synthetic second-contact mail", Field<LabelControl>(view, "subjectLabel").Text);
            Assert.AreEqual(contact.Key, page.Context!.Party); Assert.AreEqual(0D, host.Opacity); Assert.IsFalse(host.ShowInTaskbar);
            var output = Path.Combine(RenderOutput(), "owner-mail"); Directory.CreateDirectory(output);
            using var image = WawiCallPro.App.Diagnostics.UiAudit.CaptureMail(view); image.Save(Path.Combine(output, $"owner-{workspace.Application.ProductLine}-{width}-{size:0}-{scale:0.0}.png"), ImageFormat.Png);
            var changed = await workspace.Settings.LoadAsync();
            await workspace.Settings.SaveAsync(changed.Options with { Database = changed.Options.Database with { SqliteFileName = "another-database.db" } }, changed);
            var denied = await Assert.ThrowsExactlyAsync<MailException>(() => mail.ForParty(contact).GetMessagesAsync(new("outlook", account.Id.ToString("N"), "inbox"), ""));
            Assert.AreEqual(MailFailure.Conflict, denied.Failure);
            var restore = await workspace.Settings.LoadAsync();
            await workspace.Settings.SaveAsync(restore.Options with { Database = changed.Options.Database }, restore);
            plugin.ProductLine = company.ProductLine == LexwareProductLine.Pro ? LexwareProductLine.FinOffice : LexwareProductLine.Pro;
            var wrongLine = await Assert.ThrowsExactlyAsync<MailException>(() => mail.ForParty(contact).GetFoldersAsync());
            Assert.AreEqual(MailFailure.AccessDenied, wrongLine.Failure);
            plugin.ProductLine = company.ProductLine;
            await page.StopAsync();
        }
        finally
        {
            WindowsFormsSettings.DefaultFont = oldFont; AppearanceObject.DefaultFont = oldAppearance;
            if (string.IsNullOrEmpty(oldPalette)) DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(oldSkin);
            else DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(oldSkin, oldPalette);
        }
    });

    private sealed class OwnerMailFactory(OwnerMailConnector provider) : IMailConnectorFactory
    { public IMailConnector Create(MailAccountSettings account) => provider; }
    private sealed class OwnerMailConnector(Guid account) : IMailConnector
    {
        public Guid AccountId => account;
        public string Provider => "outlook";
        private MailEnvelope CreateEnvelope() => new(new(new(Provider, account.ToString("N"), "message"), "Synthetic second-contact mail", "second@example.test", "our-mailbox@example.test", "",
            DateTimeOffset.UtcNow, "Synthetic body", null, []), null, "second@example.test", [], ["inbox"], "revision");
        public Task<MailFolderCatalog> GetFoldersAsync(CancellationToken cancellationToken = default) => Task.FromResult(new MailFolderCatalog([], ""));
        public Task<ImmutableArray<MailListItem>> GetMessagesAsync(string folderId, string search, CancellationToken cancellationToken = default) => throw new AssertFailedException();
        public Task<MailEnvelope?> GetMessageAsync(string messageId, CancellationToken cancellationToken = default) => Task.FromResult<MailEnvelope?>(CreateEnvelope());
        public Task<MailChangePage> GetChangesAsync(string folderId, string? checkpoint, string? nextPage, CancellationToken cancellationToken = default) =>
            Task.FromResult(new MailChangePage([CreateEnvelope()], [], null, "checkpoint") { FullSnapshot = true });
        public Task<byte[]> GetAttachmentAsync(string messageId, string attachmentId, CancellationToken cancellationToken = default) => throw new AssertFailedException();
    }
}

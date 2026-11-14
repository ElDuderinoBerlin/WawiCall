using System.Collections.Immutable;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using DevExpress.Office.Services;
using DevExpress.Utils;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraRichEdit;
using DevExpress.XtraTreeList;
using Microsoft.Extensions.DependencyInjection;
using WawiCallPro.App.Hosting;
using WawiCallPro.App.Views;
using WawiCallPro.Interfaces.Office;
using WawiCallPro.Utilities.Hosting;
using WawiCallPro.Utilities.Diagnostics;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    public Task MailViewDisplaysFoldersUnreadMessagesHtmlAndAttachmentNamesWithoutOpeningResources() => RunStaAsync(async () =>
    {
        var source = new MailSourceDouble();
        using var page = new MailPage(); page.Bind(source, null); CreateHandles(page); await page.ReloadAsync();
        Assert.AreEqual("Rückfrage zum Angebot", Field<LabelControl>(page, "subjectLabel").Text);
        Assert.Contains("kunde@example.test", Field<LabelControl>(page, "senderLabel").Text);
        Assert.Contains("Angebot.pdf", Field<LabelControl>(page, "attachmentsLabel").Text);
        var reader = Field<RichEditControl>(page, "body"); Assert.IsTrue(reader.ReadOnly); Assert.Contains("Guten Tag", reader.Text);
        Assert.AreEqual(DocumentCapability.Disabled, reader.Options.Behavior.Open);
        Assert.AreEqual(DocumentCapability.Disabled, reader.Options.Behavior.SaveAs);
        var resources = reader.GetService<IUriStreamService>(); Assert.IsInstanceOfType<BlockedMailResources>(resources);
        Assert.AreSame(Stream.Null, resources.GetStream("https://example.test/tracker"));
        Assert.AreSame(Stream.Null, resources.GetStream("file:///C:/private.png"));
        Assert.IsFalse(Field<GridView>(page, "messagesView").IsFindPanelVisible);
        Assert.AreEqual(2, Field<GridView>(page, "messagesView").DataRowCount); Assert.AreEqual(0, source.Writes);
        await page.StopAsync();
    });

    [TestMethod]
    public Task MailViewRoutesSearchToTheSourceAndKeepsBackendHitsOutsideTheListSnippet() => RunStaAsync(async () =>
    {
        var source = new MailSourceDouble();
        using var page = new MailPage(); page.Bind(source, null); CreateHandles(page); await page.ReloadAsync();
        var grid = Field<GridView>(page, "messagesView"); grid.ApplyFindFilter("Suchwort nur im vollständigen Text");
        await page.LoadCurrentFolderAsync();
        Assert.AreEqual("Suchwort nur im vollständigen Text", source.LastSearch); Assert.AreEqual(2, grid.DataRowCount);
        var key = grid.GetFocusedRowCellValue("Subject"); Assert.IsNotNull(key);
        await page.StopAsync();
    });

    [TestMethod]
    public Task MailViewRejectsForeignMessageIdentityAndReportsErrorsWithoutSourceDetails() => RunStaAsync(async () =>
    {
        var source = new MailSourceDouble();
        using var page = new MailPage(); page.Bind(source, null); CreateHandles(page); await page.ReloadAsync();
        source.Read = (key, _) => Task.FromResult<MailContent?>(source.Content(key) with { Key = key with { Account = "foreign" }, Text = "private-body", Html = null });
        await page.LoadFocusedMessageAsync(); Assert.DoesNotContain("private-body", Field<RichEditControl>(page, "body").Text);
        Assert.Contains("nicht geladen", Field<LabelControl>(page, "subjectLabel").Text);
        source.List = (_, _, _) => throw new InvalidOperationException("private-source-token");
        await page.LoadCurrentFolderAsync(); Assert.AreEqual(0, Field<GridView>(page, "messagesView").DataRowCount);
        Assert.DoesNotContain("private-source-token", Field<LabelControl>(page, "statusLabel").Text); await page.StopAsync();
    });

    [TestMethod]
    public Task MailViewRejectsCrossAccountAndCyclicFolderHierarchies() => RunStaAsync(async () =>
    {
        var source = new MailSourceDouble();
        using var page = new MailPage(); page.Bind(source, null); CreateHandles(page);
        foreach (var invalid in new[]
        {
            ImmutableArray.Create(new MailFolder(source.Inbox, source.Inbox, "Zyklus")),
            ImmutableArray.Create(new MailFolder(source.Inbox, source.Inbox with { Account = "foreign" }, "Fremder Mandant")),
            ImmutableArray.Create(new MailFolder(source.Inbox, null, "eins"), new MailFolder(source.Inbox, null, "doppelt"))
        })
        {
            source.Catalog = new(invalid, ""); await page.ReloadAsync();
            Assert.Contains("fehlgeschlagen", Field<LabelControl>(page, "statusLabel").Text);
            Assert.AreEqual(0, Field<GridView>(page, "messagesView").DataRowCount);
        }
        await page.StopAsync();
    });

    [TestMethod]
    public Task MailViewIgnoresLateMessagesFromAPreviousFolder() => RunStaAsync(async () =>
    {
        var source = new MailSourceDouble();
        using var page = new MailPage(); page.Bind(source, null); CreateHandles(page); await page.ReloadAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); CancellationToken oldToken = default;
        source.List = async (folder, _, token) =>
        {
            if (folder == source.Inbox) { oldToken = token; entered.TrySetResult(); await release.Task; }
            return source.Messages(folder);
        };
        var old = page.LoadCurrentFolderAsync(); await entered.Task;
        var tree = Field<TreeList>(page, "folderTree"); tree.FocusedNode = tree.FindNodeByKeyID(source.Sent);
        await page.LoadCurrentFolderAsync(); Assert.IsTrue(oldToken.IsCancellationRequested);
        release.SetResult(); await old;
        Assert.AreEqual("Gesendet", Field<LabelControl>(page, "folderLabel").Text);
        Assert.Contains("Ihre Unterlagen", Field<LabelControl>(page, "subjectLabel").Text); await page.StopAsync();
    });

    [TestMethod]
    public Task MailViewIgnoresLatePreviewFromAPreviousMessage() => RunStaAsync(async () =>
    {
        var source = new MailSourceDouble();
        using var page = new MailPage(); page.Bind(source, null); CreateHandles(page); await page.ReloadAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        source.Read = async (key, _) => { if (key.Id == "1") { entered.TrySetResult(); await release.Task; } return source.Content(key); };
        var old = page.LoadFocusedMessageAsync(); await entered.Task;
        Field<GridView>(page, "messagesView").FocusedRowHandle = 1; await page.LoadFocusedMessageAsync();
        release.SetResult(); await old;
        Assert.AreEqual("Terminabstimmung", Field<LabelControl>(page, "subjectLabel").Text); await page.StopAsync();
    });

    [TestMethod]
    [DataRow("folders")]
    [DataRow("messages")]
    [DataRow("preview")]
    public Task MailViewShutdownCancelsAndAwaitsNonCooperativeSources(string lane) => RunStaAsync(async () =>
    {
        var source = new MailSourceDouble();
        using var page = new MailPage(); page.Bind(source, null); CreateHandles(page); await page.ReloadAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); CancellationToken captured = default;
        async Task Block(CancellationToken token) { captured = token; entered.TrySetResult(); await release.Task; }
        Task pending;
        if (lane == "folders") { source.BeforeFolders = Block; pending = page.ReloadAsync(); }
        else if (lane == "messages") { source.List = async (folder, _, token) => { await Block(token); return source.Messages(folder); }; pending = page.LoadCurrentFolderAsync(); }
        else { source.Read = async (key, token) => { await Block(token); return source.Content(key); }; pending = page.LoadFocusedMessageAsync(); }
        await entered.Task; var closing = page.StopAsync();
        try { Assert.IsTrue(captured.IsCancellationRequested); Assert.IsFalse(closing.IsCompleted); Assert.IsFalse(Field<SimpleButton>(page, "reloadButton").Enabled); }
        finally { release.SetResult(); await closing; await pending; }
        Assert.IsFalse(Field<TreeList>(page, "folderTree").Enabled); Assert.IsFalse(Field<GridView>(page, "messagesView").GridControl.Enabled);
    });

    [TestMethod]
    public Task MailViewIsReachableOnceThroughTheShellAndShutdownStopsItsSource() => WithWorkspace(async (workspace, services, plugin) =>
    {
        var source = new MailSourceDouble();
        using var form = new MainForm(workspace.Application, workspace.Configuration, workspace, services.GetRequiredService<AppUiState>(),
            new LogWindowService(), new AudioCatalogDouble(), source);
        form.PrepareForBackgroundRun(); form.Show(); form.Navigate("mail");
        var page = (MailPage)form.GetActiveDocumentView()!; await page.ReloadAsync(); form.Navigate("mail"); Assert.AreSame(page, form.GetActiveDocumentView());
        Assert.IsNotNull(Field<DevExpress.XtraBars.BarButtonItem>(form, "mailItem").ImageOptions.SvgImage);
        await form.PrepareToCloseAsync(); Assert.IsFalse(Field<SimpleButton>(page, "reloadButton").Enabled);
    });

    [TestMethod]
    public Task MailViewUnconfiguredSourceShowsAnExplicitEmptyState() => RunStaAsync(async () =>
    {
        using var page = new MailPage(); page.Bind(new UnconfiguredMailViewSource(), null); CreateHandles(page); await page.ReloadAsync();
        Assert.Contains("Kein Postfach verbunden", Field<LabelControl>(page, "statusLabel").Text);
        Assert.AreEqual(0, Field<GridView>(page, "messagesView").DataRowCount); await page.StopAsync();
    });

    [TestMethod]
    public Task MailViewSourceIsRegisteredInTheActualApplicationCompositionRoot() => RunStaAsync(async () =>
    {
        var root = Path.Combine(Path.GetTempPath(), "WawiCallPro-mail-composition-" + Guid.NewGuid().ToString("N"));
        var previous = Environment.GetEnvironmentVariable("WAWICALLPRO_CONFIG_DIR");
        Environment.SetEnvironmentVariable("WAWICALLPRO_CONFIG_DIR", root);
        using var context = new WindowsFormsSynchronizationContext();
        using var exceptions = new AppExceptionBoundary(new MaintenanceCrashReporter());
        var host = AppCompositionRoot.Build(context, exceptions, new StartupExecutionMode(true), new MaintenanceInstanceGuard());
        try
        {
            await host.Services.GetRequiredService<WawiCallPro.Utilities.Configuration.ApplicationConfigurationService>().StartAsync(default);
            Assert.IsInstanceOfType<UnconfiguredMailViewSource>(host.Services.GetRequiredService<IMailViewSource>());
            using var form = ActivatorUtilities.CreateInstance<MainForm>(host.Services); form.PrepareForBackgroundRun();
            form.Show(); form.Navigate("mail");
            var page = (MailPage)form.GetActiveDocumentView()!; await page.ReloadAsync();
            Assert.Contains("Kein Postfach verbunden", Field<LabelControl>(page, "statusLabel").Text); await form.PrepareToCloseAsync();
        }
        finally
        {
            await ((IAsyncDisposable)host).DisposeAsync();
            Environment.SetEnvironmentVariable("WAWICALLPRO_CONFIG_DIR", previous);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    });

    [TestMethod]
    public Task MailViewHtmlImporterUsesTheBlockedResourceService() => RunStaAsync(async () =>
    {
        var source = new MailSourceDouble();
        source.Read = (key, _) => Task.FromResult<MailContent?>(source.Content(key) with
            { Html = "<html><body><p>Lesbarer Text</p><img src='file:///C:/blocked-mail-image.png'><img src='http://127.0.0.1:9/blocked.png'></body></html>" });
        using var page = new MailPage(); page.Bind(source, null); CreateHandles(page); await page.ReloadAsync();
        var editor = Field<RichEditControl>(page, "body"); Assert.Contains("Lesbarer Text", editor.Text);
        var service = (BlockedMailResources)editor.GetService<IUriStreamService>(); Assert.IsGreaterThan(0, service.BlockedRequests);
        await page.StopAsync();
    });

    [TestMethod]
    public Task MailViewCtrlFTogglesTheBuiltInFindPanelAndReadingPaneCanMoveBelow() => RunStaAsync(async () =>
    {
        using var page = new MailPage { Size = new(1400, 720) }; page.Bind(new MailSourceDouble(), null);
        using var host = new MainForm(); host.PrepareForBackgroundRun(); host.Controls.Add(page); host.Show(); CreateHandles(page); await page.ReloadAsync();
        var grid = Field<GridView>(page, "messagesView");
        var handler = typeof(MailPage).GetMethod("ProcessCmdKey", BindingFlags.Instance | BindingFlags.NonPublic)!;
        object[] arguments = [Message.Create(page.Handle, 0, IntPtr.Zero, IntPtr.Zero), Keys.Control | Keys.F];
        Assert.AreEqual(true, handler.Invoke(page, arguments)); Assert.IsTrue(grid.IsFindPanelVisible);
        Assert.AreEqual(true, handler.Invoke(page, arguments)); Assert.IsFalse(grid.IsFindPanelVisible);
        Field<SimpleButton>(page, "readingPositionButton").PerformClick();
        Assert.IsFalse(Field<SplitContainerControl>(page, "contentSplit").Horizontal); await page.StopAsync();
    });

    [TestMethod]
    public Task MailViewRendersRepresentativeContentAndEmptyStatesWithoutVisibleWindows() => RunStaAsync(async () =>
    {
        AppUiStartup.Apply();
        var originalFont = WindowsFormsSettings.DefaultFont; var originalAppearance = AppearanceObject.DefaultFont;
        var originalSkin = DevExpress.LookAndFeel.UserLookAndFeel.Default.ActiveSkinName;
        var originalPalette = DevExpress.LookAndFeel.UserLookAndFeel.Default.ActiveSvgPaletteName;
        var line = ApplicationDescriptor.FromAssembly(typeof(MainForm).Assembly).ProductLine;
        var output = Path.Combine(".artifacts", "mail-view", line); Directory.CreateDirectory(output);
        try
        {
            foreach (var state in new[] { "filled", "empty", "failure" })
            foreach (var (width, fontSize, scale, skin) in new[] { (1240, 10F, 1F, "Basic"), (760, 16F, 1F, "Office 2019 Black"), (1240, 10F, 1.5F, "Basic") })
            {
                if (skin == "Basic") DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(skin, "Blue Dark");
                else DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(skin);
                using var font = new Font("Segoe UI", fontSize); WindowsFormsSettings.DefaultFont = AppearanceObject.DefaultFont = font;
                var source = new MailSourceDouble();
                if (state == "empty") source.Catalog = new([], "Kein Postfach verbunden.");
                if (state == "failure") source.List = (_, _, _) => throw new InvalidOperationException("private-source-error");
                using var page = new MailPage { Font = font, Size = new(width, 720) }; page.Bind(source, null);
                using var host = new MainForm(); host.PrepareForBackgroundRun(); host.Font = font; host.MinimumSize = Size.Empty;
                host.Controls.Add(page); page.BringToFront(); host.ClientSize = page.Size; host.Show(); page.Size = new(width, 720);
                CreateHandles(page); if (scale != 1F) page.Scale(new SizeF(scale, scale)); await page.ReloadAsync();
                page.PerformLayout(); Application.DoEvents(); CheckTableBounds(page);
                Assert.AreEqual(0D, host.Opacity); Assert.IsFalse(host.ShowInTaskbar);
                Assert.IsTrue(Field<SimpleButton>(page, "reloadButton").Bounds.Right <= Field<TableLayoutPanel>(page, "toolbar").ClientSize.Width);
                using var shot = WawiCallPro.App.Diagnostics.UiAudit.CaptureMail(page);
                shot.Save(Path.Combine(output, $"mail-{state}-{width}-{fontSize:0}-{scale:0.0}-{skin}.png"), ImageFormat.Png);
                await page.StopAsync();
            }
        }
        finally
        {
            WindowsFormsSettings.DefaultFont = originalFont; AppearanceObject.DefaultFont = originalAppearance;
            if (string.IsNullOrEmpty(originalPalette)) DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(originalSkin);
            else DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(originalSkin, originalPalette);
        }
    });

    private sealed class MailSourceDouble : IMailViewSource
    {
        internal readonly MailFolderKey Root = new("Fixture", "account", "root");
        internal readonly MailFolderKey Inbox = new("Fixture", "account", "inbox");
        internal readonly MailFolderKey Sent = new("Fixture", "account", "sent");
        internal MailFolderCatalog? Catalog;
        internal Func<CancellationToken, Task>? BeforeFolders;
        internal Func<MailFolderKey, string, CancellationToken, Task<ImmutableArray<MailListItem>>>? List;
        internal Func<MailMessageKey, CancellationToken, Task<MailContent?>>? Read;
        internal string LastSearch = "";
        internal int Writes => 0;
        public async Task<MailFolderCatalog> GetFoldersAsync(CancellationToken cancellationToken = default)
        {
            if (BeforeFolders is not null) await BeforeFolders(cancellationToken);
            return Catalog ?? new([new(Root, null, "Testpostfach"), new(Inbox, Root, "Posteingang", 2), new(Sent, Root, "Gesendet")], "");
        }
        public Task<ImmutableArray<MailListItem>> GetMessagesAsync(MailFolderKey folder, string search, CancellationToken cancellationToken = default)
        { LastSearch = search; return List?.Invoke(folder, search, cancellationToken) ?? Task.FromResult(Messages(folder)); }
        internal ImmutableArray<MailListItem> Messages(MailFolderKey folder) => folder == Sent
            ? [new(new("Fixture", "account", "3"), Sent, "Finanzbüro · info@example.test", "Ihre Unterlagen", DateTimeOffset.Now, "Anbei erhalten Sie die besprochenen Unterlagen.", true, 0)]
            : [new(new("Fixture", "account", "1"), Inbox, "Kundin Erika Muster · kunde@example.test", "Rückfrage zum Angebot", DateTimeOffset.Now, "Guten Tag, vielen Dank für Ihr Angebot. Können wir die Liefertermine noch besprechen?", false, 1),
               new(new("Fixture", "account", "2"), Inbox, "Max Beispiel · max@example.test", "Terminabstimmung", DateTimeOffset.Now.AddHours(-2), "Unser nächster Termin findet am Dienstag statt.", true, 0)];
        public Task<MailContent?> GetMessageAsync(MailMessageKey message, CancellationToken cancellationToken = default) =>
            Read?.Invoke(message, cancellationToken) ?? Task.FromResult<MailContent?>(Content(message));
        internal MailContent Content(MailMessageKey key) => new(key, key.Id == "1" ? "Rückfrage zum Angebot" : key.Id == "2" ? "Terminabstimmung" : "Ihre Unterlagen",
            "Erika Muster <kunde@example.test>", "Finanzbüro <info@example.test>", "", DateTimeOffset.Now,
            "Guten Tag, vielen Dank für Ihr Angebot. Können wir die Liefertermine noch besprechen?",
            "<html><body><h2>Guten Tag,</h2><p>vielen Dank für Ihr Angebot. Können wir die <b>Liefertermine</b> noch besprechen?</p><p>Mit freundlichen Grüßen<br>Erika Muster</p></body></html>", [new("Angebot.pdf", 12345)]);
    }
}

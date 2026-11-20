using DevExpress.XtraEditors;
using WawiCallPro.App.Hosting;
using WawiCallPro.App.Views;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    public Task WaitCursorRestoresStateIfItsConstructorCannotApplyTheCursor() => RunStaAsync(() =>
    {
        using var owner = new CursorFailureControl { Cursor = Cursors.Cross };
        owner.FailNextSet = true;
        var rejected = false;
        try { using var failed = new UiWaitCursor(owner); }
        catch (InvalidOperationException) { rejected = true; }
        Assert.IsTrue(rejected); Assert.IsFalse(owner.UseWaitCursor); Assert.AreEqual(Cursors.Cross, owner.Cursor);
        using (new UiWaitCursor(owner)) { Assert.IsTrue(owner.UseWaitCursor); }
        Assert.IsFalse(owner.UseWaitCursor); Assert.AreEqual(Cursors.Cross, owner.Cursor);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task WaitCursorRestoresOriginalStateAfterOverlappingAndRepeatedDisposal() => RunStaAsync(() =>
    {
        using var form = new XtraForm { Cursor = Cursors.Cross };
        using var child = new XtraUserControl(); form.Controls.Add(child);
        var first = new UiWaitCursor(child);
        var second = new UiWaitCursor(form);
        Assert.IsTrue(form.UseWaitCursor); Assert.AreEqual(Cursors.WaitCursor, form.Cursor);
        first.Dispose(); first.Dispose();
        Assert.IsTrue(form.UseWaitCursor, "The other operation still owns a lease.");
        second.Dispose();
        Assert.IsFalse(form.UseWaitCursor); Assert.AreEqual(Cursors.Cross, form.Cursor);
        second.Dispose();
        Assert.AreEqual(Cursors.Cross, form.Cursor);
        form.UseWaitCursor = true;
        using (new UiWaitCursor(child)) { }
        Assert.IsTrue(form.UseWaitCursor, "An existing busy state must be preserved.");
        form.UseWaitCursor = false;
        using var disposed = new XtraForm();
        var finishing = new UiWaitCursor(disposed); disposed.Dispose(); finishing.Dispose(); finishing.Dispose();
        return Task.CompletedTask;
    });

    [TestMethod]
    [DataRow("success")]
    [DataRow("failure")]
    [DataRow("cancel")]
    public Task PageOperationsKeepTheWaitCursorUntilSuccessFailureOrCancellationCompletes(string outcome) => RunStaAsync(async () =>
    {
        using var owner = new XtraForm { Cursor = Cursors.Cross };
        using var page = new XtraUserControl(); owner.Controls.Add(page);
        using var status = new LabelControl(); page.Controls.Add(status);
        using var operation = new PageOperation(page, status);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var running = operation.RunAsync(async token =>
        {
            Assert.IsTrue(owner.UseWaitCursor);
            entered.TrySetResult(); await release.Task.WaitAsync(token);
            if (outcome == "failure") throw new InvalidOperationException("Synthetic operation failure");
        });
        await entered.Task;
        Assert.IsTrue(owner.UseWaitCursor);
        if (outcome == "cancel") await operation.StopAsync();
        else { release.TrySetResult(); await running; }
        Assert.IsFalse(owner.UseWaitCursor); Assert.AreEqual(Cursors.Cross, owner.Cursor);
        Assert.IsFalse(operation.IsRunning);
    });

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task DatabaseDialogPreparationUsesRaiiAndRestoresCursorOnRejectionOrFailure(bool fail) => WithWorkspace(async (workspace, _, _) =>
    {
        using var form = new FrmSettings(workspace) { Cursor = Cursors.Cross };
        form.PrepareForBackgroundRun(); CreateHandles(form);
        var page = Field<SettingsPage>(form, "settingsPage");
        var visited = false;
        await page.SetupDatabaseAsync(dialog =>
        {
            visited = true; Assert.IsTrue(form.UseWaitCursor);
            if (fail) throw new InvalidOperationException("Synthetic dialog failure");
            return Task.FromResult(DialogResult.Cancel);
        });
        Assert.IsTrue(visited); Assert.IsFalse(form.UseWaitCursor); Assert.AreEqual(Cursors.Cross, form.Cursor);
        Assert.IsTrue(Field<SimpleButton>(page, "databaseSetupButton").Enabled);
        await form.StopAsync();
    });

    private sealed class CursorFailureControl : Control
    {
        internal bool FailNextSet;
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override Cursor Cursor
        {
            get => base.Cursor;
            set
            {
                if (FailNextSet) { FailNextSet = false; throw new InvalidOperationException("Synthetic cursor failure"); }
                base.Cursor = value;
            }
        }
    }
}

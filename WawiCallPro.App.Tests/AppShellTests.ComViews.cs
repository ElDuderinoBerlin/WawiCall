using System.Collections.Immutable;
using DevExpress.XtraEditors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WawiCallPro.App.Hosting;
using WawiCallPro.App.Views;
using WawiCallPro.Automation;
using WawiCallPro.Core.Directory;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Plugin.Windows;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    public Task AutomationDatabaseModalClosesAfterOneRequest() => RunStaAsync(() =>
    {
        using var owner = new MainForm(); owner.PrepareForBackgroundRun(); owner.Show();
        using var dialog = new FrmDatabaseSetup(); dialog.PrepareForBackgroundRun();
        using var timeout = new System.Windows.Forms.Timer { Interval = 2000 };
        var repeated = false;
        timeout.Tick += (_, _) => { repeated = true; dialog.Close(); };
        dialog.Shown += (_, _) => { timeout.Start(); dialog.BeginInvoke(new Action(dialog.Close)); };
        dialog.ShowDialog(owner); timeout.Stop();
        Assert.IsFalse(repeated, "The first close must finish after the pending FormClosing handler returns.");
        return Task.CompletedTask;
    });
}

using System.Drawing.Imaging;
using System.Net.Sockets;
using System.Reflection;
using DevExpress.Utils;
using DevExpress.XtraEditors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WawiCallPro.App.Diagnostics;
using WawiCallPro.App.Hosting;
using WawiCallPro.App.Views;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Diagnostics;
using WawiCallPro.Utilities.Operations;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    public Task LogHoverReadsTheMatchingFileRecordAndKeepsTheListShort() => RunStaAsync(() =>
    {
        var root = Path.Combine(Path.GetTempPath(), "Wcp-LogHover-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new ApplicationPaths(new ConfigurationLocation(root));
            using var file = new SafeFileLoggerProvider(paths, new RuntimeFailureState());
            var logs = new LogWindowService(file);
            const string summary = "Die Gesprächsaufnahme konnte nicht abgespielt werden. "
                + "Diese ausführliche Erläuterung für den Fehler soll nur beim Überfahren sichtbar sein.";
            logs.LogWarning(summary, Capture(new IOException("private-file-and-password", new SocketException((int)SocketError.ConnectionReset))));
            logs.LogWarning(summary, Capture(new UnauthorizedAccessException("private-token")));
            using var host = new Form { Opacity = 0, ShowInTaskbar = false, ClientSize = new Size(840, 220) };
            using var panel = new LogPanel { Dock = DockStyle.Fill };
            host.Controls.Add(panel);
            host.Show();
            panel.Bind(logs);
            var list = Field<ListBoxControl>(panel, "listLog");
            list.TopIndex = 0;
            Application.DoEvents();

            var first = FieldForRow(0);
            var second = FieldForRow(1);
            Assert.AreEqual(list.Items[0].ToString()!.Split(':', 4)[3], list.Items[1].ToString()!.Split(':', 4)[3]);
            Assert.DoesNotContain("ausführliche Erläuterung", list.Items[0].ToString()!);
            Assert.Contains("ausführliche Erläuterung", first);
            Assert.Contains(nameof(IOException), first);
            Assert.Contains(nameof(SocketError.ConnectionReset), first);
            Assert.Contains(nameof(UnauthorizedAccessException), second);
            Assert.DoesNotContain(nameof(SocketError.ConnectionReset), second);
            var fileText = File.ReadAllText(Directory.GetFiles(paths.LogsDirectory, "*.jsonl").Single());
            Assert.DoesNotContain("private-", fileText);
            Assert.Contains(nameof(SocketError.ConnectionReset), fileText);
            Assert.Contains(nameof(Capture), first);

            var folder = Path.Combine(RenderOutput(), "log-details");
            Directory.CreateDirectory(folder);
            using var image = UiAudit.Capture(host);
            image.Save(Path.Combine(folder, "short-warnings.png"), ImageFormat.Png);

            string FieldForRow(int index)
            {
                var rectangle = list.GetItemRectangle(index);
                var args = new ToolTipControllerGetActiveObjectInfoEventArgs(list, null, null, new Point(rectangle.Left + 10, rectangle.Top + 2));
                typeof(LogListBoxToolTipHelper).GetMethod("SetToolTipInfo", BindingFlags.Static | BindingFlags.NonPublic)!
                    .Invoke(null, [list, args]);
                Assert.IsNotNull(args.Info);
                Assert.AreEqual(DefaultBoolean.False, args.Info.SuperTip.AllowHtmlText);
                return args.Info.Text;
            }
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task FileAndUiUseOneProviderWithAnExactMatchForIdenticalWarnings() => RunStaAsync(async () =>
    {
        var profile = Directory.CreateTempSubdirectory("Wcp-LogProvider-");
        try
        {
            var product = profile.CreateSubdirectory("product");
            await File.WriteAllTextAsync(Path.Combine(product.FullName, "appsettings.json"), """
                {"Serilog":{"WriteTo":[{"Name":"File","Args":{"path":"logs/WawiCallPro-.log"}}]}}
                """);
            using var context = new WindowsFormsSynchronizationContext();
            using var exceptions = new AppExceptionBoundary(new MaintenanceCrashReporter());
            var mode = new StartupExecutionMode(SuppressNetwork: true)
                { IsolatedProfile = true, DocumentationProfileDirectory = profile.FullName };
            var host = AppCompositionRoot.Build(context, exceptions, mode, new MaintenanceInstanceGuard(),
                null, null, services => services.AddSingleton(new ConfigurationLocation(profile.FullName, product.FullName)));
            await using var lifetime = (IAsyncDisposable)host;
            var paths = host.Services.GetRequiredService<ApplicationPaths>();
            await host.Services.GetRequiredService<ApplicationConfigurationService>().StartAsync(default);
            var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(AppShellTests).FullName!);
            OperationEvents.Write(logger, OperationEvent.LiveRecordingInputFailed, Capture(new IOException("not for the file")));
            OperationEvents.Write(logger, OperationEvent.LiveRecordingInputFailed, Capture(new TimeoutException("private")));
            var rows = host.Services.GetRequiredService<ILogWindowService>().Drain();
            Assert.HasCount(2, rows);
            Assert.AreEqual(rows[0].Message, rows[1].Message);
            Assert.Contains(nameof(IOException), rows[0].FileRecord!.ReadDetails()!);
            Assert.Contains(nameof(TimeoutException), rows[1].FileRecord!.ReadDetails()!);
            var path = Directory.GetFiles(paths.LogsDirectory, "*.jsonl").Single();
            Assert.HasCount(2, File.ReadAllLines(path));
            var text = await File.ReadAllTextAsync(Directory.GetFiles(paths.LogsDirectory, "*.log").Single());
            Assert.Contains(nameof(IOException), text);
            Assert.Contains(nameof(TimeoutException), text);
            Assert.DoesNotContain("not for the file", text);
            File.Delete(path);
            Assert.IsNull(rows[0].FileRecord!.ReadDetails());
        }
        finally { profile.Delete(true); }
    });

    private static Exception Capture(Exception exception)
    {
        try { throw exception; }
        catch (Exception error) { return error; }
    }
}

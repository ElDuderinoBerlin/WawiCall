using Microsoft.Extensions.DependencyInjection;
using System.Text;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Hosting;
using WawiCallPro.Utilities.Licensing;
using WawiCallPro.Utilities.Operations;

namespace WawiCallPro.App.Hosting;

internal static class AppLicenseMaintenance
{
    internal static int Run(AppStartArguments command)
    {
        try
        {
            Console.OutputEncoding = Encoding.UTF8;
            var application = ApplicationDescriptor.FromAssembly(typeof(MainForm).Assembly);
            var location = ConfigurationLocation.ForCurrentUser(application.ProductLine);
            var paths = new ApplicationPaths(location);
            using var instance = new AppInstanceGuard(application, paths);
            if (!instance.TryAcquire())
            {
                Console.Error.WriteLine("WawiCallPro zuerst beenden, bevor die Lizenz geändert wird.");
                return 1;
            }

            var services = new ServiceCollection();
            services.AddSingleton(location);
            services.AddSingleton(paths);
            services.AddSingleton(TimeProvider.System);
            services.AddProductLicensing();
            using var provider = services.BuildServiceProvider();
            var maintenance = provider.GetRequiredService<IProductLicenseMaintenance>();
            if (command.Kind == AppStartKind.ImportLicense)
            {
                maintenance.ImportOfflineAsync(command.Path!, CancellationToken.None).GetAwaiter().GetResult();
                Console.WriteLine("Die signierte Offline-Lizenz wurde übernommen.");
            }
            else
            {
                maintenance.ResetAsync(CancellationToken.None).GetAwaiter().GetResult();
                Console.WriteLine("Die lokale Lizenz wurde zurückgesetzt.");
            }
            return 0;
        }
        catch (LicenseAccessException error)
        {
            Console.Error.WriteLine(StartupFailureMessage.Format(error));
            return 1;
        }
        catch (Exception)
        {
            Console.Error.WriteLine("Die Lizenzdatei konnte nicht gelesen oder die lokale Lizenz nicht geändert werden.");
            return 1;
        }
    }
}

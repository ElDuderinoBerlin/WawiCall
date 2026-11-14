using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WawiCallPro.Core.Companies;
using WawiCallPro.Core.PhoneNumbers;
using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Interfaces.Plugins;
using WawiCallPro.Interfaces.PhoneNumbers;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Security;
using WawiCallPro.Utilities.Operations;
using Microsoft.Extensions.Logging;
using WawiCallPro.Database;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Database.Repositories;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Utilities.Persistence;
using WawiCallPro.Utilities.Plugins;

namespace WawiCallPro.Utilities.Hosting;

/// <summary>Shared registrations; each executable supplies its dispatcher and presentation services.</summary>
public static class WawiCallHost
{
    public static IHostBuilder CreateBuilder(Assembly entryAssembly)
    {
        ArgumentNullException.ThrowIfNull(entryAssembly);
        // Only the safe provider is registered; arbitrary formatted state never becomes a log record.
        return new HostBuilder()
            .UseContentRoot(AppContext.BaseDirectory)
            .UseDefaultServiceProvider(options =>
            {
                options.ValidateScopes = true;
                options.ValidateOnBuild = true;
            })
            .ConfigureServices(services =>
            {
                services.Configure<HostOptions>(options =>
                {
                    options.StartupTimeout = TimeSpan.FromSeconds(30);
                    options.ShutdownTimeout = TimeSpan.FromSeconds(30);
                });
                services.AddSingleton(ApplicationDescriptor.FromAssembly(entryAssembly));
                services.AddSingleton(provider => ConfigurationLocation.ForCurrentUser(provider.GetRequiredService<ApplicationDescriptor>().ProductLine));
                services.AddSingleton<ApplicationPaths>();
                services.AddSingleton<RuntimeFailureState>();
                services.AddSingleton<RuntimeLoggingSettings>();
                services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Trace));
                services.AddSingleton<ILoggerProvider, SafeFileLoggerProvider>();
                services.AddSingleton<Quantarix.Plugin.Abstractions.ILogger, QuantarixLogAdapter>();
                services.AddSingleton<IPluginManagerFactory, QuantarixPluginManagerFactory>();
                services.AddSingleton<ISharedPluginContracts, SharedPluginContracts>();
                services.AddSingleton<IWawiCallHostServices, WawiCallHostServices>();
                services.AddSingleton<IPluginSessionFactory, PluginSessionFactory>();
                services.AddSingleton<IPluginTrust, PluginTrust>();
                services.AddHostedService<RuntimeFailureMonitor>();
                services.AddSingleton<ISecretProtector, WindowsUserSecretProtector>();
                services.AddSingleton<ApplicationOptionsValidator>();
                services.AddSingleton<IPhoneNumberParser, LibPhoneNumberParser>();
                services.AddSingleton<IPhoneNumberInterpreter, PhoneNumberInterpreter>();
                services.AddSingleton<IPhoneCacheMetadata, PhoneCacheMetadata>();
                services.AddSingleton<ICompanyPhoneMatchIndexFactory, CompanyPhoneMatchIndexFactory>();
                services.AddSingleton<ICallerMatchDecisionService, CallerMatchDecisionService>();
                services.AddSingleton<ICallViewScopeFactory, CallViewScopeFactory>();
                services.AddSingleton<ICompanyPhoneMatchingService, CompanyPhoneMatchingService>();
                services.AddSingleton<ICompanyPhoneNumberContextProvider, CompanyPhoneNumberContextProvider>();
                services.AddSingleton<ConfigurationStore>();
                services.AddSingleton<PhoneSearchChanges>();
                services.AddSingleton<PhoneCacheReindexer>();
                services.AddSingleton<ApplicationConfigurationService>();
                services.AddSingleton<IPluginConnectionConfiguration, PluginConnectionConfiguration>();
                services.AddHostedService(provider => provider.GetRequiredService<ApplicationConfigurationService>());
                services.AddSingleton(TimeProvider.System);
                services.AddSingleton<DatabaseFactory>();
                services.AddSingleton<ICommunicationDatabaseFactory, CommunicationDatabaseFactory>();
                services.AddSingleton<IDatabaseSetupService, DatabaseSetupService>();
                services.AddSingleton<IDatabaseTargetSource, ConfiguredDatabaseTarget>();
                services.AddSingleton<IDatabaseMigrationEngineFactory, DatabaseMigrationEngineFactory>();
                services.AddSingleton<IDatabaseMigrationLock, DatabaseMigrationLock>();
                services.AddSingleton<IDatabaseMigrationPermissions, DatabaseMigrationPermissions>();
                services.AddSingleton<DatabaseRepositoryContext>();
                services.AddSingleton<ICompanyRepository, DapperCompanyRepository>();
                services.AddSingleton<DapperPartyCacheRepository>();
                services.AddSingleton<IPartyCacheRepository, PhoneIndexCacheRepository>();
                services.AddSingleton<ICallRepository, DapperCallRepository>();
                services.AddSingleton<ICallNoteRepository, DapperCallNoteRepository>();
                services.AddSingleton<IRecordingRepository, DapperRecordingRepository>();
                services.AddSingleton<INumberParseLogRepository, DapperNumberParseLogRepository>();
                services.AddSingleton<IPhoneNumberDiagnostics, PhoneNumberDiagnostics>();
                services.AddSingleton<DapperPhoneMatchingRepository>();
                services.AddSingleton<IPhoneMatchingRepository, PhoneIndexRuleRepository>();
                services.AddSingleton<ISqliteDatabaseBackup, SqliteDatabaseBackup>();
                services.AddSingleton<IWorkstationIdentity, WorkstationIdentity>();
                services.AddSingleton<ICallOriginProvider, WindowsCallOriginProvider>();
                services.AddSingleton<ICallJournal, DatabaseCallJournal>();
                services.AddSingleton<ICallHistoryService, DatabaseCallHistory>();
                services.AddSingleton<ICallAssociationService, DatabaseCallAssociation>();
                services.AddSingleton<ICommunicationDatabase, CommunicationDatabase>();
                services.AddSingleton<CompanyContext>();
                services.AddSingleton<ICompanyContext>(provider => provider.GetRequiredService<CompanyContext>());
                services.AddHostedService<CompanyContextLifetime>();
                services.AddSingleton<HostDiagnostics>();
            });
    }
}

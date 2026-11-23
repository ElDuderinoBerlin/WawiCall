using System.Collections.Immutable;
using DevExpress.XtraEditors;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Interfaces.Plugins;

namespace WawiCallPro.Plugin.Windows;

/// <summary>Optional desktop capability. The same session-bound instance is injected and passed to Configure.</summary>
public interface IWindowsPluginHostServices : IWawiCallHostServices
{
    IPluginHostUi Ui { get; }
}

/// <summary>Registers metadata during plugin configuration without constructing controls or invoking the factory.</summary>
public interface IPluginHostUi
{
    IPluginSettingsRegistration RegisterSettingsTab(PluginSettingsTabDescriptor descriptor, IPluginSettingsPageFactory factory);
}

public sealed record PluginSettingsTabDescriptor(string Id, string Title, int Order);

/// <summary>Revocation prevents new acquisitions and waits for outstanding factory leases.</summary>
public interface IPluginSettingsRegistration : IPluginLifetimeResource { }

/// <summary>Called on the UI thread under the runtime resource gate. Create performs no source IO.</summary>
public interface IPluginSettingsPageFactory
{
    IPluginSettingsPage Create(IPluginSettingsPageContext context);
}

/// <summary>One dialog-owned view. Load runs after the creation callback; stop precedes UI-thread disposal.</summary>
public interface IPluginSettingsPage : IPluginLifetimeResource
{
    Control View { get; }
    bool HasUnsavedChanges { get; }
    Task LoadAsync(CancellationToken cancellationToken = default);
    Task ValidateAsync(CancellationToken cancellationToken = default);
    void StageChanges();
    Task StopAsync(CancellationToken cancellationToken = default);
}

/// <summary>Borrowed dialog draft and presentation helpers; provides no container or persistence access.</summary>
public interface IPluginSettingsPageContext
{
    IPluginConnectionEditContext Connection { get; }
    IPluginCompanySelectionDraft Companies { get; }
    IPluginSettingsPresentation Presentation { get; }
}

/// <summary>Optional source authorization bound to the dialog's plugin generation; never exposes the host container.</summary>
public interface IPluginSettingsSourceAccess
{
    Task EnsureSourceAccessAsync(CancellationToken cancellationToken = default);
}

/// <summary>Selection changes affect only the dialog draft, never the live company context.</summary>
public interface IPluginCompanySelectionDraft
{
    CompanyKey? SelectedCompany { get; }
    void StageSelection(CompanyKey? company);
    IReadOnlyList<Company> SelectedCompanies => Array.Empty<Company>();
    void StageSelections(IReadOnlyList<Company> companies) => throw new NotSupportedException();
}

/// <summary>Host-side publication after a successful save and current plugin authorization.</summary>
public interface IPluginCompanySelectionPublisher
{
    Task PublishAsync(CompanyKey? company, CancellationToken cancellationToken = default);
}

public enum PluginSettingsNotice { Loading, Loaded, Empty, Unavailable, Failed, SelectionChanged }

/// <summary>Closed failure vocabulary. Never carries source text, credentials or response bodies.</summary>
public enum PluginCompanyFailure
{
    Unknown, ConfigurationRequired, SourceAccess, InstallationUnavailable, AuthenticationFailed,
    ClientKeyMissing, ClientKeyUnknown, ClientKeyUnauthorized, PermissionDenied, ClientConfigurationExpired,
    ClientConfigurationMissing, ClientConfigurationInvalid, ApiUnavailable, Timeout, InvalidResponse, CompanyNames
}

/// <summary>Optional plugin classification of a source exception without exposing its message.</summary>
public interface IPluginCompanyFailure
{
    PluginCompanyFailure CompanyFailure { get; }
}

public interface IPluginSettingsPresentation
{
    void ReportCompanyFailure(PluginCompanyFailure failure) => Report(PluginSettingsNotice.Failed);
    void Report(PluginSettingsNotice notice) { }
    void RestoreCompanyLayout(DevExpress.XtraGrid.Views.Grid.GridView view) { }
    void SaveCompanyLayout(DevExpress.XtraGrid.Views.Grid.GridView view) { }
    IDisposable BeginWait(Control owner);
    void SizeButtons(params SimpleButton[] buttons);
}

/// <summary>Opaque generation and local tab identity. A key from a replaced session is never reusable.</summary>
public readonly record struct PluginSettingsTabKey(Guid SessionId, string Id);

/// <summary>Detached metadata only; safe to enumerate without retaining a plugin factory or creating UI.</summary>
public sealed record RegisteredPluginSettingsTab(PluginSettingsTabKey Key, string PluginId, PluginSettingsTabDescriptor Descriptor);

public sealed record PluginSettingsCatalogSnapshot(long Revision, ImmutableArray<RegisteredPluginSettingsTab> Tabs);

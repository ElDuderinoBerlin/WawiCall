using WawiCallPro.Interfaces.Plugins;

namespace WawiCallPro.Plugin.Windows;

/// <summary>Optional plugin-owned editor, requested only by the desktop settings dialog.</summary>
public interface IWawiCallPluginSettings
{
    bool HasConnectionSettings => true;
    string ConnectionSettingsTitle => "Zugang";
    IPluginSettingsView CreateConnectionSettings(IPluginConnectionEditContext context);
}

public interface IPluginSettingsView : IPluginViewLease
{
    bool HasUnsavedChanges { get; }
    void Reload(IPluginConnectionEditContext context);
    void ApplyChanges(IPluginConnectionEditContext context);
    Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    Task ValidateAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

/// <summary>The shared runtime owns the handle; the dialog releases its editor before switching plugins.</summary>
public interface IPluginSettingsHandle : IPluginViewHandle
{
    string Title { get; }
    void Reload(IPluginConnectionEditContext context);
    void ApplyChanges(IPluginConnectionEditContext context);
    Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    Task ValidateAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

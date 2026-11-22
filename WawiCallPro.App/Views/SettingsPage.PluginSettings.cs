using WawiCallPro.Interfaces.Plugins;
using WawiCallPro.Plugin.Windows;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.App.Views;

public partial class SettingsPage
{
    private IPluginSettingsHandle? _pluginSettings;
    private Control? _pluginSettingsControl;
    private PluginConnectionEditContext? _pluginEditContext;
    private Hosting.PluginSettingsPageContext? _pluginPageContext;
    private string? _pluginSettingsId;
    private bool _pluginSettingsLoaded;
    private bool _selectPluginConnectionOnLoad;

    private static bool PluginConnectionSettingsDiffer(ErpConnectionSettings left, ErpConnectionSettings right) =>
        !System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(left).AsSpan().SequenceEqual(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(right));

    private void ApplyPluginConnectionSnapshot(ConfigurationSnapshot snapshot)
    {
        if (_workspace is null) return;
        _pluginEditContext = _workspace.Settings.BeginPluginConnectionEdit(snapshot, _workspace.Secrets);
        _pluginPageContext = new(_pluginEditContext, _workspace, snapshot.Options.User.PreferredCompany);
        if (_pluginSettings is not null && _pluginSettingsId == (_workspace.Plugins as IProductPluginSelection)?.ActivePluginId)
            _pluginSettings.Reload(_pluginPageContext);
    }

    private ApplicationOptions ApplyPluginConnectionDraft(ApplicationOptions options)
    {
        if (_pluginEditContext is null) return options;
        _pluginSettings?.ApplyChanges(_pluginPageContext ?? (IPluginConnectionEditContext)_pluginEditContext);
        return _pluginPageContext?.ApplyTo(options) ?? _pluginEditContext.ApplyTo(options);
    }

    private void RequireCleanPluginDraft()
    {
        if (_pluginSettings?.HasUnsavedChanges == true || _pluginPageContext?.HasSelectionChanges == true)
            throw new PluginSettingsValidationException(PluginSettingsIssue.UnsavedChanges);
    }

    private async Task LoadPluginConnectionEditorAsync(CancellationToken token)
    {
        if (_workspace is null || _pluginEditContext is null) return;
        var id = (_workspace.Plugins as IProductPluginSelection)?.ActivePluginId;
        if (_pluginSettingsLoaded && id == _pluginSettingsId) return;
        await ClosePluginConnectionEditorAsync();
        token.ThrowIfCancellationRequested();
        wawiHintLabel.Visible = true;
        wawiHintLabel.Text = "Für das aktive Plugin ist hier kein Zugangseditor verfügbar. Plugin-Auswahl prüfen.";
        if (!_workspace.CanUseBusinessDirectory) return;
        IPluginSettingsHandle? editor = null;
        try
        {
            editor = await _workspace.Views.OpenSettingsAsync(_pluginPageContext ?? (IPluginConnectionEditContext)_pluginEditContext, token);
            token.ThrowIfCancellationRequested();
            if (id != (_workspace.Plugins as IProductPluginSelection)?.ActivePluginId) return;
            _pluginSettingsLoaded = true; _pluginSettingsId = id;
            if (editor is null) return;
            _pluginSettingsControl = editor.View;
            _pluginSettingsControl.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            _pluginSettingsControl.Margin = Padding.Empty;
            wawiLayout.Controls.Add(_pluginSettingsControl, 0, 1);
            wawiTab.Text = editor.Title;
            tabs.TabPages.Insert(2, wawiTab);
            _pluginSettings = editor; editor = null;
            wawiHintLabel.Visible = false;
            if (_selectPluginConnectionOnLoad) { tabs.SelectedTabPage = wawiTab; _selectPluginConnectionOnLoad = false; }
            await _pluginSettings.LoadAsync(token);
            ContentSizeChanged?.Invoke(this, EventArgs.Empty);
        }
        finally { if (editor is not null) await editor.DisposeAsync(); }
    }

    private async Task ClosePluginConnectionEditorAsync()
    {
        var editor = _pluginSettings;
        _pluginSettings = null; _pluginSettingsLoaded = false; _pluginSettingsId = null;
        tabs.TabPages.Remove(wawiTab);
        if (_pluginSettingsControl is not null)
        {
            wawiLayout.Controls.Remove(_pluginSettingsControl);
            _pluginSettingsControl = null;
        }
        if (editor is not null) { await editor.StopAsync(); await editor.DisposeAsync(); }
    }
}

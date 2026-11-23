using DevExpress.XtraEditors;
using System.IO;
using WawiCallPro.App.Hosting;
using WawiCallPro.Utilities.Plugins;

namespace WawiCallPro.App.Views;

public partial class FunctionPluginsPage : XtraUserControl
{
    private IFunctionPluginRuntime? _runtime;
    private PageOperation? _operation;
    private bool _busy;
    private bool _stopping;

    public FunctionPluginsPage()
    {
        InitializeComponent();
        UiButtonSizing.Attach(this);
        UiButtonSizing.Apply(toggleButton, executeButton);
        UpdateActions();
    }

    internal void Bind(IFunctionPluginRuntime runtime)
    {
        _runtime = runtime;
        _operation = new PageOperation(this, status);
    }

    internal async Task LoadAsync(CancellationToken token)
    {
        if (_runtime is null) return;
        var selected = (plugins.SelectedItem as Row)?.Plugin.Id;
        IReadOnlyList<FunctionPluginChoice> catalog;
        try { catalog = await _runtime.ListAsync(token); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or FunctionPluginException)
        {
            plugins.DataSource = null;
            status.Text = "Funktionsplugins konnten nicht geladen werden. Bitte den Plugin-Ordner prüfen.";
            UpdateActions();
            return;
        }
        token.ThrowIfCancellationRequested();
        var rows = catalog.Select(plugin => new Row(plugin)).ToArray();
        plugins.DataSource = rows;
        plugins.SelectedIndex = Array.FindIndex(rows, row => row.Plugin.Id == selected);
        if (plugins.SelectedIndex < 0 && rows.Length != 0) plugins.SelectedIndex = 0;
        status.Text = rows.Length == 0 ? "Keine Funktionsplugins installiert." : "";
        UpdateActions();
    }

    internal Task StopAsync()
    {
        _stopping = true;
        UpdateActions();
        return _operation?.StopAsync() ?? Task.CompletedTask;
    }

    private void Plugins_SelectedIndexChanged(object? sender, EventArgs e) => UpdateActions();
    private async void Toggle_Click(object? sender, EventArgs e) => await RunAsync(execute: false);
    private async void Execute_Click(object? sender, EventArgs e) => await RunAsync(execute: true);

    internal async Task RunAsync(bool execute)
    {
        if (_runtime is null || _operation is null || _busy || _stopping || plugins.SelectedItem is not Row row) return;
        _busy = true;
        UpdateActions();
        try
        {
            await _operation.RunAsync(async token =>
            {
                try
                {
                    if (execute)
                    {
                        await _runtime.ExecuteAsync(row.Plugin.Id, token);
                        status.Text = "Funktion ausgeführt.";
                    }
                    else
                    {
                        await _runtime.SetEnabledAsync(row.Plugin.Id, !row.Plugin.Enabled, token);
                        await LoadAsync(token);
                    }
                }
                catch (FunctionPluginException error) { status.Text = error.Message; }
            });
        }
        finally
        {
            _busy = false;
            if (!IsDisposed) UpdateActions();
        }
    }

    private void UpdateActions()
    {
        var row = plugins.SelectedItem as Row;
        var caption = row?.Plugin.Enabled == true ? "Deaktivieren" : "Aktivieren";
        if (toggleButton.Text != caption)
        {
            toggleButton.Text = caption;
            UiButtonSizing.Apply(toggleButton, executeButton);
        }
        toggleButton.Enabled = !_busy && !_stopping && row is not null;
        executeButton.Enabled = toggleButton.Enabled && row!.Plugin.Enabled;
        plugins.Enabled = !_busy && !_stopping;
    }

    private void DisposePage() => _operation?.Dispose();

    private sealed record Row(FunctionPluginChoice Plugin)
    {
        public string Text => $"{Plugin.Name} · {(Plugin.Enabled ? "Aktiv" : "Inaktiv")}";
    }
}

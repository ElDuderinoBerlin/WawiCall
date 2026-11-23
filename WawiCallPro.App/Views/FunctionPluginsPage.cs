using DevExpress.XtraEditors;
using DevExpress.Data;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Base;
using DevExpress.XtraGrid.Views.Grid;
using System.ComponentModel;
using System.IO;
using WawiCallPro.App.Hosting;
using WawiCallPro.Utilities.Operations;
using WawiCallPro.Utilities.Plugins;

namespace WawiCallPro.App.Views;

public partial class FunctionPluginsPage : XtraUserControl
{
    private const string GridLayoutKey = "FunctionPlugins-v1";
    private const string ActiveColumnCaption = "Aktiv";
    private IFunctionPluginRuntime? _runtime;
    private PageOperation? _operation;
    private Action<OperationEvent>? _report;
    private AppUiState? _state;
    private bool _bindingCatalog;
    private bool _busy;
    private bool _stopping;

    public FunctionPluginsPage()
    {
        InitializeComponent();
        UiButtonSizing.Attach(this);
        UiButtonSizing.Apply(executeButton);
        pluginsGrid.HandleCreated += FitActiveColumn;
        pluginsGrid.FontChanged += FitActiveColumn;
        pluginsGrid.DpiChangedAfterParent += FitActiveColumn;
        pluginsView.Appearance.HeaderPanel.Changed += FitActiveColumn;
        var appearance = LookAndFeel;
        appearance.StyleChanged += FitActiveColumn;
        Disposed += (_, _) => appearance.StyleChanged -= FitActiveColumn;
        FitActiveColumn(this, EventArgs.Empty);
        UpdateActions();
    }

    internal void Bind(IFunctionPluginRuntime runtime, Action<OperationEvent> report)
    {
        _runtime = runtime;
        _report = report;
        _operation = new PageOperation(this, report);
    }

    internal void ConfigureLayoutPersistence(AppUiState? state)
    {
        _state = state;
        state?.Restore(pluginsView, GridLayoutKey);
        FitActiveColumn(this, EventArgs.Empty);
    }

    internal async Task LoadAsync(CancellationToken token)
    {
        if (_runtime is null) return;
        var selectedId = (pluginsView.GetFocusedRow() as Row)?.Plugin.Id;
        IReadOnlyList<FunctionPluginChoice> catalog;
        try { catalog = await _runtime.ListAsync(token); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or FunctionPluginException)
        {
            ApplyCatalog([], selectedId);
            _report?.Invoke(OperationEvent.SettingsFunctionPluginsListFailed);
            return;
        }
        token.ThrowIfCancellationRequested();
        var rows = catalog.Select(plugin => new Row(plugin)).ToArray();
        ApplyCatalog(rows, selectedId);
        if (rows.Length == 0) _report?.Invoke(OperationEvent.SettingsFunctionPluginsEmpty);
    }

    private void ApplyCatalog(Row[] rows, string? focusedId)
    {
        _bindingCatalog = true;
        try
        {
            pluginsGrid.DataSource = rows;
            var index = Array.FindIndex(rows, row => row.Plugin.Id == focusedId);
            pluginsView.FocusedRowHandle = index >= 0 ? pluginsView.GetRowHandle(index)
                : rows.Length == 0 ? GridControl.InvalidRowHandle : 0;
        }
        finally { _bindingCatalog = false; }
        UpdateActions();
    }

    internal Task StopAsync()
    {
        _stopping = true;
        UpdateActions();
        return _operation?.StopAsync() ?? Task.CompletedTask;
    }

    private void FitActiveColumn(object? sender, EventArgs e)
    {
        if (pluginsGrid.IsDisposed) return;
        var captionWidth = TextRenderer.MeasureText(ActiveColumnCaption, pluginsView.Appearance.HeaderPanel.Font, Size.Empty,
            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;
        var width = Math.Max(pluginsGrid.LogicalToDeviceUnits(24), captionWidth + pluginsGrid.LogicalToDeviceUnits(10));
        if (pluginsView.OptionsSelection.CheckBoxSelectorColumnWidth != width)
            pluginsView.OptionsSelection.CheckBoxSelectorColumnWidth = width;
    }

    private static void Plugins_CustomDrawColumnHeader(object sender, ColumnHeaderCustomDrawEventArgs e)
    {
        if (e.Column?.FieldName != nameof(Row.Enabled)) return;
        e.Info.Caption = ActiveColumnCaption;
        e.DefaultDraw();
        e.Handled = true;
    }

    private void Plugins_FocusedRowChanged(object sender, FocusedRowChangedEventArgs e) => UpdateActions();

    private void Plugins_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.A) e.SuppressKeyPress = true;
    }

    private async void Plugins_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_bindingCatalog || _busy || _stopping || e.Action == CollectionChangeAction.Refresh
            || pluginsView.GetRow(e.ControllerRow) is not Row row) return;
        var enable = e.Action == CollectionChangeAction.Add;
        if (row.Plugin.Enabled == enable) return;
        await RunAsync(async (runtime, token) =>
        {
            await Task.Yield(); // Let the grid finish updating its bound checkbox before changing it.
            try
            {
                await runtime.SetEnabledAsync(row.Plugin.Id, enable, token);
                row.Plugin = row.Plugin with { Enabled = enable };
                _report?.Invoke(enable ? OperationEvent.SettingsFunctionPluginEnabled : OperationEvent.SettingsFunctionPluginDisabled);
            }
            catch (Exception error) when (error is FunctionPluginException or IOException or UnauthorizedAccessException)
            {
                _report?.Invoke(OperationEvent.SettingsFunctionPluginActivationFailed);
            }
            finally
            {
                if (!IsDisposed && pluginsView.GetRow(e.ControllerRow) == row && pluginsView.IsRowSelected(e.ControllerRow) != row.Plugin.Enabled)
                {
                    if (row.Plugin.Enabled) pluginsView.SelectRow(e.ControllerRow);
                    else pluginsView.UnselectRow(e.ControllerRow);
                }
            }
        });
    }

    private async void Execute_Click(object? sender, EventArgs e)
    {
        if (pluginsView.GetFocusedRow() is not Row { Plugin.Enabled: true } row) return;
        await RunAsync(async (runtime, token) =>
        {
            try
            {
                await runtime.ExecuteAsync(row.Plugin.Id, token);
                _report?.Invoke(OperationEvent.SettingsFunctionPluginExecuted);
            }
            catch (FunctionPluginException) { _report?.Invoke(OperationEvent.SettingsFunctionPluginExecutionFailed); }
        });
    }

    private async Task RunAsync(Func<IFunctionPluginRuntime, CancellationToken, Task> action)
    {
        if (_runtime is not { } runtime || _operation is null || _busy || _stopping) return;
        _busy = true;
        UpdateActions();
        try { await _operation.RunAsync(token => action(runtime, token)); }
        finally
        {
            _busy = false;
            if (!IsDisposed) UpdateActions();
        }
    }

    private void UpdateActions()
    {
        var ready = !_busy && !_stopping;
        executeButton.Enabled = ready && pluginsView.GetFocusedRow() is Row { Plugin.Enabled: true };
        pluginsGrid.Enabled = ready;
    }

    private void DisposePage()
    {
        _operation?.Dispose();
        _state?.Save(pluginsView, GridLayoutKey);
    }

    private sealed class Row(FunctionPluginChoice plugin)
    {
        public FunctionPluginChoice Plugin { get; set; } = plugin;
        public string Name => Plugin.Name;
        public string Version => Plugin.Version;
        public bool Enabled { get; set; } = plugin.Enabled;
    }
}

using System.IO;
using DevExpress.XtraEditors;
using WawiCallPro.App.Hosting;
using WawiCallPro.Utilities.Persistence;

namespace WawiCallPro.App.Views;

public sealed partial class FrmRecordingInventory : XtraForm
{
    private const string LayoutKey = "RecordingInventory-v1";
    private readonly IRecordingDirectoryInventory? _inventory;
    private readonly AppUiState? _state;
    private readonly PageOperation _operation;
    private bool _closing;
    private bool _backgroundRun;

    public FrmRecordingInventory()
    {
        InitializeComponent();
        _operation = new PageOperation(this, summaryLabel);
        AppIconHelper.Apply(this);
        WindowWorkArea.Attach(this);
        UiButtonSizing.Attach(this);
        refreshButton.Enabled = false;
    }

    internal FrmRecordingInventory(IRecordingDirectoryInventory? inventory, AppUiState? state) : this()
    {
        _inventory = inventory;
        _state = state;
        _state?.Restore(reviewView, LayoutKey);
        refreshButton.Enabled = inventory is not null;
    }

    internal void PrepareForBackgroundRun()
    {
        _backgroundRun = true;
        Opacity = 0;
    }

    protected override bool ShowWithoutActivation => _backgroundRun || base.ShowWithoutActivation;

    internal async Task RefreshAsync()
    {
        if (_inventory is null || _closing || _operation.IsRunning) return;
        refreshButton.Enabled = false;
        reviewGrid.DataSource = null;
        try
        {
            await _operation.RunAsync(async token =>
            {
                summaryLabel.Text = "Aufnahmeordner wird geprüft …";
                try
                {
                    var (result, rows) = await Task.Run(async () =>
                    {
                        var inventory = await _inventory.ReadAsync(token).ConfigureAwait(false);
                        var entries = inventory.Review.OrderBy(entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase)
                            .Select(entry => new ReviewRow(entry.RelativePath, GetReason(entry))).ToArray();
                        return (inventory, entries);
                    }, token);
                    token.ThrowIfCancellationRequested();
                    reviewGrid.DataSource = rows;
                    summaryLabel.Text = $"{result.Registered} mit lokaler Zuordnung · {result.WithoutLocalReference} ohne lokale Zuordnung · "
                        + $"{result.TemporaryWithoutLocalReference} temporär ohne Zuordnung · Übersprungene Links: {result.LinksSkipped}";
                    if (rows.Length == 0) summaryLabel.Text += Environment.NewLine + "Keine Dateien mit Prüfbedarf gefunden.";
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { }
                catch (Exception)
                {
                    summaryLabel.Text = "Die Aufnahmeinventur konnte nicht vollständig gelesen werden. "
                        + "Bitte Kommunikationsdatenbank, Arbeitsplatzkennung und Ordnerzugriff prüfen.";
                }
            });
        }
        finally { if (!IsDisposed) refreshButton.Enabled = !_closing; }
    }

    private static string GetReason(RecordingDirectoryEntry entry) =>
        entry.IsLink ? "Link übersprungen" : entry.AwaitingConfirmation ? "WAV-Abschluss ausstehend" :
            Path.GetExtension(entry.RelativePath).Equals(".tmp", StringComparison.OrdinalIgnoreCase)
                ? "Temporäre Datei ohne Zuordnung" : "Ohne lokale Zuordnung";

    private async void Inventory_Shown(object? sender, EventArgs e) => await RefreshAsync();
    private async void Refresh_Click(object? sender, EventArgs e) => await RefreshAsync();
    private void Close_Click(object? sender, EventArgs e) => Close();

    private async void Inventory_Closing(object? sender, FormClosingEventArgs e)
    {
        if (!_operation.IsRunning) return;
        e.Cancel = true;
        if (_closing) return;
        _closing = true;
        await _operation.StopAsync();
        if (!IsDisposed && IsHandleCreated) BeginInvoke(new Action(Close));
    }

    private void DisposePage()
    {
        _operation.Dispose();
        _state?.Save(reviewView, LayoutKey);
    }

    private sealed record ReviewRow(string RelativePath, string Reason);
}

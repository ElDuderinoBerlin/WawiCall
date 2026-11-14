using DevExpress.XtraBars;

namespace WawiCallPro.App;

public partial class MainForm
{
    private CancellationTokenSource _partyOpening = new();
    private bool _partyOpeningDisposed;
    private void Parties_Click(object sender, ItemClickEventArgs e) => Navigate("parties");
    private void PartiesNavigation_Click(object? sender, EventArgs e) => Navigate("parties");
    private async Task OpenPartyWorkspaceAsync()
    {
        if (_closing || _workspace?.CanUseBusinessDirectory != true || _partyWorkspaces is null) return;
        if (_documents.TryGetValue("parties", out var existing)) { ActivateDocument(existing); return; }
        try
        {
            var token = _partyOpening.Token;
            var handle = await _partyWorkspaces.OpenAsync(token);
            if (_closing || token.IsCancellationRequested) { await handle.DisposeAsync(); return; }
            try
            {
                var host = new Panel { Dock = DockStyle.Fill };
                handle.View.Dock = DockStyle.Fill; host.Controls.Add(handle.View);
                var document = tabbedView.AddDocument(host); document.Caption = "Kunden und Lieferanten";
                _documents.Add("parties", document); _pluginPages.Add(document, handle); ActivateDocument(document);
            }
            catch { await handle.DisposeAsync(); throw; }
        }
        catch (OperationCanceledException) { }
        catch (Exception error) { if (!_closing) connectionLabel.Text = Hosting.PageOperation.Describe(error); }
    }
    private async Task StopPartyOpeningAsync()
    {
        var cancellation = _partyOpening;
        if (!_partyOpeningDisposed) cancellation.Cancel();
        await _selectionTask;
        if (!_partyOpeningDisposed && ReferenceEquals(_partyOpening, cancellation))
        { cancellation.Dispose(); _partyOpening = new(); }
    }
    private void DisposePartyOpening()
    {
        if (_partyOpeningDisposed) return;
        _partyOpeningDisposed = true; _partyOpening.Cancel(); _partyOpening.Dispose();
    }
}

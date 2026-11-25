using DevExpress.XtraBars;
using DevExpress.XtraBars.Docking2010.Views;
using WawiCallPro.App.Hosting;

namespace WawiCallPro.App;

public partial class MainForm
{
    private readonly ViewNavigationHistory<BaseDocument> _viewHistory = new();
    private bool IsWorkspaceDocument(BaseDocument document) => _documents.Values.Any(candidate => ReferenceEquals(candidate, document))
        || _pluginPages.ContainsKey(document) || _callPages.Values.Any(candidate => ReferenceEquals(candidate.Document, document));

    private void Document_Activated(object sender, DocumentEventArgs e)
    {
        // Native AddDocument can activate a new, not yet published tab (including a background call).
        if (!IsWorkspaceDocument(e.Document)) return;
        _lastDocument = e.Document; _viewHistory.Record(e.Document); UpdateViewNavigation();
        SaveWorkspaceTab(e.Document);
    }

    private void SaveWorkspaceTab(BaseDocument document)
    {
        if (_closing || !Visible || _state is null) return;
        var id = _documents.FirstOrDefault(pair => ReferenceEquals(pair.Value, document)).Key;
        if (id is "home" or "history" or "mail" or "help" or "parties") _state.SaveTab("MainWorkspace", id);
    }
    private void Document_Closed(object sender, DocumentEventArgs e)
    {
        _viewHistory.Remove(e.Document);
        if (ReferenceEquals(_lastDocument, e.Document)) _lastDocument = null;
        UpdateViewNavigation();
    }
    private void NavigateBack_Click(object sender, ItemClickEventArgs e)
    {
        if (_closing) return;
        if (_viewHistory.GoBack() is { } document) ActivateDocument(document);
        UpdateViewNavigation();
    }
    private void NavigateForward_Click(object sender, ItemClickEventArgs e)
    {
        if (_closing) return;
        if (_viewHistory.GoForward() is { } document) ActivateDocument(document);
        UpdateViewNavigation();
    }
    private void UpdateViewNavigation()
    {
        navigateBackItem.Enabled = !_closing && _viewHistory.CanGoBack;
        navigateForwardItem.Enabled = !_closing && _viewHistory.CanGoForward;
    }
}

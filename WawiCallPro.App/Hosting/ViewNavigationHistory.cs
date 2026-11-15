namespace WawiCallPro.App.Hosting;

/// <summary>UI-owned history of open views. Navigation does not create new entries; closed views are released.</summary>
internal sealed class ViewNavigationHistory<T> where T : class
{
    private readonly List<T> _views = [];
    private int _position = -1;
    internal bool CanGoBack => _position > 0;
    internal bool CanGoForward => _position >= 0 && _position < _views.Count - 1;

    internal void Record(T view)
    {
        if (_position >= 0 && ReferenceEquals(_views[_position], view)) return;
        if (CanGoForward) _views.RemoveRange(_position + 1, _views.Count - _position - 1);
        _views.Add(view); _position = _views.Count - 1;
    }
    internal T? GoBack() => CanGoBack ? _views[--_position] : null;
    internal T? GoForward() => CanGoForward ? _views[++_position] : null;
    internal void Remove(T view)
    {
        for (var index = _views.Count - 1; index >= 0; index--)
        {
            if (!ReferenceEquals(_views[index], view)) continue;
            _views.RemoveAt(index); if (index <= _position) _position--;
        }
        // Closing a tab can activate its neighbor before DocumentClosed and leave adjacent duplicate entries.
        for (var index = _views.Count - 1; index > 0; index--)
        {
            if (!ReferenceEquals(_views[index], _views[index - 1])) continue;
            _views.RemoveAt(index); if (index <= _position) _position--;
        }
        if (_position < 0 && _views.Count > 0) _position = 0;
    }
}

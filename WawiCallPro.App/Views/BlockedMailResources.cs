using DevExpress.Office.Services;
using System.IO;

namespace WawiCallPro.App.Views;

// Replacing the whole service prevents fallback to the default file/network providers.
internal sealed class BlockedMailResources : IUriStreamService
{
    internal int BlockedRequests { get; private set; }
    public Stream GetStream(string uri) { BlockedRequests++; return Stream.Null; }
    public void RegisterProvider(IUriStreamProvider provider) { }
    public void UnregisterProvider(IUriStreamProvider provider) { }
}

using DevExpress.XtraEditors;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.App.Hosting;

public interface IClientKeyExitPrompt
{
    bool Confirm(IWin32Window owner);
    void ReportFailure(IWin32Window owner);
}

internal sealed class ClientKeyExitPrompt : IClientKeyExitPrompt
{
    public bool Confirm(IWin32Window owner) => XtraMessageBox.Show(owner,
        "Der Client-Key steht unverschlüsselt in der lokalen appsettings.json. Soll er vor dem Beenden verschlüsselt werden?",
        "Client-Key schützen", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
    public void ReportFailure(IWin32Window owner) => XtraMessageBox.Show(owner,
        "Die lokale appsettings.json konnte nicht geschützt werden. Zugriffsrechte prüfen. Die Anwendung bleibt geöffnet.",
        "Client-Key schützen", MessageBoxButtons.OK, MessageBoxIcon.Warning);
}

internal sealed class ClientKeyExitProtection(ProductAppSettings settings, IClientKeyExitPrompt prompt)
{
    internal bool TryComplete(IWin32Window owner)
    {
        using var waiting = owner is Control control ? new UiWaitCursor(control) : null;
        try
        {
            if (settings.HasPlaintextClientKey() && prompt.Confirm(owner)) settings.EncryptClientKey();
            return true;
        }
        catch (Exception) { prompt.ReportFailure(owner); return false; }
    }
}

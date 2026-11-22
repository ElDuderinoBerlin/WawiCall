using DevExpress.XtraEditors;
using WawiCallPro.Interfaces.Plugins;

namespace WawiCallPro.Plugins.Lexware.Pro;

/// <summary>Plugin-owned connection editor; no discovery, authentication or file IO occurs while editing.</summary>
public partial class ProConnectionSettingsPage : XtraUserControl
{
    private ProConnectionEditorSnapshot _saved = new();

    public ProConnectionSettingsPage() { InitializeComponent(); UpdateEnabled(); }

    public override Size GetPreferredSize(Size proposedSize)
    {
        if (layout is null) return base.GetPreferredSize(proposedSize);
        var width = Math.Max(1, proposedSize.Width > 0 ? proposedSize.Width : Width);
        var available = Math.Max(1, width - layout.Padding.Horizontal);
        var labelWidth = layout.Controls.Cast<Control>().Where(control => layout.GetColumnSpan(control) == 1 && layout.GetColumn(control) == 0)
            .Max(control => control.GetPreferredSize(Size.Empty).Width + control.Margin.Horizontal);
        var heights = new int[layout.RowCount];
        foreach (Control control in layout.Controls)
        {
            if (!control.Visible) continue;
            var cellWidth = layout.GetColumnSpan(control) == 2 ? available : layout.GetColumn(control) == 0 ? labelWidth : Math.Max(1, available - labelWidth);
            var row = layout.GetRow(control);
            heights[row] = Math.Max(heights[row], control.GetPreferredSize(new(Math.Max(1, cellWidth - control.Margin.Horizontal), 0)).Height + control.Margin.Vertical);
        }
        // Measure wrapped hints at the actual width, including when the parent's scrollbar appears.
        return new(width, heights.Sum() + layout.Padding.Vertical + Padding.Vertical);
    }

    internal void Reload(IPluginConnectionEditContext context)
    {
        _saved = context.ReadPro();
        enabledEdit.Checked = _saved.Configured || _saved.InitialPassword is not null;
        usernameEdit.Text = _saved.Username ?? "";
        passwordEdit.Text = _saved.InitialPassword ?? "";
        clientKeyEdit.Text = "";
        UpdateEnabled();
    }

    internal void ApplyChanges(IPluginConnectionEditContext context) => context.StagePro(ReadChanges());

    internal ProConnectionEditorChange ReadChanges() => ProConnectionEditorRules.Read(ReadInput(), _saved);
    private ProConnectionEditorInput ReadInput() => new()
    {
        Enabled = enabledEdit.Checked, Endpoint = "", InstallationId = _saved.InstallationId ?? "",
        Username = usernameEdit.Text, Password = passwordEdit.Text, ClientKey = clientKeyEdit.Text,
        UseDefaultClientKey = clientKeyEdit.Text.Length == 0 && !_saved.ClientKeyStored && _saved.DefaultClientKeyAvailable,
        TimeoutSeconds = _saved.TimeoutSeconds, TokenTimeoutSeconds = _saved.TokenTimeoutSeconds
    };

    internal bool HasChanges()
    {
        if (!enabledEdit.Checked) return _saved.Configured;
        return !_saved.Configured
            || usernameEdit.Text.Trim() != (_saved.Username ?? "") || passwordEdit.Text.Length > 0 || clientKeyEdit.Text.Length > 0;
    }

    private void Enabled_Changed(object? sender, EventArgs e) => UpdateEnabled();
    private void UpdateEnabled()
    {
        var enabled = enabledEdit.Checked;
        usernameEdit.Enabled = passwordEdit.Enabled = clientKeyEdit.Enabled = enabled;
    }

    private void DisposePage() { if (!IsDisposed) passwordEdit.Text = clientKeyEdit.Text = ""; }
}

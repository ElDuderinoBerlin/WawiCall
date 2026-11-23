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
        var labelWidth = Math.Max(MeasureLabelColumn(), (int)layout.ColumnStyles[0].Width);
        var heights = new int[layout.RowCount];
        foreach (Control control in layout.Controls)
        {
            if (!control.Visible) continue;
            var cellWidth = layout.GetColumnSpan(control) > 1 ? available : layout.GetColumn(control) == 0 ? labelWidth : Math.Max(1, Math.Min(available - labelWidth, (int)layout.ColumnStyles[1].Width));
            var row = layout.GetRow(control);
            heights[row] = Math.Max(heights[row], control.GetPreferredSize(new(Math.Max(1, cellWidth - control.Margin.Horizontal), 0)).Height + control.Margin.Vertical);
        }
        // Measure wrapped hints at the actual width, including when the parent's scrollbar appears.
        return new(width, heights.Sum() + layout.Padding.Vertical + Padding.Vertical);
    }

    internal int MeasureLabelColumn() => new[] { usernameLabel, passwordLabel, clientKeyLabel }
        .Max(label => label.GetPreferredSize(Size.Empty).Width + label.Margin.Horizontal);

    internal void AlignColumns(int editorLeft, int editorWidth)
    {
        var labelColumn = layout.ColumnStyles[0];
        var editorColumn = layout.ColumnStyles[1];
        var labelWidth = editorLeft - layout.Left - layout.Padding.Left - usernameEdit.Margin.Left;
        var editorColumnWidth = editorWidth + usernameEdit.Margin.Horizontal;
        if (labelColumn.SizeType == SizeType.Absolute && labelColumn.Width == labelWidth
            && editorColumn.Width == editorColumnWidth) return;

        layout.SuspendLayout();
        try
        {
            labelColumn.SizeType = SizeType.Absolute;
            labelColumn.Width = labelWidth;
            editorColumn.Width = editorColumnWidth;
        }
        finally { layout.ResumeLayout(); }
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

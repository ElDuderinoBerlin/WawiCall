using DevExpress.XtraEditors;
using WawiCallPro.Interfaces.Plugins;
using WawiCallPro.Plugin.Windows;

namespace WawiCallPro.Plugins.Lexware.Pro;

/// <summary>Plugin-owned connection editor; no discovery, authentication or file IO occurs while editing.</summary>
public partial class ProConnectionSettingsPage : XtraUserControl
{
    private ProConnectionEditorSnapshot _saved = new();
    private readonly CancellationTokenSource _stopping = new();
    private Task _accessTest = Task.CompletedTask;
    private bool _testing;
    private readonly string _editHint;
    [System.ComponentModel.Browsable(false), System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal Func<CancellationToken, Task>? TestAccess { private get; set; }
    internal bool AccessEnabled => enabledEdit.Checked;

    public ProConnectionSettingsPage()
    {
        InitializeComponent();
        _editHint = hintLabel.Text;
        UpdateEnabled();
    }

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
        hintLabel.Text = _editHint;
        (context as IPluginSettingsPageContext)?.Presentation.SizeButtons(testAccessButton);
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

    private void Enabled_Changed(object? sender, EventArgs e)
    {
        AccessField_Changed(sender, e);
        UpdateEnabled();
    }

    private void AccessField_Changed(object? sender, EventArgs e) => hintLabel.Text = _editHint;
    private void UpdateEnabled()
    {
        enabledEdit.Enabled = !_testing;
        var enabled = enabledEdit.Checked && !_testing;
        usernameEdit.Enabled = passwordEdit.Enabled = clientKeyEdit.Enabled = enabled;
        testAccessButton.Enabled = enabled && TestAccess is not null;
    }

    private async void TestAccess_Click(object? sender, EventArgs e)
    {
        if (_testing || TestAccess is not { } test || _stopping.IsCancellationRequested) return;
        var token = _stopping.Token;
        _testing = true;
        UpdateEnabled();
        try { _accessTest = test(token); await _accessTest; }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        finally
        {
            _testing = false;
            if (!IsDisposed) UpdateEnabled();
        }
    }

    internal Task WaitForAccessTestAsync(CancellationToken token) => _accessTest.WaitAsync(token);

    internal async Task StopTestAsync()
    {
        if (IsDisposed) return;
        await _stopping.CancelAsync();
        try { await _accessTest; }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested) { }
    }

    internal void ShowAccessTestResult(bool passed)
    {
        if (IsDisposed) return;
        hintLabel.Text = passed ? "API-Zugang erfolgreich geprüft." : "API-Zugang konnte nicht bestätigt werden.";
    }

    private void DisposePage()
    {
        if (IsDisposed) return;
        _stopping.Cancel();
        _stopping.Dispose();
        passwordEdit.Text = clientKeyEdit.Text = "";
    }
}

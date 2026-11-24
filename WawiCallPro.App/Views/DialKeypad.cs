using DevExpress.XtraEditors;
using WawiCallPro.App.Hosting;

namespace WawiCallPro.App.Views;

public partial class DialKeypad : XtraUserControl
{
    private bool _responsive;
    public DialKeypad()
    {
        InitializeComponent();
        UiButtonSizing.Attach(this);
        UiButtonSizing.Apply(one, two, three, four, five, six, seven, eight, nine, star, zero, hash);
    }

    internal event Action<char>? DigitPressed;
    internal void FocusFirstKey() => one.Select();

    internal void EnableResponsiveLayout()
    {
        _responsive = true;
        AutoSize = false;
        keys.AutoSize = false;
        keys.Dock = DockStyle.Fill;
        foreach (ColumnStyle column in keys.ColumnStyles)
        {
            column.SizeType = SizeType.Percent;
            column.Width = 100F / keys.ColumnCount;
        }
        foreach (RowStyle row in keys.RowStyles)
        {
            row.SizeType = SizeType.Percent;
            row.Height = 100F / keys.RowCount;
        }
        foreach (var button in keys.Controls.OfType<SimpleButton>()) button.Dock = DockStyle.Fill;
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        if (keys is null) return base.GetPreferredSize(proposedSize);
        if (!_responsive) return keys.GetPreferredSize(Size.Empty);
        return new((one.MinimumSize.Width + one.Margin.Horizontal) * keys.ColumnCount,
            (one.MinimumSize.Height + one.Margin.Vertical) * keys.RowCount);
    }

    private void Digit_Click(object? sender, EventArgs e)
    {
        if (sender is SimpleButton { Text.Length: 1 } button) DigitPressed?.Invoke(button.Text[0]);
    }

    private void Digit_KeyPress(object? sender, KeyPressEventArgs e)
    {
        var key = char.ToUpperInvariant(e.KeyChar);
        if (!"0123456789*#ABCD".Contains(key)) return;
        e.Handled = true;
        DigitPressed?.Invoke(key);
    }
}

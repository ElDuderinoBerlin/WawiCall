using DevExpress.XtraEditors;
using WawiCallPro.App.Hosting;

namespace WawiCallPro.App.Views;

public partial class DialKeypad : XtraUserControl
{
    public DialKeypad()
    {
        InitializeComponent();
        UiButtonSizing.Attach(this);
        UiButtonSizing.Apply(one, two, three, four, five, six, seven, eight, nine, star, zero, hash);
    }

    internal event Action<char>? DigitPressed;
    internal void FocusFirstKey() => one.Select();

    public override Size GetPreferredSize(Size proposedSize) =>
        keys is null ? base.GetPreferredSize(proposedSize) : keys.GetPreferredSize(Size.Empty);

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

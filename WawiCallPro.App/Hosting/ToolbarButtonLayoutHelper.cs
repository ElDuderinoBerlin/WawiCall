using System.Runtime.CompilerServices;
using DevExpress.LookAndFeel;
using DevExpress.XtraEditors;

namespace WawiCallPro.App.Hosting;

/// <summary>Sizes all text button groups consistently from native skin/text metrics, independent of their host.</summary>
internal static class ToolbarButtonLayoutHelper
{
    private const int AdditionalHeight = 4;
    private static readonly ConditionalWeakTable<SimpleButton, ButtonBinding> Buttons = new();
    private static readonly ConditionalWeakTable<Control, object> Owners = new();
    [ThreadStatic] private static bool _resizing;

    internal static void Attach(Control owner)
    {
        if (Owners.TryGetValue(owner, out _)) return;
        Owners.Add(owner, new object());
        foreach (Control child in owner.Controls) Attach(child);
        ApplyContainer(owner);
        owner.ControlAdded += (_, e) =>
        {
            if (e.Control is null) return;
            Attach(e.Control);
            ApplyContainer(owner);
        };
    }

    private static void ApplyContainer(Control owner)
    {
        if (owner is TableLayoutPanel)
        {
            var buttons = owner.Controls.OfType<SimpleButton>().ToArray();
            if (buttons.Length > 0) Apply(buttons);
        }
        else if (owner is SimpleButton button && !Buttons.TryGetValue(button, out _)) Apply(button);
    }

    internal static Size Apply(params SimpleButton[] buttons)
    {
        var live = buttons.Where(button => !button.IsDisposed).Distinct().ToArray();
        if (live.Length == 0) return Size.Empty;
        var group = new ButtonGroup(live);
        foreach (var button in live)
            GetBinding(button).Group = group;
        return group.Resize();
    }

    internal static void UseNativeHeight(SimpleButton button)
    {
        var binding = GetBinding(button);
        binding.UseNativeHeight = true;
        binding.Group?.Resize();
    }

    private static ButtonBinding GetBinding(SimpleButton button) => Buttons.GetValue(button, static control =>
    {
        var binding = new ButtonBinding();
        EventHandler changed = (_, _) => binding.Group?.Resize();
        control.FontChanged += changed;
        control.Appearance.Changed += changed;
        control.TextChanged += changed;
        control.HandleCreated += changed;
        control.DpiChangedAfterParent += changed;
        UserLookAndFeel.Default.StyleChanged += changed;
        control.Disposed += (_, _) => UserLookAndFeel.Default.StyleChanged -= changed;
        return binding;
    });

    internal static Size GetPreferredSize(SimpleButton button)
    {
        var text = TextRenderer.MeasureText(button.Text, button.Font);
        var best = button.CalcBestSize();
        var extraHeight = Buttons.TryGetValue(button, out var binding) && binding.UseNativeHeight
            ? 0 : button.LogicalToDeviceUnits(AdditionalHeight);
        return new Size(
            Math.Max(best.Width, text.Width + button.Padding.Horizontal),
            Math.Max(best.Height, text.Height) + extraHeight);
    }

    private sealed class ButtonBinding
    {
        internal ButtonGroup? Group;
        internal bool UseNativeHeight;
    }

    private sealed class ButtonGroup(SimpleButton[] buttons)
    {
        internal Size Resize()
        {
            var live = buttons.Where(button => !button.IsDisposed).ToArray();
            if (live.Length == 0) return Size.Empty;
            if (_resizing) return new Size(live.Max(button => button.Width), live.Max(button => button.Height));
            _resizing = true;
            try
            {
                foreach (var button in live)
                {
                    button.AutoSize = false;
                    button.MinimumSize = Size.Empty;
                    var inset = button.LogicalToDeviceUnits(button.Text == "..." ? 4 : 12);
                    button.Padding = new Padding(inset, 0, inset, 0);
                    if ((button.Anchor & (AnchorStyles.Left | AnchorStyles.Right)) == (AnchorStyles.Left | AnchorStyles.Right))
                        button.Anchor &= ~AnchorStyles.Right;
                }
                var preferred = live.Select(GetPreferredSize).ToArray();
                var size = new Size(preferred.Max(value => value.Width), preferred.Max(value => value.Height));
                foreach (var button in live)
                {
                    button.MinimumSize = size;
                    button.Size = size;
                }
                return size;
            }
            finally { _resizing = false; }
        }
    }
}

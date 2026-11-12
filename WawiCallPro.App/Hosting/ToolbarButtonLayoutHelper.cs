using System.Runtime.CompilerServices;
using DevExpress.LookAndFeel;
using DevExpress.XtraEditors;

namespace WawiCallPro.App.Hosting;

/// <summary>Sizes application button groups using the LxTranslate toolbar metrics and lifecycle hooks.</summary>
internal static class ToolbarButtonLayoutHelper
{
    private const int MinimumHeight = 32;
    private const int HorizontalPadding = 24;
    private const int VerticalPadding = 14;
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
        {
            if (!Buttons.TryGetValue(button, out var binding))
            {
                binding = new ButtonBinding();
                Buttons.Add(button, binding);
                EventHandler changed = (_, _) => binding.Group?.Resize();
                button.FontChanged += changed;
                button.Appearance.Changed += changed;
                button.TextChanged += changed;
                button.HandleCreated += changed;
                button.DpiChangedAfterParent += changed;
                UserLookAndFeel.Default.StyleChanged += changed;
                button.Disposed += (_, _) => UserLookAndFeel.Default.StyleChanged -= changed;
            }
            binding.Group = group;
        }
        return group.Resize();
    }

    internal static Size GetPreferredSize(SimpleButton button)
    {
        var text = TextRenderer.MeasureText(button.Text, button.Font);
        var best = button.CalcBestSize();
        return new Size(
            Math.Max(best.Width, text.Width + (int)Math.Ceiling(HorizontalPadding * button.DeviceDpi / 96F)),
            Math.Max(MinimumHeight, Math.Max(best.Height, text.Height + button.LogicalToDeviceUnits(VerticalPadding))));
    }

    private sealed class ButtonBinding
    {
        internal ButtonGroup? Group;
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
                    // Insets are included in the measured size, as in LxTranslate. Control.Padding
                    // would be counted again by GetPreferredSize when a table arranges the button.
                    button.Padding = Padding.Empty;
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

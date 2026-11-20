using System.Runtime.CompilerServices;

namespace WawiCallPro.App.Hosting;

/// <summary>Keeps normal windows and their minimum sizes inside the current monitor's usable area.</summary>
internal static class WindowWorkArea
{
    private static readonly ConditionalWeakTable<Form, Binding> Bindings = new();

    internal static void Attach(Form form, Func<Form, Rectangle>? getArea = null)
    {
        if (Bindings.TryGetValue(form, out var existing))
        {
            if (getArea is not null) existing.GetArea = getArea;
            return;
        }
        var binding = new Binding(form, getArea ?? (window => Screen.FromControl(window).WorkingArea));
        Bindings.Add(form, binding);
        form.HandleCreated += binding.Changed;
        form.Load += binding.Changed;
        form.SizeChanged += binding.Changed;
        form.LocationChanged += binding.Changed;
        form.DpiChanged += binding.DpiChanged;
    }

    internal static Rectangle GetArea(Form form) => Bindings.TryGetValue(form, out var binding)
        ? binding.GetArea(form) : Screen.FromControl(form).WorkingArea;

    internal static Size LimitMinimum(Size minimum, Rectangle area) => new(
        Math.Clamp(minimum.Width, 0, Math.Max(1, area.Width)), Math.Clamp(minimum.Height, 0, Math.Max(1, area.Height)));

    internal static Rectangle Fit(Rectangle bounds, Size minimum, Rectangle area)
    {
        var width = Math.Clamp(bounds.Width, Math.Clamp(minimum.Width, 1, area.Width), area.Width);
        var height = Math.Clamp(bounds.Height, Math.Clamp(minimum.Height, 1, area.Height), area.Height);
        return new Rectangle(Math.Clamp(bounds.Left, area.Left, area.Right - width),
            Math.Clamp(bounds.Top, area.Top, area.Bottom - height), width, height);
    }

    internal static void Constrain(Form form, Rectangle area)
    {
        if (area.Width <= 0 || area.Height <= 0 || form.IsDisposed) return;
        form.MinimumSize = LimitMinimum(form.MinimumSize, area);
        if (form.WindowState == FormWindowState.Normal)
            form.Bounds = Fit(form.Bounds, form.MinimumSize, area);
    }

    private sealed class Binding(Form form, Func<Form, Rectangle> getArea)
    {
        private bool _applying;
        internal Func<Form, Rectangle> GetArea { get; set; } = getArea;
        internal void DpiChanged(object? sender, DpiChangedEventArgs e) => Changed(sender, e);
        internal void Changed(object? sender, EventArgs e)
        {
            if (_applying || !form.IsHandleCreated || form.IsDisposed) return;
            _applying = true;
            try { Constrain(form, GetArea(form)); }
            finally { _applying = false; }
        }
    }
}

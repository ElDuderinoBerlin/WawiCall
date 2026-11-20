namespace WawiCallPro.App.Hosting;

/// <summary>Normal window bounds with their source DPI and monitor-relative position. DPI zero identifies legacy pixels.</summary>
internal readonly record struct WindowPlacement(Rectangle Bounds, int Dpi, Rectangle WorkingArea, string Monitor, bool Maximized)
{
    internal Rectangle GetBounds(int targetDpi, Rectangle targetArea)
    {
        if (Dpi == 0) return Bounds;
        var factor = (double)targetDpi / Dpi;
        return new Rectangle(targetArea.Left + Scale(Bounds.Left - WorkingArea.Left, factor),
            targetArea.Top + Scale(Bounds.Top - WorkingArea.Top, factor),
            Math.Max(1, Scale(Bounds.Width, factor)), Math.Max(1, Scale(Bounds.Height, factor)));
    }

    private static int Scale(int pixels, double factor) => (int)Math.Round(pixels * factor, MidpointRounding.AwayFromZero);
}

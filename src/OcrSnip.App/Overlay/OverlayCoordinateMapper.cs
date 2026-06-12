using System.Windows;
using WpfPoint = System.Windows.Point;

namespace OcrSnip.App.Overlay;

public static class OverlayCoordinateMapper
{
    public static Int32Rect FromPhysicalPoints(WpfPoint a, WpfPoint b)
    {
        var left = (int)Math.Round(Math.Min(a.X, b.X));
        var top = (int)Math.Round(Math.Min(a.Y, b.Y));
        var right = (int)Math.Round(Math.Max(a.X, b.X));
        var bottom = (int)Math.Round(Math.Max(a.Y, b.Y));
        return new Int32Rect(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
    }

    public static Rect Normalize(WpfPoint a, WpfPoint b)
    {
        return new Rect(
            Math.Min(a.X, b.X),
            Math.Min(a.Y, b.Y),
            Math.Abs(a.X - b.X),
            Math.Abs(a.Y - b.Y));
    }
}
